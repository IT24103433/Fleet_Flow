import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'vite';
import { normalizeValidationErrors } from '../utils/validationErrorUtils.js';

let server;
let bookings;
let maintenance;
const originalFetch = globalThis.fetch;
const originalStorage = Object.getOwnPropertyDescriptor(globalThis, 'sessionStorage');
before(async () => {
  Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: { getItem: () => 'test-token' } });
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  bookings = await server.ssrLoadModule('/src/services/bookingService.js');
  maintenance = await server.ssrLoadModule('/src/services/maintenanceService.js');
});
after(async () => {
  globalThis.fetch = originalFetch;
  if (originalStorage) Object.defineProperty(globalThis, 'sessionStorage', originalStorage);
  else delete globalThis.sessionStorage;
  await server?.close();
});

for (const type of ['application/json', 'application/problem+json', 'application/problem+json; charset=utf-8']) {
  test(`booking and maintenance preserve field errors from ${type}`, async () => {
    const errors = { StartDateTime: ['Choose a valid start.'], ServiceInformation: ['Service information is required.'] };
    globalThis.fetch = async () => new Response(JSON.stringify({ title: 'Validation failed', errors }), { status: 400, headers: { 'Content-Type': type } });
    for (const result of [await bookings.createBooking({}), await maintenance.createMaintenanceRecord('test-token', {})]) {
      assert.equal(result.success, false);
      assert.deepEqual(normalizeValidationErrors(result.errors), {
        startDateTime: 'Choose a valid start.', serviceInformation: 'Service information is required.',
      });
    }
  });
}

test('non-JSON service failures retain a readable fallback', async () => {
  globalThis.fetch = async () => new Response('Proxy unavailable', { status: 502, headers: { 'Content-Type': 'text/plain' } });
  for (const result of [await bookings.createBooking({}), await maintenance.createMaintenanceRecord('test-token', {})]) {
    assert.equal(result.success, false);
    assert.equal(result.status, 502);
    assert.equal(typeof result.message, 'string');
    assert.equal(result.errors, null);
  }
});
