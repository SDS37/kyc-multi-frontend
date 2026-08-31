import {
  type ChangeEvent,
  type FormEvent,
  type ReactElement,
  type RefObject,
  useEffect,
  useRef,
} from 'react';
import { CASES_LIST_MESSAGES, type CasesListMessages } from '../cases.messages';
import { CREATE_DRAFT_TITLE_MAX_LENGTH } from '../cases.models';
import styles from './create-draft-dialog.module.css';

const FOCUSABLE_SELECTOR: string =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export type CreateDraftDialogProps = {
  readonly title: string;
  readonly titleError: string | null;
  readonly touched: boolean;
  readonly creating: boolean;
  readonly formError: string | null;
  readonly onTitleChange: (value: string) => void;
  readonly onClose: () => void;
  readonly onSubmit: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * Presentational create-draft dialog (KYC-072).
 * Owns focus trap, Escape, and body scroll lock — no GraphQL.
 */
export function CreateDraftDialog(props: CreateDraftDialogProps): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;
  const dialogRef: RefObject<HTMLDivElement | null> = useRef<HTMLDivElement | null>(null);
  const titleInputRef: RefObject<HTMLInputElement | null> = useRef<HTMLInputElement | null>(
    null,
  );
  const creatingRef: RefObject<boolean> = useRef(props.creating);
  const onCloseRef: RefObject<() => void> = useRef(props.onClose);

  creatingRef.current = props.creating;
  onCloseRef.current = props.onClose;

  useEffect((): (() => void) => {
    const previousOverflow: string = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const previouslyFocused: HTMLElement | null =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const focusTimer: number = window.setTimeout((): void => {
      titleInputRef.current?.focus();
    }, 0);

    function onKeyDown(event: globalThis.KeyboardEvent): void {
      if (event.key === 'Escape') {
        if (creatingRef.current) {
          return;
        }
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key !== 'Tab') {
        return;
      }

      const dialog: HTMLDivElement | null = dialogRef.current;
      if (!dialog) {
        return;
      }

      const focusable: HTMLElement[] = Array.from(
        dialog.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      ).filter((el: HTMLElement): boolean => !el.hasAttribute('disabled'));

      if (focusable.length === 0) {
        event.preventDefault();
        return;
      }

      const first: HTMLElement | undefined = focusable[0];
      const last: HTMLElement | undefined = focusable[focusable.length - 1];
      if (!first || !last) {
        return;
      }

      const active: Element | null = document.activeElement;
      if (event.shiftKey) {
        if (active === first || !dialog.contains(active)) {
          event.preventDefault();
          last.focus();
        }
      } else if (active === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', onKeyDown);

    return (): void => {
      window.clearTimeout(focusTimer);
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
      previouslyFocused?.focus();
    };
  }, []);

  return (
    <div
      className={styles['dialogBackdrop']}
      role="presentation"
      onClick={props.onClose}
    >
      <div
        ref={dialogRef}
        className={styles['dialog']}
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-draft-title"
        onClick={(event): void => {
          event.stopPropagation();
        }}
      >
        <h2 id="create-draft-title" className={styles['dialogTitle']}>
          {copy.createDialogTitle}
        </h2>
        <form className={styles['dialogForm']} onSubmit={props.onSubmit} noValidate>
          {props.formError !== null ? (
            <p className={styles['dialogAlert']} role="alert">
              {props.formError}
            </p>
          ) : null}
          <label className={styles['dialogField']}>
            <span>{copy.createTitleLabel}</span>
            <input
              ref={titleInputRef}
              value={props.title}
              maxLength={CREATE_DRAFT_TITLE_MAX_LENGTH}
              autoComplete="off"
              aria-invalid={props.touched && props.titleError !== null}
              aria-describedby={
                props.touched && props.titleError !== null
                  ? 'create-title-error'
                  : undefined
              }
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                props.onTitleChange(event.target.value);
              }}
            />
            {props.touched && props.titleError !== null ? (
              <span id="create-title-error" className={styles['fieldError']}>
                {props.titleError}
              </span>
            ) : null}
          </label>
          <div className={styles['dialogActions']}>
            <button
              type="button"
              className={styles['dialogSecondary']}
              onClick={props.onClose}
              disabled={props.creating}
            >
              {copy.createCancel}
            </button>
            <button
              type="submit"
              className={styles['dialogPrimary']}
              disabled={props.creating}
              aria-busy={props.creating}
            >
              {props.creating ? copy.createSubmitting : copy.createSubmit}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
