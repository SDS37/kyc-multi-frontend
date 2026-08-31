import { type ReactElement } from 'react';
import { CASES_LIST_MESSAGES, type CasesListMessages } from '../cases.messages';
import styles from './cases-loading.module.css';

/** Presentational list loading state. */
export function CasesLoading(): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;

  return (
    <div className={styles['loading']} role="status" aria-live="polite">
      <span className={styles['spinner']} aria-label={copy.loadingAria} />
      <span>{copy.loading}</span>
    </div>
  );
}
