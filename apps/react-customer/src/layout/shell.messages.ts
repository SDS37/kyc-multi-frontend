import { UI_MESSAGES } from '../shared/ui.messages';

/** Authenticated shell chrome (KYC-071; mirrors Angular KYC-064). */
export const SHELL_MESSAGES = {
  brand: UI_MESSAGES.brand,
  casesNav: 'My cases',
  primaryNavAria: 'Primary',
  signOut: UI_MESSAGES.signOut,
  tenantIdTitlePrefix: 'Tenant id: ',
  casesPlaceholderTitle: 'My cases',
  casesPlaceholderLede:
    'Your customer cases will appear here in the next story. You are signed in.',
} as const;

export type ShellMessages = typeof SHELL_MESSAGES;

/** Pure: title attribute for the tenant chip. */
export function tenantIdTitle(tenantId: string): string {
  return `${SHELL_MESSAGES.tenantIdTitlePrefix}${tenantId}`;
}
