import { defineConfig, devices } from '@playwright/test';

const isCi: boolean = process.env['CI'] === 'true';
const baseURL: string = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,
  workers: 1,
  reporter: isCi ? [['github'], ['list']] : 'list',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL,
    trace: 'on-first-retry',
    ...devices['Desktop Chrome'],
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npm start -- --host localhost --port 4200',
    url: baseURL,
    reuseExistingServer: !isCi,
    timeout: 180_000,
  },
});
