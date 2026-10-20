import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests', testMatch: ['farmer/**/*.spec.js', 'retailCustomer/**/*.spec.js', 'farmer-retail-navigation.spec.js'], workers: 1, retries: 0,
  reporter: [['list'], ['json', { outputFile: 'test-results/farmer-retail-ui.json' }]],
  use: { baseURL: 'http://127.0.0.1:5192', channel: 'chrome', headless: true,
    viewport: { width: 1440, height: 1000 }, screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  webServer: process.env.FARMER_RETAIL_EXTERNAL_UI ? undefined : { command: 'npm run dev -- --port 5192', url: 'http://127.0.0.1:5192', timeout: 45000, reuseExistingServer: false },
});
