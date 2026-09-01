import { UI_MESSAGES } from '../shared/ui.messages';

/** Authenticated shell chrome (KYC-080). */
export interface ShellMessages {
  readonly brand: string;
  readonly reportsNav: string;
  readonly primaryNavAria: string;
  readonly skipToContent: string;
  readonly signOut: string;
  readonly tenantIdTitlePrefix: string;
}

export const SHELL_MESSAGES: ShellMessages = {
  brand: UI_MESSAGES.brand,
  reportsNav: 'Reports',
  primaryNavAria: 'Primary',
  skipToContent: 'Skip to main content',
  signOut: UI_MESSAGES.signOut,
  tenantIdTitlePrefix: 'Tenant id: ',
};

/** Pure: title attribute for the tenant chip. */
export function tenantIdTitle(tenantId: string): string {
  return `${SHELL_MESSAGES.tenantIdTitlePrefix}${tenantId}`;
}
