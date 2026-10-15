import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { validateApiBuildSettings } from './buildConfig.mjs'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  if (command === 'build') {
    const settings = { ...loadEnv(mode, process.cwd(), ''), ...process.env };
    validateApiBuildSettings(settings, { localReview: settings.FLEETFLOW_LOCAL_REVIEW_BUILD === '1' && settings.CI !== 'true' });
  }
  return { plugins: [react()], cacheDir: process.env.NODE_TEST_CONTEXT ? `node_modules/.vite-tests/${process.pid}` : undefined };
})
