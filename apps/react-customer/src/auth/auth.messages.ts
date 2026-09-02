import type { AppRole } from './auth.models';

/** Login page + auth failure copy (KYC-071; same contract as Angular KYC-061). */
export interface LoginMessages {
  readonly title: string;
  readonly lede: string;
  readonly tenantSlugLabel: string;
  readonly emailLabel: string;
  readonly passwordLabel: string;
  readonly submit: string;
  readonly submitting: string;
  readonly submittingAria: string;
  readonly tenantSlugRequired: string;
  readonly tenantSlugMaxLength: string;
  readonly emailRequired: string;
  readonly emailMaxLength: string;
  readonly emailInvalid: string;
  readonly passwordRequired: string;
  readonly passwordMaxLength: string;
  readonly signInFailed: string;
  readonly networkFailed: string;
  readonly rateLimited: string;
  readonly captchaLabel: string;
  readonly captchaRequired: string;
  readonly captchaHelp: string;
  readonly captchaUnavailable: string;
}

export const LOGIN_MESSAGES: LoginMessages = {
  title: 'Sign in',
  lede: 'Use your tenant slug, work email, and password.',
  tenantSlugLabel: 'Tenant slug',
  emailLabel: 'Email',
  passwordLabel: 'Password',
  submit: 'Sign in',
  submitting: 'Signing in…',
  submittingAria: 'Signing in',
  tenantSlugRequired: 'Tenant slug is required.',
  tenantSlugMaxLength: 'Tenant slug must be at most 64 characters.',
  emailRequired: 'Email is required.',
  emailMaxLength: 'Email must be at most 256 characters.',
  emailInvalid: 'Enter a valid email address.',
  passwordRequired: 'Password is required.',
  passwordMaxLength: 'Password must be at most 128 characters.',
  signInFailed: 'Sign-in failed. Check your details and try again.',
  networkFailed: 'Unable to reach the sign-in service. Try again in a moment.',
  rateLimited: 'Too many sign-in attempts. Wait a minute and try again.',
  captchaLabel: 'Verification',
  captchaRequired: 'Complete the verification check.',
  captchaHelp: 'Paste the verification token from your sign-in provider.',
  captchaUnavailable: 'Verification could not load. Refresh the page and try again.',
};

/** Shell role labels (display only). */
export const APP_ROLE_LABELS: Readonly<Record<AppRole, string>> = {
  TenantAdmin: 'Tenant admin',
  Reviewer: 'Reviewer',
  Customer: 'Customer',
};

export function appRoleLabel(role: AppRole): string {
  return APP_ROLE_LABELS[role];
}
