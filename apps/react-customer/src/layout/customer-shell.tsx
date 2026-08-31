import type { ReactElement } from 'react';
import { NavLink, Outlet, useNavigate, type NavigateFunction } from 'react-router';
import { appRoleLabel } from '../auth/auth.mappers';
import type { ShellSession } from '../auth/auth.models';
import { getValidShellSession } from '../auth/session';
import { tokenStorage } from '../auth/token-storage';
import { SHELL_MESSAGES, type ShellMessages, tenantIdTitle } from './shell.messages';
import styles from './customer-shell.module.css';

/**
 * Authenticated chrome host (KYC-071).
 * Login lives outside this shell; feature screens mount via Outlet.
 */
export function CustomerShell(): ReactElement {
  const copy: ShellMessages = SHELL_MESSAGES;
  const navigate: NavigateFunction = useNavigate();
  const session: ShellSession | null = getValidShellSession();

  const tenantLabel: string =
    session?.tenantSlug?.trim() || session?.tenantId || '';

  function signOut(): void {
    tokenStorage.clearSession();
    void navigate('/login', { replace: true });
  }

  return (
    <div className={styles['shell']}>
      <header className={styles['header']}>
        <div className={styles['brandBlock']}>
          <p className={styles['brand']}>{copy.brand}</p>
          <nav className={styles['nav']} aria-label={copy.primaryNavAria}>
            <NavLink
              to="/cases"
              className={({ isActive }: { isActive: boolean }): string =>
                isActive
                  ? `${styles['navLink'] ?? ''} ${styles['navLinkActive'] ?? ''}`.trim()
                  : (styles['navLink'] ?? '')
              }
            >
              {copy.casesNav}
            </NavLink>
          </nav>
        </div>

        <div className={styles['session']}>
          {session ? (
            <div className={styles['who']} aria-live="polite">
              <span
                className={styles['tenant']}
                title={tenantIdTitle(session.tenantId)}
              >
                {tenantLabel}
              </span>
              <span className={styles['sep']} aria-hidden="true">
                ·
              </span>
              <span className={styles['email']}>{session.email}</span>
              <span className={styles['role']}>{appRoleLabel(session.role)}</span>
            </div>
          ) : null}
          <button type="button" className={styles['signOut']} onClick={signOut}>
            {copy.signOut}
          </button>
        </div>
      </header>

      <div className={styles['content']}>
        <Outlet />
      </div>
    </div>
  );
}
