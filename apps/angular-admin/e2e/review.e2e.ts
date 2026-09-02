import { expect, test, type Page, type Response } from '@playwright/test';
import {
  DEMO_PASSWORD,
  DEMO_REVIEWER_EMAIL,
  DEMO_TENANT_SLUG,
} from './demo-accounts';
import { prepareSubmittedCase } from './prepare-submitted-case';

async function signInAsReviewer(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Tenant slug').fill(DEMO_TENANT_SLUG);
  await page.getByLabel('Email').fill(DEMO_REVIEWER_EMAIL);
  await page.getByLabel('Password').fill(DEMO_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Cases' })).toBeVisible();
}

test('reviewer starts review, empty reject fails, download and approve succeed', async ({
  page,
}): Promise<void> => {
  const prepared: { readonly title: string; readonly documentFileName: string } =
    await prepareSubmittedCase();

  await signInAsReviewer(page);
  await page.getByRole('link', { name: `Open case ${prepared.title}` }).click();
  await expect(page.getByRole('heading', { name: prepared.title })).toBeVisible();
  await expect(page.locator('[data-status="SUBMITTED"]')).toHaveText('Submitted');

  await page.getByRole('button', { name: 'Start review' }).click();
  await expect(page.locator('[data-status="IN_REVIEW"]')).toHaveText('In review');

  await page.getByRole('button', { name: 'Reject' }).click();
  await expect(page.getByText('A comment is required to reject.')).toBeVisible();
  await expect(page.locator('[data-status="IN_REVIEW"]')).toHaveText('In review');

  const downloadOk: Promise<Response> = page.waitForResponse((res: Response): boolean => {
    return (
      res.request().method() === 'GET' &&
      res.url().includes('/documents/') &&
      res.status() === 200
    );
  });
  await page.getByRole('button', { name: `Download ${prepared.documentFileName}` }).click();
  const downloadResponse: Response = await downloadOk;
  expect(downloadResponse.ok()).toBe(true);

  await page.getByRole('button', { name: 'Approve' }).click();
  await expect(page.locator('[data-status="APPROVED"]')).toHaveText('Approved');
});
