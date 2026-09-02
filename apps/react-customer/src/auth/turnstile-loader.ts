/** Official Turnstile explicit-render script. Never take this URL from config. */
const TURNSTILE_SCRIPT_SRC: string =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
const TURNSTILE_SCRIPT_ATTR: string = 'data-kyc-turnstile';

export interface TurnstileWidgetApi {
  render: (
    container: HTMLElement,
    options: {
      sitekey: string;
      callback: (token: string) => void;
      'error-callback'?: () => void;
      'expired-callback'?: () => void;
      theme?: 'auto' | 'light' | 'dark';
    },
  ) => string;
  reset: (widgetId: string) => void;
  remove: (widgetId: string) => void;
}

function readTurnstile(): TurnstileWidgetApi | null {
  const candidate: unknown = (globalThis as { turnstile?: unknown }).turnstile;
  if (candidate === null || typeof candidate !== 'object') {
    return null;
  }
  const record: { render?: unknown; reset?: unknown; remove?: unknown } = candidate as {
    render?: unknown;
    reset?: unknown;
    remove?: unknown;
  };
  if (
    typeof record.render !== 'function' ||
    typeof record.reset !== 'function' ||
    typeof record.remove !== 'function'
  ) {
    return null;
  }
  return candidate as TurnstileWidgetApi;
}

/** Load the Turnstile script once per document. Site key is not interpolated into the URL. */
export function loadTurnstileWidget(): Promise<TurnstileWidgetApi> {
  const existing: TurnstileWidgetApi | null = readTurnstile();
  if (existing) {
    return Promise.resolve(existing);
  }

  return new Promise((resolve, reject): void => {
    const onReady = (): void => {
      const api: TurnstileWidgetApi | null = readTurnstile();
      if (api) {
        resolve(api);
        return;
      }
      reject(new Error('Turnstile API missing'));
    };

    const found: Element | null = document.querySelector(`script[${TURNSTILE_SCRIPT_ATTR}]`);
    if (found instanceof HTMLScriptElement) {
      found.addEventListener('load', onReady, { once: true });
      found.addEventListener(
        'error',
        (): void => {
          reject(new Error('Turnstile script failed'));
        },
        { once: true },
      );
      return;
    }

    const script: HTMLScriptElement = document.createElement('script');
    script.src = TURNSTILE_SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.setAttribute(TURNSTILE_SCRIPT_ATTR, '1');
    script.addEventListener('load', onReady, { once: true });
    script.addEventListener(
      'error',
      (): void => {
        reject(new Error('Turnstile script failed'));
      },
      { once: true },
    );
    document.head.appendChild(script);
  });
}
