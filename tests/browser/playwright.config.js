import { defineConfig, devices } from '@playwright/test';

if (!process.env.ASE_TEST_PASSWORD) throw new Error('ASE_TEST_PASSWORD is required');
const output = process.env.RESULTS_DIR;
if (!output) throw new Error('RESULTS_DIR must point to a fresh evidence directory');
export default defineConfig({
  testDir: './specs', fullyParallel: false, workers: 1, retries: 0, timeout: 30000,
  forbidOnly: true,
  metadata: { assignedOwner:'Varun K.', studentId:'IT24103696', scope:'Real local React + Identity + Fleet APIs; no route mocks' },
  outputDir: `${output}/test-results`,
  reporter: [['list'], ['html',{ outputFolder:`${output}/html`, open:'never' }], ['json',{ outputFile:`${output}/results.json` }], ['junit',{ outputFile:`${output}/junit.xml` }]],
  use: { ...devices['Desktop Chrome'], baseURL:'http://127.0.0.1:15173', headless:true,
    ...(process.env.ASE_BROWSER_CHANNEL ? { channel:process.env.ASE_BROWSER_CHANNEL } : {}),
    trace:'retain-on-failure', screenshot:'only-on-failure', video:'retain-on-failure', reducedMotion:'reduce' },
  projects:[{ name:'chromium' }],
});
