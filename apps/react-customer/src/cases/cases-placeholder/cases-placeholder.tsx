import type { ReactElement } from 'react';
import { SHELL_MESSAGES, type ShellMessages } from '../../layout/shell.messages';
import styles from './cases-placeholder.module.css';

/** Stub my-cases screen until KYC-072 (satisfies post-login redirect). */
export function CasesPlaceholder(): ReactElement {
  const copy: ShellMessages = SHELL_MESSAGES;

  return (
    <section className={styles['panel']} aria-labelledby="cases-heading">
      <h1 id="cases-heading" className={styles['title']}>
        {copy.casesPlaceholderTitle}
      </h1>
      <p className={styles['lede']}>{copy.casesPlaceholderLede}</p>
    </section>
  );
}
