import { UI_MESSAGES } from '../shared/ui.messages';

/** Authenticated shell chrome (KYC-071+). */
export interface ShellMessages {
  readonly brand: string;
  readonly casesNav: string;
  readonly primaryNavAria: string;
  readonly signOut: string;
  readonly tenantIdTitlePrefix: string;
}

export const SHELL_MESSAGES: ShellMessages = {
  brand: UI_MESSAGES.brand,
  casesNav: 'My cases',
  primaryNavAria: 'Primary',
  signOut: UI_MESSAGES.signOut,
  tenantIdTitlePrefix: 'Tenant id: ',
};

/** Pure: title attribute for the tenant chip. */
export function tenantIdTitle(tenantId: string): string {
  return `${SHELL_MESSAGES.tenantIdTitlePrefix}${tenantId}`;
}
