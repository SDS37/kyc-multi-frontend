import { expect, test, type Locator, type Page } from '@playwright/test';
import {
  DEMO_CUSTOMER_EMAIL,
  DEMO_PASSWORD,
  DEMO_REVIEWER_EMAIL,
  DEMO_TENANT_SLUG,
} from './demo-accounts';

async function fillLogin(
  page: Page,
  email: string,
): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Tenant slug').fill(DEMO_TENANT_SLUG);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(DEMO_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('reviewer sees status counts and the latest-10 table', async ({
  page,
}): Promise<void> => {
  await fillLogin(page, DEMO_REVIEWER_EMAIL);
  await expect(page.getByRole('heading', { name: 'Reports' })).toBeVisible();
  const counts: Locator = page.getByLabel('Cases by status');
  await expect(counts).toBeVisible();
  await expect(counts.getByText('Draft', { exact: true })).toBeVisible();
  await expect(counts.getByText('Submitted', { exact: true })).toBeVisible();
  await expect(counts.getByText('In review', { exact: true })).toBeVisible();
  await expect(counts.getByText('Approved', { exact: true })).toBeVisible();
  await expect(counts.getByText('Rejected', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Latest cases' })).toBeVisible();
  const latest: Locator = page.getByRole('table');
  await expect(latest).toBeVisible();
  await expect(latest.getByRole('columnheader', { name: 'Title' })).toBeVisible();
  await expect(latest.getByRole('columnheader', { name: 'Status' })).toBeVisible();
  await expect(latest.getByRole('row').nth(1)).toBeVisible();
});

test('customer cannot open reports', async ({ page }): Promise<void> => {
  await fillLogin(page, DEMO_CUSTOMER_EMAIL);
  await expect(
    page.getByText(
      'This app is for reviewers and tenant admins. Use the customer portal to submit cases.',
    ),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Reports' })).toHaveCount(0);
  await expect(page).toHaveURL(/\/login/);
});
