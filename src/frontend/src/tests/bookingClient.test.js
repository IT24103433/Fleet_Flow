import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createBookingClient } from '../services/bookingService.js';
const booking = { vehicleId: 'vehicle', startDateTime: '2026-11-01T08:00:00Z', endDateTime: '2026-11-03T08:00:00Z' };
const response = (status, data) => ({ status, ok: status < 400, headers: { get: () => 'application/json' }, json: async () => data });
test('uncertain booking retries preserve their key and successful requests clear it', async () => {
  const calls = []; let index = 0; let key = 0;
  const client = createBookingClient({ baseUrl: 'https://rental.test', getToken: () => 'customer-token', newKey: () => `key-${++key}`,
    fetchImpl: async (url, options) => { calls.push({ url, options }); return response(index++ === 0 ? 503 : 201, { id: 'booking' }); } });
  assert.equal((await client.createBooking(booking)).success, false);
  assert.equal((await client.createBooking(booking)).success, true);
  assert.equal(calls[0].url, 'https://rental.test/api/bookings');
  assert.equal(calls[0].options.headers['Idempotency-Key'], calls[1].options.headers['Idempotency-Key']);
  await client.createBooking(booking); assert.notEqual(calls[1].options.headers['Idempotency-Key'], calls[2].options.headers['Idempotency-Key']);
});
test('request keys are isolated by account and date range; definitive conflicts clear them', async () => {
  let token = 'a'; let key = 0; const keys = [];
  const client = createBookingClient({ getToken: () => token, newKey: () => `key-${++key}`,
    fetchImpl: async (_url, options) => { keys.push(options.headers['Idempotency-Key']); return response(keys.length < 3 ? 503 : 409, {}); } });
  await client.createBooking(booking); token = 'b'; await client.createBooking(booking);
  await client.createBooking({ ...booking, endDateTime: '2026-11-04T08:00:00Z' });
  await client.createBooking({ ...booking, endDateTime: '2026-11-04T08:00:00Z' }); assert.equal(new Set(keys).size, 4);
});
test('history cancellation and availability use the configured Rental destination', async () => {
  const calls = []; const client = createBookingClient({ baseUrl: 'https://rental.test', getToken: () => 'token',
    fetchImpl: async (url, options) => { calls.push({ url, options }); return response(200, { isAvailable: true }); } });
  await client.getMyBookings(); await client.cancelBooking('id'); await client.checkVehicleAvailability('v', booking.startDateTime, booking.endDateTime);
  assert.equal(calls[0].url, 'https://rental.test/api/bookings'); assert.equal(calls[1].url, 'https://rental.test/api/bookings/id/cancel');
  assert.ok(calls[2].url.startsWith('https://rental.test/api/bookings/check-availability?'));
  assert.equal(calls[1].options.method, 'POST'); assert.equal(calls[2].options.headers.Authorization, undefined);
});
