import type { ReactElement } from 'react';
import { Link } from 'react-router';
import { UI_MESSAGES } from '../../shared/ui.messages';
import {
  CASES_DRAFT_MESSAGES,
  CASES_LIST_MESSAGES,
  type CasesDraftMessages,
  type CasesListMessages,
} from '../cases.messages';
import styles from './case-draft.module.css';

export type CaseDraftLoadErrorProps = {
  readonly message: string;
  readonly showRetry: boolean;
  readonly onRetry: () => void;
};

/** Presentational load failure for the draft route. */
export function CaseDraftLoadError(props: CaseDraftLoadErrorProps): ReactElement {
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
      <div className={styles['loadError']} role="alert">
        <p className={styles['alert']}>{props.message}</p>
        {props.showRetry ? (
          <button type="button" className={styles['retry']} onClick={props.onRetry}>
            {UI_MESSAGES.tryAgain}
          </button>
        ) : null}
      </div>
    </section>
  );
}
