const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: '.',
  testMatch: '*.spec.cjs',
  workers: 2,
  use: { baseURL: process.env.PLANORA_BASE_URL || 'http://localhost:5276',
    browserName: 'chromium', launchOptions: { channel: 'chrome' } },
  reporter: 'list',
});
