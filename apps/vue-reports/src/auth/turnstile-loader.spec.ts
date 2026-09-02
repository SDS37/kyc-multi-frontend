import { afterEach, describe, expect, it } from 'vitest';
import { loadTurnstileWidget } from './turnstile-loader';

describe('loadTurnstileWidget', (): void => {
  afterEach((): void => {
    document.querySelectorAll('script[data-kyc-turnstile]').forEach((node: Element): void => {
      node.remove();
    });
    delete (globalThis as { turnstile?: unknown }).turnstile;
  });

  it('replaces a leftover failed script and can resolve on a fresh load', async (): Promise<void> => {
    const leftover: HTMLScriptElement = document.createElement('script');
    leftover.setAttribute('data-kyc-turnstile', '1');
    leftover.setAttribute('data-kyc-turnstile-state', 'error');
    document.head.appendChild(leftover);

    const loading: Promise<unknown> = loadTurnstileWidget();
    const injected: HTMLScriptElement | null = document.querySelector(
      'script[data-kyc-turnstile]',
    );
    expect(injected).not.toBe(leftover);
    expect(injected?.getAttribute('data-kyc-turnstile-state')).toBe('pending');
    expect(document.contains(leftover)).toBe(false);

    (globalThis as { turnstile?: unknown }).turnstile = {
      render: (): string => 'widget-1',
      reset: (): void => {
        /* test double */
      },
      remove: (): void => {
        /* test double */
      },
    };
    injected?.dispatchEvent(new Event('load'));

    await expect(loading).resolves.toMatchObject({
      render: expect.any(Function),
    });
  });

  it('replaces a leftover script whose load event already fired', async (): Promise<void> => {
    const leftover: HTMLScriptElement = document.createElement('script');
    leftover.setAttribute('data-kyc-turnstile', '1');
    document.head.appendChild(leftover);

    const loading: Promise<unknown> = loadTurnstileWidget();
    const injected: HTMLScriptElement | null = document.querySelector(
      'script[data-kyc-turnstile]',
    );
    expect(injected).not.toBe(leftover);

    (globalThis as { turnstile?: unknown }).turnstile = {
      render: (): string => 'widget-1',
      reset: (): void => {
        /* test double */
      },
      remove: (): void => {
        /* test double */
      },
    };
    injected?.dispatchEvent(new Event('load'));

    await expect(loading).resolves.toMatchObject({
      render: expect.any(Function),
    });
  });

  it('resolves from a pending in-document script when Turnstile becomes available', async (): Promise<void> => {
    const pending: HTMLScriptElement = document.createElement('script');
    pending.setAttribute('data-kyc-turnstile', '1');
    pending.setAttribute('data-kyc-turnstile-state', 'pending');
    document.head.appendChild(pending);

    const loading: Promise<unknown> = loadTurnstileWidget();
    (globalThis as { turnstile?: unknown }).turnstile = {
      render: (): string => 'widget-1',
      reset: (): void => {
        /* test double */
      },
      remove: (): void => {
        /* test double */
      },
    };
    pending.dispatchEvent(new Event('load'));

    await expect(loading).resolves.toMatchObject({
      render: expect.any(Function),
    });
  });
});
