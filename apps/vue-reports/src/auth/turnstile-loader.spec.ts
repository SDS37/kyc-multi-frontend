import { afterEach, describe, expect, it } from 'vitest';
import { loadTurnstileWidget } from './turnstile-loader';

describe('loadTurnstileWidget', (): void => {
  afterEach((): void => {
    document.querySelectorAll('script[data-kyc-turnstile]').forEach((node: Element): void => {
      node.remove();
    });
    delete (globalThis as { turnstile?: unknown }).turnstile;
  });

  it('rejects when a leftover script already failed instead of hanging', async (): Promise<void> => {
    const leftover: HTMLScriptElement = document.createElement('script');
    leftover.setAttribute('data-kyc-turnstile', '1');
    leftover.setAttribute('data-kyc-turnstile-state', 'error');
    document.head.appendChild(leftover);

    await expect(loadTurnstileWidget()).rejects.toThrow('Turnstile script failed');
    await Promise.resolve();
  });

  it('rejects when the existing script has no pending state (events already fired)', async (): Promise<void> => {
    const leftover: HTMLScriptElement = document.createElement('script');
    leftover.setAttribute('data-kyc-turnstile', '1');
    document.head.appendChild(leftover);

    await expect(loadTurnstileWidget()).rejects.toThrow('Turnstile script failed');
    await Promise.resolve();
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
