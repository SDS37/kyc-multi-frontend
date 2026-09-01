import { AppRole } from './auth.models';

/** Login page + auth failure copy (KYC-061). */
export const LOGIN_MESSAGES = {
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
  wrongAppRole:
    'This app is for reviewers and tenant admins. Use the customer portal to submit cases.',
} as const;

/** Shell role labels (display only). */
export const APP_ROLE_LABELS: Readonly<Record<AppRole, string>> = {
  TenantAdmin: 'Tenant admin',
  Reviewer: 'Reviewer',
  Customer: 'Customer',
};

export function appRoleLabel(role: AppRole): string {
  return APP_ROLE_LABELS[role];
}
