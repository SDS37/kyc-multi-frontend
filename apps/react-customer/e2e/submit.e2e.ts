import { expect, test, type Page } from '@playwright/test';
import {
  DEMO_CUSTOMER_EMAIL,
  DEMO_PASSWORD,
  DEMO_TENANT_SLUG,
} from './demo-accounts';

const DEMO_PNG: Buffer = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

async function signInAsCustomer(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Tenant slug').fill(DEMO_TENANT_SLUG);
  await page.getByLabel('Email').fill(DEMO_CUSTOMER_EMAIL);
  await page.getByLabel('Password').fill(DEMO_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'My cases' })).toBeVisible();
}

test('customer creates a draft, uploads a PNG, and submits', async ({
  page,
}): Promise<void> => {
  const title: string = `[e2e] Submit ${String(Date.now())}`;

  await signInAsCustomer(page);
  await page.getByRole('button', { name: 'New case' }).click();
  await expect(page.getByRole('dialog', { name: 'Create draft case' })).toBeVisible();
  await page.getByRole('dialog').getByLabel('Title').fill(title);
  await page.getByRole('button', { name: 'Create draft' }).click();

  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByLabel('Full name').fill('Ada Lovelace');
  await page.getByLabel('Date of birth').fill('1815-12-10');
  await page.getByLabel('Nationality').fill('British');
  await page.getByLabel('Address').fill('12 Analytical Engine Rd');

  await page.getByLabel('Upload file').setInputFiles({
    name: 'id.png',
    mimeType: 'image/png',
    buffer: DEMO_PNG,
  });
  await expect(page.getByText('id.png')).toBeVisible();

  await page.getByRole('button', { name: 'Submit case' }).click();
  await expect(page.getByText('Case submitted.')).toBeVisible();
  await expect(page.locator('[data-status="SUBMITTED"]')).toHaveText('Submitted');
});
