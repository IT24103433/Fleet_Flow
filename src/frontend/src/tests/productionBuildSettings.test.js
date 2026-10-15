import test from 'node:test';
import assert from 'node:assert/strict';
import { validateApiBuildSettings } from '../../buildConfig.mjs';
const settings = Object.fromEntries(['IDENTITY', 'FLEET', 'RENTAL', 'NOTIFICATION'].map(service => [`VITE_${service}_API_URL`, `https://${service.toLowerCase()}.example.com`]));
test('production builds require all four public HTTPS API origins', () => {
  assert.doesNotThrow(() => validateApiBuildSettings(settings));
  assert.throws(() => validateApiBuildSettings({ ...settings, VITE_RENTAL_API_URL: undefined }), /explicitly configured/);
  assert.throws(() => validateApiBuildSettings({ ...settings, VITE_IDENTITY_API_URL: 'http://localhost:5011' }), /localhost/);
});
test('production builds reject credentials, paths and private destinations', () => {
  for (const url of ['https://user:password@example.com', 'https://example.com/api', 'https://127.0.0.1', 'https://192.168.1.2', 'http://example.com'])
    assert.throws(() => validateApiBuildSettings({ ...settings, VITE_FLEET_API_URL: url }));
});
test('explicit local review builds accept only loopback roots', () => {
  const local = Object.fromEntries(Object.keys(settings).map((key, index) => [key, `http://127.0.0.1:${5011 + index}`]));
  assert.doesNotThrow(() => validateApiBuildSettings(local, { localReview: true }));
  assert.throws(() => validateApiBuildSettings(settings, { localReview: true }));
});
