import {
  type ChangeEvent,
  type ReactElement,
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react';
import { LOGIN_MESSAGES, type LoginMessages } from '../auth.messages';
import { loadTurnstileWidget, type TurnstileWidgetApi } from '../turnstile-loader';
import styles from './login-captcha.module.css';

export interface LoginCaptchaHandle {
  reset: () => void;
}

export interface LoginCaptchaProps {
  readonly siteKey: string;
  readonly disabled: boolean;
  readonly invalid: boolean;
  readonly value: string;
  readonly onTokenChange: (token: string) => void;
  readonly onLoadFailed: () => void;
  readonly onRetry: () => void;
}

/**
 * Presentational login captcha (KYC-094).
 * Turnstile widget when a site key is set; otherwise a labeled token field (API `test` provider).
 */
export const LoginCaptcha = forwardRef<LoginCaptchaHandle, LoginCaptchaProps>(
  function LoginCaptcha(
    { siteKey, disabled, invalid, value, onTokenChange, onLoadFailed, onRetry }: LoginCaptchaProps,
    ref,
  ): ReactElement {
    const copy: LoginMessages = LOGIN_MESSAGES;
    const usesWidget: boolean = siteKey.trim().length > 0;
    const hostRef = useRef<HTMLDivElement | null>(null);
    const apiRef = useRef<TurnstileWidgetApi | null>(null);
    const widgetIdRef = useRef<string | null>(null);
    const onTokenChangeRef = useRef(onTokenChange);
    const onLoadFailedRef = useRef(onLoadFailed);
    const onRetryRef = useRef(onRetry);
    const disabledRef = useRef(disabled);
    const unmountedRef = useRef(false);
    const [retryNonce, setRetryNonce] = useState<number>(0);
    const [widgetFailed, setWidgetFailed] = useState<boolean>(false);

    useEffect((): void => {
      onTokenChangeRef.current = onTokenChange;
      onLoadFailedRef.current = onLoadFailed;
      onRetryRef.current = onRetry;
      disabledRef.current = disabled;
    });

    useEffect((): (() => void) => {
      unmountedRef.current = false;
      return (): void => {
        unmountedRef.current = true;
      };
    }, []);

    useImperativeHandle(ref, (): LoginCaptchaHandle => {
      return {
        reset: (): void => {
          if (apiRef.current && widgetIdRef.current) {
            apiRef.current.reset(widgetIdRef.current);
          }
          onTokenChangeRef.current('');
        },
      };
    }, []);

    useEffect((): (() => void) | void => {
      if (!usesWidget) {
        return;
      }
      let cancelled: boolean = false;
      const site: string = siteKey.trim();
      void loadTurnstileWidget()
        .then((api: TurnstileWidgetApi): void => {
          if (cancelled) {
            return;
          }
          const host: HTMLDivElement | null = hostRef.current;
          if (!host) {
            setWidgetFailed(true);
            onLoadFailedRef.current();
            return;
          }
          apiRef.current = api;
          widgetIdRef.current = api.render(host, {
            sitekey: site,
            callback: (token: string): void => {
              if (cancelled || unmountedRef.current || disabledRef.current) {
                return;
              }
              onTokenChangeRef.current(token);
            },
            'expired-callback': (): void => {
              if (cancelled || unmountedRef.current || disabledRef.current) {
                return;
              }
              onTokenChangeRef.current('');
            },
            'error-callback': (): void => {
              if (cancelled || unmountedRef.current || disabledRef.current) {
                return;
              }
              onTokenChangeRef.current('');
            },
            theme: 'auto',
          });
        })
        .catch((): void => {
          if (!cancelled && !unmountedRef.current) {
            setWidgetFailed(true);
            onLoadFailedRef.current();
          }
        });

      return (): void => {
        cancelled = true;
        if (apiRef.current && widgetIdRef.current) {
          apiRef.current.remove(widgetIdRef.current);
        }
        apiRef.current = null;
        widgetIdRef.current = null;
      };
    }, [usesWidget, siteKey, retryNonce]);

    if (usesWidget) {
      const widgetClass: string = disabled
        ? `${styles['widget'] ?? ''} ${styles['widgetDisabled'] ?? ''}`.trim()
        : (styles['widget'] ?? '');
      return (
        <div className={styles['captcha']}>
          <p className={styles['label']} id="login-captcha-label">
            {copy.captchaLabel}
          </p>
          <div
            ref={hostRef}
            className={widgetClass}
            role="group"
            aria-labelledby="login-captcha-label"
            aria-disabled={disabled || undefined}
            aria-invalid={invalid || undefined}
            aria-describedby={invalid ? 'captcha-error' : undefined}
          />
          {invalid ? (
            <p id="captcha-error" className={styles['error']}>
              {copy.captchaRequired}
            </p>
          ) : null}
          {widgetFailed ? (
            <button
              type="button"
              className={styles['retry']}
              onClick={(): void => {
                setWidgetFailed(false);
                setRetryNonce((nonce: number): number => nonce + 1);
                onRetryRef.current();
              }}
            >
              {copy.captchaRetry}
            </button>
          ) : null}
        </div>
      );
    }

    return (
      <div className={styles['captcha']}>
        <label htmlFor="captchaToken">{copy.captchaLabel}</label>
        <input
          id="captchaToken"
          name="captchaToken"
          type="text"
          autoComplete="off"
          spellCheck={false}
          maxLength={2048}
          disabled={disabled}
          value={value}
          onChange={(event: ChangeEvent<HTMLInputElement>): void => {
            onTokenChange(event.target.value);
          }}
          aria-invalid={invalid}
          aria-describedby={invalid ? 'captcha-error' : 'captcha-help'}
        />
        <p id="captcha-help" className={styles['help']}>
          {copy.captchaHelp}
        </p>
        {invalid ? (
          <p id="captcha-error" className={styles['error']}>
            {copy.captchaRequired}
          </p>
        ) : null}
      </div>
    );
  },
);
