import {
  type ChangeEvent,
  type SubmitEvent,
  type ReactElement,
  type RefObject,
  useRef,
  useState,
} from 'react';
import {
  useNavigate,
  useSearchParams,
  type NavigateFunction,
  type SetURLSearchParams,
} from 'react-router';
import { UI_MESSAGES } from '../../shared/ui.messages';
import {
  hasLoginFieldErrors,
  resolvePostLoginUrl,
  toLoginFailedError,
  validateLoginForm,
} from '../auth.mappers';
import { LOGIN_MESSAGES, type LoginMessages } from '../auth.messages';
import type { LoginCredentials, LoginFieldErrors } from '../auth.models';
import { login } from '../login-api';
import styles from './login-page.module.css';

/**
 * Customer sign-in (KYC-071).
 * Layout and tokens mirror Angular admin login; native controls instead of Material.
 */
export function LoginPage(): ReactElement {
  const copy: LoginMessages = LOGIN_MESSAGES;
  const brand: string = UI_MESSAGES.brand;
  const navigate: NavigateFunction = useNavigate();
  const [searchParams]: [URLSearchParams, SetURLSearchParams] = useSearchParams();
  const submittingLock: RefObject<boolean> = useRef(false);

  const [tenantSlug, setTenantSlug] = useState<string>('');
  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [touched, setTouched] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [formError, setFormError] = useState<string | null>(null);

  const fieldErrors: LoginFieldErrors = validateLoginForm({
    tenantSlug,
    email,
    password,
  });

  async function onSubmit(event: SubmitEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setFormError(null);
    setTouched(true);

    const credentials: LoginCredentials = { tenantSlug, email, password };
    if (hasLoginFieldErrors(validateLoginForm(credentials)) || submittingLock.current) {
      return;
    }

    submittingLock.current = true;
    setSubmitting(true);
    try {
      await login(credentials);
      setSubmitting(false);
      const returnUrl: string | null = searchParams.get('returnUrl');
      void navigate(resolvePostLoginUrl(returnUrl), { replace: true });
    } catch (err: unknown) {
      submittingLock.current = false;
      setSubmitting(false);
      setFormError(toLoginFailedError(err).message);
    }
  }

  return (
    <main className={styles['login']}>
      <section className={styles['panel']} aria-labelledby="login-heading">
        <p className={styles['brand']}>{brand}</p>
        <h1 id="login-heading" className={styles['title']}>
          {copy.title}
        </h1>
        <p className={styles['lede']}>{copy.lede}</p>

        <form
          className={styles['form']}
          onSubmit={onSubmit}
          noValidate
          aria-describedby={formError ? 'login-form-error' : undefined}
        >
          {formError ? (
            <p
              id="login-form-error"
              className={styles['alert']}
              role="alert"
              aria-live="polite"
            >
              {formError}
            </p>
          ) : null}

          <div className={styles['field']}>
            <label htmlFor="tenantSlug">{copy.tenantSlugLabel}</label>
            <input
              id="tenantSlug"
              name="tenantSlug"
              autoComplete="organization"
              value={tenantSlug}
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                setTenantSlug(event.target.value);
              }}
              aria-invalid={touched && fieldErrors.tenantSlug !== undefined}
              aria-describedby={
                touched && fieldErrors.tenantSlug !== undefined
                  ? 'tenantSlug-error'
                  : undefined
              }
            />
            {touched && fieldErrors.tenantSlug !== undefined ? (
              <p id="tenantSlug-error" className={styles['fieldError']}>
                {fieldErrors.tenantSlug}
              </p>
            ) : null}
          </div>

          <div className={styles['field']}>
            <label htmlFor="email">{copy.emailLabel}</label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                setEmail(event.target.value);
              }}
              aria-invalid={touched && fieldErrors.email !== undefined}
              aria-describedby={
                touched && fieldErrors.email !== undefined ? 'email-error' : undefined
              }
            />
            {touched && fieldErrors.email !== undefined ? (
              <p id="email-error" className={styles['fieldError']}>
                {fieldErrors.email}
              </p>
            ) : null}
          </div>

          <div className={styles['field']}>
            <label htmlFor="password">{copy.passwordLabel}</label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event: ChangeEvent<HTMLInputElement>): void => {
                setPassword(event.target.value);
              }}
              aria-invalid={touched && fieldErrors.password !== undefined}
              aria-describedby={
                touched && fieldErrors.password !== undefined
                  ? 'password-error'
                  : undefined
              }
            />
            {touched && fieldErrors.password !== undefined ? (
              <p id="password-error" className={styles['fieldError']}>
                {fieldErrors.password}
              </p>
            ) : null}
          </div>

          <button
            type="submit"
            className={styles['submit']}
            disabled={submitting}
            aria-busy={submitting}
          >
            {submitting ? (
              <>
                <span
                  className={styles['spinner']}
                  role="status"
                  aria-label={copy.submittingAria}
                />
                <span>{copy.submitting}</span>
              </>
            ) : (
              copy.submit
            )}
          </button>
        </form>
      </section>
    </main>
  );
}
