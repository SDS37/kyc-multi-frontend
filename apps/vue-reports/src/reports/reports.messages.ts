/** Reports home chrome (KYC-080 placeholder; counts land in KYC-081). */
export interface ReportsHomeMessages {
  readonly pageTitle: string;
  readonly lede: string;
  readonly pendingHint: string;
}

export const REPORTS_HOME_MESSAGES: ReportsHomeMessages = {
  pageTitle: 'Reports',
  lede: 'Tenant-wide case overview for reviewers and tenant admins.',
  pendingHint: 'Status counts and the latest cases will appear here in the next story.',
};
