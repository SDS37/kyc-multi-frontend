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

async function expectSeededStatusCount(counts: Locator, label: string): Promise<void> {
  const labelNode: Locator = counts.getByText(label, { exact: true });
  await expect(labelNode).toBeVisible();
  const valueNode: Locator = labelNode.locator('xpath=following-sibling::*[1]');
  await expect(valueNode).toBeVisible();
  const raw: string = (await valueNode.innerText()).trim();
  const count: number = Number.parseInt(raw, 10);
  expect(Number.isInteger(count) && count > 0).toBe(true);
}

test('reviewer sees status counts and the latest-10 table', async ({
  page,
}): Promise<void> => {
  await fillLogin(page, DEMO_REVIEWER_EMAIL);
  await expect(page.getByRole('heading', { name: 'Reports' })).toBeVisible();
  const counts: Locator = page.getByRole('region', { name: 'Cases by status' });
  await expect(counts).toBeVisible();
  await expectSeededStatusCount(counts, 'Draft');
  await expectSeededStatusCount(counts, 'Submitted');
  await expectSeededStatusCount(counts, 'In review');
  await expectSeededStatusCount(counts, 'Approved');
  await expectSeededStatusCount(counts, 'Rejected');
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
