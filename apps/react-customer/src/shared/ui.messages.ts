/** Shared user-facing chrome (brand + actions used across features). */
export interface UiMessages {
  readonly brand: string;
  readonly tryAgain: string;
  readonly emptyValue: string;
  readonly homeLede: string;
  readonly shellNavHome: string;
  readonly signOut: string;
  readonly noSession: string;
  readonly primaryNavLabel: string;
  readonly configGraphqlLabel: string;
  readonly configApiLabel: string;
}

export const UI_MESSAGES: UiMessages = {
  brand: 'KYC Customer',
  tryAgain: 'Try again',
  emptyValue: '—',
  homeLede: 'Customer portal foundation is ready. Sign-in arrives in the next story.',
  shellNavHome: 'Home',
  signOut: 'Sign out',
  noSession: 'No session',
  primaryNavLabel: 'Primary',
  configGraphqlLabel: 'GraphQL',
  configApiLabel: 'API',
};
