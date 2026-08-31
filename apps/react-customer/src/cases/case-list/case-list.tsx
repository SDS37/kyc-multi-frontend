import {
  type ChangeEvent,
  type FormEvent,
  type ReactElement,
  type RefObject,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { Link, useNavigate, type NavigateFunction } from 'react-router';
import { UI_MESSAGES } from '../../shared/ui.messages';
import { createDraftCase, listCases } from '../cases-api';
import {
  parseStatusFilterValue,
  toCasesLoadError,
  toCreateDraftError,
  validateCreateDraftTitle,
} from '../cases.mappers';
import {
  CASE_STATUS_LABELS,
  CASES_LIST_MESSAGES,
  type CasesListMessages,
  casesCountLabel,
  casesEmptyForStatusLabel,
} from '../cases.messages';
import {
  CASE_STATUSES,
  CREATE_DRAFT_TITLE_MAX_LENGTH,
  type CaseListItem,
  type CaseListPage,
  type CaseStatus,
  type CreatedDraftCase,
} from '../cases.models';
import styles from './case-list.module.css';

const FOCUSABLE_SELECTOR: string =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Customer my-cases list + create draft (KYC-072).
 * Own-only filtering is enforced by the API JWT — client never sends user ids.
 */
export function CaseList(): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;
  const navigate: NavigateFunction = useNavigate();
  const loadSeq: RefObject<number> = useRef(0);
  const createLock: RefObject<boolean> = useRef(false);
  const creatingRef: RefObject<boolean> = useRef(false);
  const dialogRef: RefObject<HTMLDivElement | null> = useRef<HTMLDivElement | null>(null);
  const titleInputRef: RefObject<HTMLInputElement | null> = useRef<HTMLInputElement | null>(
    null,
  );

  const [statusFilter, setStatusFilter] = useState<CaseStatus | null>(null);
  const [items, setItems] = useState<readonly CaseListItem[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState<boolean>(false);
  const [createTitle, setCreateTitle] = useState<string>('');
  const [createTouched, setCreateTouched] = useState<boolean>(false);
  const [creating, setCreating] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const loadCases = useCallback(async (status: CaseStatus | null): Promise<void> => {
    const seq: number = loadSeq.current + 1;
    loadSeq.current = seq;
    setLoading(true);
    setLoadError(null);
    try {
      const page: CaseListPage = await listCases({ status });
      if (loadSeq.current !== seq) {
        return;
      }
      setItems(page.items);
      setTotalCount(page.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (loadSeq.current !== seq) {
        return;
      }
      setItems([]);
      setTotalCount(0);
      setLoading(false);
      setLoadError(toCasesLoadError(err).message);
    }
  }, []);

  useEffect((): void => {
    void loadCases(statusFilter);
  }, [loadCases, statusFilter]);

  useEffect((): (() => void) | void => {
    if (!createOpen) {
      return;
    }

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
        setCreateOpen(false);
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
  }, [createOpen]);

  const isEmpty: boolean = !loading && loadError === null && items.length === 0;
  const countLabel: string = casesCountLabel(totalCount);
  const emptyMessage: string = statusFilter
    ? casesEmptyForStatusLabel(statusFilter)
    : copy.emptyAll;
  const createTitleError: string | null = validateCreateDraftTitle(createTitle);

  function onStatusChange(event: ChangeEvent<HTMLSelectElement>): void {
    const raw: string = event.target.value;
    const parsed: CaseStatus | null | undefined = parseStatusFilterValue(
      raw === '' ? null : raw,
    );
    if (parsed === undefined) {
      return;
    }
    setStatusFilter(parsed);
  }

  function openCreateDialog(): void {
    setCreateTitle('');
    setCreateTouched(false);
    setCreateError(null);
    setCreateOpen(true);
  }

  function closeCreateDialog(): void {
    if (creatingRef.current) {
      return;
    }
    setCreateOpen(false);
  }

  async function onCreateSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setCreateTouched(true);
    setCreateError(null);
    if (createTitleError !== null || createLock.current) {
      return;
    }

    createLock.current = true;
    creatingRef.current = true;
    setCreating(true);
    try {
      const created: CreatedDraftCase = await createDraftCase({ title: createTitle });
      creatingRef.current = false;
      setCreating(false);
      createLock.current = false;
      setCreateOpen(false);
      void navigate(`/cases/${created.id}`);
    } catch (err: unknown) {
      creatingRef.current = false;
      createLock.current = false;
      setCreating(false);
      setCreateError(toCreateDraftError(err).message);
    }
  }

  return (
    <main className={styles['cases']}>
      <header className={styles['header']}>
        <div>
          <h1 id="cases-heading" className={styles['title']}>
            {copy.pageTitle}
          </h1>
          <p className={styles['lede']}>{copy.lede}</p>
        </div>
        <button type="button" className={styles['create']} onClick={openCreateDialog}>
          {copy.createAction}
        </button>
      </header>

      <section className={styles['toolbar']} aria-labelledby="cases-heading">
        <label className={styles['filter']}>
          <span className={styles['filterLabel']}>{copy.statusFilterLabel}</span>
          <select
            value={statusFilter ?? ''}
            onChange={onStatusChange}
            aria-label={copy.statusFilterAria}
          >
            <option value="">{copy.allStatuses}</option>
            {CASE_STATUSES.map((status: CaseStatus) => (
              <option key={status} value={status}>
                {CASE_STATUS_LABELS[status]}
              </option>
            ))}
          </select>
        </label>

        <p className={styles['count']} aria-live="polite">
          {loading ? copy.loading : loadError !== null ? loadError : countLabel}
        </p>
      </section>

      {loadError !== null ? (
        <div className={styles['alert']} role="alert">
          <p>{loadError}</p>
          <button
            type="button"
            className={styles['retry']}
            onClick={(): void => {
              void loadCases(statusFilter);
            }}
          >
            {UI_MESSAGES.tryAgain}
          </button>
        </div>
      ) : loading ? (
        <div className={styles['loading']} role="status" aria-live="polite">
          <span className={styles['spinner']} aria-label={copy.loadingAria} />
          <span>{copy.loading}</span>
        </div>
      ) : isEmpty ? (
        <p className={styles['empty']} role="status">
          {emptyMessage}
        </p>
      ) : (
        <div className={styles['tableWrap']}>
          <table className={styles['table']} aria-labelledby="cases-heading">
            <thead>
              <tr>
                <th scope="col">{copy.columnTitle}</th>
                <th scope="col">{copy.columnStatus}</th>
                <th scope="col">{copy.columnUpdated}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((row: CaseListItem) => (
                <tr key={row.id} className={styles['row']}>
                  <td>
                    <Link
                      className={styles['link']}
                      to={`/cases/${row.id}`}
                      aria-label={row.openAriaLabel}
                    >
                      {row.title}
                    </Link>
                  </td>
                  <td>
                    <span className={styles['status']} data-status={row.status}>
                      {row.statusLabel}
                    </span>
                  </td>
                  <td>{row.updatedAtLabel}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {createOpen ? (
        <div
          className={styles['dialogBackdrop']}
          role="presentation"
          onClick={closeCreateDialog}
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
            <form className={styles['dialogForm']} onSubmit={onCreateSubmit} noValidate>
              {createError !== null ? (
                <p className={styles['dialogAlert']} role="alert">
                  {createError}
                </p>
              ) : null}
              <label className={styles['dialogField']}>
                <span>{copy.createTitleLabel}</span>
                <input
                  ref={titleInputRef}
                  value={createTitle}
                  maxLength={CREATE_DRAFT_TITLE_MAX_LENGTH}
                  autoComplete="off"
                  aria-invalid={createTouched && createTitleError !== null}
                  aria-describedby={
                    createTouched && createTitleError !== null
                      ? 'create-title-error'
                      : undefined
                  }
                  onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                    setCreateTitle(event.target.value);
                  }}
                />
                {createTouched && createTitleError !== null ? (
                  <span id="create-title-error" className={styles['fieldError']}>
                    {createTitleError}
                  </span>
                ) : null}
              </label>
              <div className={styles['dialogActions']}>
                <button
                  type="button"
                  className={styles['dialogSecondary']}
                  onClick={closeCreateDialog}
                  disabled={creating}
                >
                  {copy.createCancel}
                </button>
                <button
                  type="submit"
                  className={styles['dialogPrimary']}
                  disabled={creating}
                  aria-busy={creating}
                >
                  {creating ? copy.createSubmitting : copy.createSubmit}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </main>
  );
}
