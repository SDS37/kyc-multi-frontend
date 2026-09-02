/** Official Turnstile explicit-render script. Never take this URL from config. */
const TURNSTILE_SCRIPT_SRC: string =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
const TURNSTILE_SCRIPT_ATTR: string = 'data-kyc-turnstile';
const TURNSTILE_SCRIPT_STATE_ATTR: string = 'data-kyc-turnstile-state';
const SCRIPT_PENDING: string = 'pending';
const SCRIPT_READY: string = 'ready';
const SCRIPT_ERROR: string = 'error';

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

let inFlight: Promise<TurnstileWidgetApi> | null = null;

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

function loadTurnstileWidgetFrom(doc: Document): Promise<TurnstileWidgetApi> {
  const existing: TurnstileWidgetApi | null = readTurnstile();
  if (existing) {
    return Promise.resolve(existing);
  }
  if (inFlight !== null) {
    return inFlight;
  }

  const pending: Promise<TurnstileWidgetApi> = new Promise((resolve, reject): void => {
    const fail = (script: HTMLScriptElement | null, message: string): void => {
      if (script !== null) {
        script.setAttribute(TURNSTILE_SCRIPT_STATE_ATTR, SCRIPT_ERROR);
      }
      reject(new Error(message));
    };

    const onReady = (script: HTMLScriptElement): void => {
      script.setAttribute(TURNSTILE_SCRIPT_STATE_ATTR, SCRIPT_READY);
      const api: TurnstileWidgetApi | null = readTurnstile();
      if (api) {
        resolve(api);
        return;
      }
      fail(script, 'Turnstile API missing');
    };

    const waitForScript = (script: HTMLScriptElement): void => {
      script.addEventListener(
        'load',
        (): void => {
          onReady(script);
        },
        { once: true },
      );
      script.addEventListener(
        'error',
        (): void => {
          fail(script, 'Turnstile script failed');
        },
        { once: true },
      );
    };

    const found: Element | null = doc.querySelector(`script[${TURNSTILE_SCRIPT_ATTR}]`);
    if (found instanceof HTMLScriptElement) {
      const apiNow: TurnstileWidgetApi | null = readTurnstile();
      if (apiNow) {
        resolve(apiNow);
        return;
      }
      const state: string | null = found.getAttribute(TURNSTILE_SCRIPT_STATE_ATTR);
      if (state === SCRIPT_PENDING) {
        waitForScript(found);
        return;
      }
      // load/error already fired (or a prior attempt failed). Do not attach listeners.
      fail(found, 'Turnstile script failed');
      return;
    }

    const head: HTMLHeadElement | null = doc.head;
    if (head === null) {
      fail(null, 'Turnstile script failed');
      return;
    }

    const script: HTMLScriptElement = doc.createElement('script');
    script.src = TURNSTILE_SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.setAttribute(TURNSTILE_SCRIPT_ATTR, '1');
    script.setAttribute(TURNSTILE_SCRIPT_STATE_ATTR, SCRIPT_PENDING);
    waitForScript(script);
    head.appendChild(script);
  });

  inFlight = pending;
  void pending.catch((): void => {
    inFlight = null;
  });
  return pending;
}

/** Load the Turnstile script once per document. Site key is not interpolated into the URL. */
export function loadTurnstileWidget(): Promise<TurnstileWidgetApi> {
  return loadTurnstileWidgetFrom(document);
}
