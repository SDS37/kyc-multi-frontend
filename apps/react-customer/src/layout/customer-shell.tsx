import type { ReactElement } from 'react';
import { Outlet } from 'react-router';
import { UI_MESSAGES, type UiMessages } from '../shared/ui.messages';
import { tokenStorage } from '../auth/token-storage';
import styles from './customer-shell.module.css';

/**
 * App chrome host (KYC-070) — guest home now; authenticated screens via Outlet (KYC-071+).
 */
export function CustomerShell(): ReactElement {
  const copy: UiMessages = UI_MESSAGES;
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
