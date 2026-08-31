import type { ReactElement } from 'react';
import { UI_MESSAGES, type UiMessages } from '../shared/ui.messages';
import { appConfig } from '../config/app-config';
import styles from './home-placeholder.module.css';

/** Foundation landing until KYC-071 login. */
export function HomePlaceholder(): ReactElement {
  const copy: UiMessages = UI_MESSAGES;

  return (
    <section className={styles['panel']} aria-labelledby="home-heading">
      <h1 id="home-heading" className={styles['title']}>
        {copy.brand}
      </h1>
      <p className={styles['lede']}>{copy.homeLede}</p>
      <dl className={styles['meta']}>
        <div>
          <dt>{copy.configGraphqlLabel}</dt>
          <dd>
            <code>{appConfig.graphqlUrl}</code>
          </dd>
        </div>
        <div>
          <dt>{copy.configApiLabel}</dt>
          <dd>
            <code>{appConfig.apiBaseUrl}</code>
          </dd>
        </div>
      </dl>
    </section>
  );
}
