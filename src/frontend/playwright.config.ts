import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  retries: 0,
  workers: 1,
  timeout: 120000,
  use: {
    baseURL: 'http://localhost:5173',
    channel: 'msedge',
    headless: true,
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 120000,
  },
});
