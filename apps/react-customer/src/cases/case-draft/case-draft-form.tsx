import type { ChangeEvent, ReactElement, SubmitEvent } from 'react';
import { Link } from 'react-router';
import { UI_MESSAGES } from '../../shared/ui.messages';
import {
  CASE_FORM_FIELD_LABELS,
  CASES_DRAFT_MESSAGES,
  CASES_LIST_MESSAGES,
  type CasesDraftMessages,
  type CasesListMessages,
} from '../cases.messages';
import {
  CASE_FORM_FIELD_KEYS,
  CREATE_DRAFT_TITLE_MAX_LENGTH,
  type CaseDraftDetail,
  type CaseFormFieldKey,
  type DraftFormFieldErrors,
  type DraftFormModel,
} from '../cases.models';
import styles from './case-draft.module.css';

export type CaseDraftFormProps = {
  readonly detail: CaseDraftDetail;
  readonly form: DraftFormModel;
  readonly fieldErrors: DraftFormFieldErrors;
  readonly touched: boolean;
  readonly actionError: string | null;
  readonly successMessage: string | null;
  readonly saving: boolean;
  readonly submitting: boolean;
  readonly onFieldChange: (field: keyof DraftFormModel, value: string) => void;
  readonly onSave: (event: SubmitEvent<HTMLFormElement>) => void;
  readonly onSubmitCase: () => void;
};

/**
 * Presentational draft editor (KYC-073).
 * Props/callbacks only — no GraphQL.
 */
export function CaseDraftForm(props: CaseDraftFormProps): ReactElement {
  const draftCopy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const listCopy: CasesListMessages = CASES_LIST_MESSAGES;
  const busy: boolean = props.saving || props.submitting;

  return (
    <section className={styles['panel']} aria-labelledby="draft-heading">
      <p className={styles['back']}>
        <Link to="/cases">{listCopy.backToCases}</Link>
      </p>
      <header className={styles['header']}>
        <h1 id="draft-heading" className={styles['title']}>
          {props.form.title.trim() || draftCopy.pageTitleFallback}
        </h1>
        <p className={styles['lede']}>{draftCopy.ledeEdit}</p>
        <ul className={styles['meta']}>
          <li>
            <span className={styles['status']} data-status={props.detail.status}>
              {props.detail.statusLabel}
            </span>
          </li>
          <li>
            {draftCopy.updatedLabel}: {props.detail.updatedAtLabel}
          </li>
        </ul>
      </header>

      {props.actionError !== null ? (
        <p className={styles['alert']} role="alert">
          {props.actionError}
        </p>
      ) : null}
      {props.successMessage !== null ? (
        <p className={styles['success']} role="status">
          {props.successMessage}
        </p>
      ) : null}

      <form className={styles['form']} onSubmit={props.onSave} noValidate>
        <fieldset className={styles['section']}>
          <legend className={styles['sectionLegend']}>{listCopy.createTitleLabel}</legend>
          <label className={styles['field']}>
            <span>{listCopy.createTitleLabel}</span>
            <input
              name="title"
              value={props.form.title}
              maxLength={CREATE_DRAFT_TITLE_MAX_LENGTH}
              autoComplete="off"
              disabled={busy}
              aria-invalid={props.touched && props.fieldErrors.title !== undefined}
              aria-describedby={
                props.touched && props.fieldErrors.title !== undefined
                  ? 'draft-title-error'
                  : undefined
              }
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                props.onFieldChange('title', event.target.value);
              }}
            />
            {props.touched && props.fieldErrors.title !== undefined ? (
              <span id="draft-title-error" className={styles['fieldError']}>
                {props.fieldErrors.title}
              </span>
            ) : null}
          </label>
        </fieldset>

        <fieldset className={styles['section']}>
          <legend className={styles['sectionLegend']}>{draftCopy.sectionPerson}</legend>
          {CASE_FORM_FIELD_KEYS.map((key: CaseFormFieldKey): ReactElement => {
            const errorId: string = `draft-${key}-error`;
            const error: string | undefined = props.fieldErrors[key];
            const showError: boolean = props.touched && error !== undefined;
            const isDob: boolean = key === 'dateOfBirth';
            const isAddress: boolean = key === 'address';
            const describedBy: string | undefined = showError
              ? errorId
              : isDob
                ? 'draft-dob-hint'
                : undefined;
            return (
              <label key={key} className={styles['field']}>
                <span>{CASE_FORM_FIELD_LABELS[key]}</span>
                {isAddress ? (
                  <textarea
                    name={key}
                    value={props.form[key]}
                    autoComplete="street-address"
                    disabled={busy}
                    rows={3}
                    aria-invalid={showError}
                    aria-describedby={describedBy}
                    onChange={(event: ChangeEvent<HTMLTextAreaElement>): void => {
                      props.onFieldChange(key, event.target.value);
                    }}
                  />
                ) : (
                  <input
                    name={key}
                    type={isDob ? 'date' : 'text'}
                    value={props.form[key]}
                    autoComplete="off"
                    disabled={busy}
                    aria-invalid={showError}
                    aria-describedby={describedBy}
                    onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                      props.onFieldChange(key, event.target.value);
                    }}
                  />
                )}
                {isDob && !showError ? (
                  <span id="draft-dob-hint" className={styles['hint']}>
                    {draftCopy.dateOfBirthHint}
                  </span>
                ) : null}
                {showError ? (
                  <span id={errorId} className={styles['fieldError']}>
                    {error}
                  </span>
                ) : null}
              </label>
            );
          })}
        </fieldset>

        <fieldset className={styles['section']}>
          <legend className={styles['sectionLegend']}>{draftCopy.sectionCompany}</legend>
          <p className={styles['hint']}>{draftCopy.companyOptionalHint}</p>
          <label className={styles['field']}>
            <span>{draftCopy.companyNameLabel}</span>
            <input
              name="companyName"
              value={props.form.companyName}
              autoComplete="organization"
              disabled={busy}
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                props.onFieldChange('companyName', event.target.value);
              }}
            />
          </label>
        </fieldset>

        <div className={styles['actions']}>
          <button
            type="submit"
            className={styles['secondary']}
            disabled={busy}
            aria-busy={props.saving}
          >
            {props.saving ? draftCopy.savingDraft : draftCopy.saveDraft}
          </button>
          <button
            type="button"
            className={styles['primary']}
            disabled={busy}
            aria-busy={props.submitting}
            onClick={props.onSubmitCase}
          >
            {props.submitting ? draftCopy.submitting : draftCopy.submit}
          </button>
        </div>
      </form>
    </section>
  );
}

export type CaseDraftReadonlyProps = {
  readonly detail: CaseDraftDetail;
  readonly successMessage?: string | null;
};

/** Presentational read-only view when the case is no longer a draft. */
export function CaseDraftReadonly(props: CaseDraftReadonlyProps): ReactElement {
  const draftCopy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const listCopy: CasesListMessages = CASES_LIST_MESSAGES;
  const form: DraftFormModel = props.detail.form;
  const successMessage: string | null = props.successMessage ?? null;

  return (
    <section className={styles['panel']} aria-labelledby="draft-heading">
      <p className={styles['back']}>
        <Link to="/cases">{listCopy.backToCases}</Link>
      </p>
      <header className={styles['header']}>
        <h1 id="draft-heading" className={styles['title']}>
          {props.detail.title}
        </h1>
        <p className={styles['lede']}>{draftCopy.ledeReadonly}</p>
        <ul className={styles['meta']}>
          <li>
            <span className={styles['status']} data-status={props.detail.status}>
              {props.detail.statusLabel}
            </span>
          </li>
          <li>
            {draftCopy.updatedLabel}: {props.detail.updatedAtLabel}
          </li>
          {props.detail.submittedAtLabel !== null ? (
            <li>
              {draftCopy.submittedLabel}: {props.detail.submittedAtLabel}
            </li>
          ) : null}
        </ul>
      </header>

      {successMessage !== null ? (
        <p className={styles['success']} role="status">
          {successMessage}
        </p>
      ) : null}

      <p className={styles['notice']} role="status">
        {draftCopy.readonlyNotice}
      </p>

      <dl className={styles['readonlyGrid']}>
        <div className={styles['readonlyRow']}>
          <dt className={styles['readonlyLabel']}>{listCopy.createTitleLabel}</dt>
          <dd className={styles['readonlyValue']}>{form.title || UI_MESSAGES.emptyValue}</dd>
        </div>
        {CASE_FORM_FIELD_KEYS.map((key: CaseFormFieldKey): ReactElement => (
          <div key={key} className={styles['readonlyRow']}>
            <dt className={styles['readonlyLabel']}>{CASE_FORM_FIELD_LABELS[key]}</dt>
            <dd className={styles['readonlyValue']}>
              {form[key].trim() || UI_MESSAGES.emptyValue}
            </dd>
          </div>
        ))}
        <div className={styles['readonlyRow']}>
          <dt className={styles['readonlyLabel']}>{draftCopy.companyNameLabel}</dt>
          <dd className={styles['readonlyValue']}>
            {form.companyName.trim() || UI_MESSAGES.emptyValue}
          </dd>
        </div>
      </dl>
    </section>
  );
}
