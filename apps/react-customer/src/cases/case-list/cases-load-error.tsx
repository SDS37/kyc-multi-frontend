import { type ReactElement } from 'react';
import { UI_MESSAGES } from '../../shared/ui.messages';
import styles from './cases-load-error.module.css';

export type CasesLoadErrorProps = {
  readonly message: string;
  readonly onRetry: () => void;
};

/** Presentational load-failure alert + retry. */
export function CasesLoadError(props: CasesLoadErrorProps): ReactElement {
  return (
    <div className={styles['alert']} role="alert">
      <p>{props.message}</p>
      <button type="button" className={styles['retry']} onClick={props.onRetry}>
        {UI_MESSAGES.tryAgain}
      </button>
    </div>
  );
}
