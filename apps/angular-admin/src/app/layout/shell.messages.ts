import { UI_MESSAGES } from '../shared/ui.messages';

/** Authenticated shell chrome (KYC-064). */
export const SHELL_MESSAGES = {
  brand: UI_MESSAGES.brand,
  casesNav: 'Cases',
  primaryNavAria: 'Primary',
  skipToContent: 'Skip to main content',
  signOut: 'Sign out',
  tenantIdTitlePrefix: 'Tenant id: ',
} as const;

/** Pure: title attribute for the tenant chip. */
export function tenantIdTitle(tenantId: string): string {
  return `${SHELL_MESSAGES.tenantIdTitlePrefix}${tenantId}`;
}
