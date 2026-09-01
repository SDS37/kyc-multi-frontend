import type { ReactElement } from 'react';
import { Link } from 'react-router';
import {
  CASES_DRAFT_MESSAGES,
  CASES_LIST_MESSAGES,
  type CasesDraftMessages,
  type CasesListMessages,
} from '../cases.messages';
import styles from './case-draft.module.css';

/** Presentational loading state for the draft route. */
export function CaseDraftLoading(): ReactElement {
  const draftCopy: CasesDraftMessages = CASES_DRAFT_MESSAGES;
  const listCopy: CasesListMessages = CASES_LIST_MESSAGES;

  return (
    <section className={styles['panel']} aria-labelledby="draft-heading">
      <p className={styles['back']}>
        <Link to="/cases">{listCopy.backToCases}</Link>
      </p>
      <h1 id="draft-heading" className={styles['title']}>
        {draftCopy.pageTitleFallback}
      </h1>
      <div className={styles['loading']} role="status" aria-live="polite">
        <span aria-label={draftCopy.loadingAria}>{draftCopy.loading}</span>
      </div>
    </section>
  );
}
