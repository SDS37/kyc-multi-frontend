/** Shared user-facing chrome (brand + actions used across features). */
export interface UiMessages {
  readonly brand: string;
  readonly tryAgain: string;
  readonly emptyValue: string;
  readonly signOut: string;
}

export const UI_MESSAGES: UiMessages = {
  brand: 'KYC Reports',
  tryAgain: 'Try again',
  emptyValue: '—',
  signOut: 'Sign out',
};
