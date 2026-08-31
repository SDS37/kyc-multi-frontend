import type { ReactElement } from 'react';
import { Outlet } from 'react-router';
import { UI_MESSAGES } from '../shared/ui.messages';
import { tokenStorage } from '../auth/token-storage';
import styles from './customer-shell.module.css';

/**
 * Authenticated chrome host (KYC-070).
 * Feature screens mount via Outlet (KYC-072+).
 */
export function CustomerShell(): ReactElement {
  const copy: typeof UI_MESSAGES = UI_MESSAGES;
  const tenantSlug: string | null = tokenStorage.getTenantSlug();
  const hasSession: boolean = tokenStorage.getAccessToken() !== null;

  return (
    <div className={styles['shell']}>
      <header className={styles['header']}>
        <p className={styles['brand']}>{copy.brand}</p>
        <nav className={styles['nav']} aria-label={copy.primaryNavLabel}>
          <span className={styles['navItem']}>{copy.shellNavHome}</span>
        </nav>
        <div className={styles['meta']}>
          {hasSession ? (
            <p className={styles['session']} title={tenantSlug ?? undefined}>
              {tenantSlug ?? copy.emptyValue}
            </p>
          ) : (
            <p className={styles['sessionMuted']}>{copy.noSession}</p>
          )}
        </div>
      </header>
      <main className={styles['main']}>
        <Outlet />
      </main>
    </div>
  );
}
