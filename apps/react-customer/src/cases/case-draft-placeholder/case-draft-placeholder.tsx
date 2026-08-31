import type { ReactElement } from 'react';
import { Link, useParams } from 'react-router';
import {
  CASES_LIST_MESSAGES,
  type CasesListMessages,
} from '../cases.messages';
import styles from './case-draft-placeholder.module.css';

/** Thin draft host until KYC-073 form editor. */
export function CaseDraftPlaceholder(): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;
  const params: Readonly<Partial<Record<string, string>>> = useParams();
  const caseId: string = params['caseId'] ?? '';

  return (
    <section className={styles['panel']} aria-labelledby="draft-heading">
      <p className={styles['back']}>
        <Link to="/cases">{copy.backToCases}</Link>
      </p>
      <h1 id="draft-heading" className={styles['title']}>
        {copy.draftPlaceholderTitle}
      </h1>
      <p className={styles['lede']}>{copy.draftPlaceholderLede}</p>
      {caseId ? (
        <p className={styles['meta']}>
          <code>{caseId}</code>
        </p>
      ) : null}
    </section>
  );
}
