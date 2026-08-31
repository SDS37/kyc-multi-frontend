import { type ReactElement } from 'react';
import styles from './cases-empty.module.css';

export type CasesEmptyProps = {
  readonly message: string;
};

/** Presentational empty list message. */
export function CasesEmpty(props: CasesEmptyProps): ReactElement {
  return (
    <p className={styles['empty']} role="status">
      {props.message}
    </p>
  );
}
