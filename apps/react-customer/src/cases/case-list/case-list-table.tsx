import { type ReactElement } from 'react';
import { Link } from 'react-router';
import { CASES_LIST_MESSAGES, type CasesListMessages } from '../cases.messages';
import type { CaseListItem } from '../cases.models';
import styles from './case-list-table.module.css';

export type CaseListTableProps = {
  readonly items: readonly CaseListItem[];
  readonly labelledBy: string;
};

/** Presentational cases table — title links only; no fetch. */
export function CaseListTable(props: CaseListTableProps): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;

  return (
    <div className={styles['tableWrap']}>
      <table className={styles['table']} aria-labelledby={props.labelledBy}>
        <thead>
          <tr>
            <th scope="col">{copy.columnTitle}</th>
            <th scope="col">{copy.columnStatus}</th>
            <th scope="col">{copy.columnUpdated}</th>
          </tr>
        </thead>
        <tbody>
          {props.items.map((row: CaseListItem) => (
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
  );
}
