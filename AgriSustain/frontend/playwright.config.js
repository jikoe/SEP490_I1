import { defineConfig } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const apiRoot = fileURLToPath(new URL('../src/AgriSustain.Api', import.meta.url));

export default defineConfig({
  testDir: './tests',
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    channel: 'msedge',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    viewport: { width: 1440, height: 1000 },
  },
  webServer: [
    {
      command: `dotnet "${apiRoot}/bin/Debug/net8.0/AgriSustain.Api.dll" --contentRoot "${apiRoot}" --urls http://127.0.0.1:5179`,
      env: { Persistence__Mode: 'InMemory', ASPNETCORE_ENVIRONMENT: 'Development' },
      url: 'http://localhost:5179/health',
      reuseExistingServer: false,
      timeout: 45000,
    },
    {
      command: 'npm run dev',
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: false,
      timeout: 45000,
    },
  ],
});
