import { type ChangeEvent, type ReactElement } from 'react';
import { parseStatusFilterValue } from '../cases.mappers';
import {
  CASE_STATUS_LABELS,
  CASES_LIST_MESSAGES,
  type CasesListMessages,
} from '../cases.messages';
import { CASE_STATUSES, type CaseStatus } from '../cases.models';
import styles from './cases-toolbar.module.css';

export type CasesToolbarProps = {
  readonly statusFilter: CaseStatus | null;
  readonly countText: string | null;
  readonly onStatusChange: (status: CaseStatus | null) => void;
};

/** Presentational status filter + count line. */
export function CasesToolbar(props: CasesToolbarProps): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;

  function onSelectChange(event: ChangeEvent<HTMLSelectElement>): void {
    const raw: string = event.target.value;
    const parsed: CaseStatus | null | undefined = parseStatusFilterValue(
      raw === '' ? null : raw,
    );
    if (parsed === undefined) {
      return;
    }
    props.onStatusChange(parsed);
  }

  return (
    <section className={styles['toolbar']} aria-labelledby="cases-heading">
      <label className={styles['filter']}>
        <span className={styles['filterLabel']}>{copy.statusFilterLabel}</span>
        <select
          value={props.statusFilter ?? ''}
          onChange={onSelectChange}
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
        {props.countText}
      </p>
    </section>
  );
}
