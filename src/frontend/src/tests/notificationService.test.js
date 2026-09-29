import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createNotificationClient } from '../services/notificationService.js';

function clientWithResponse(status, data) {
  const calls = [];
  const client = createNotificationClient({ baseUrl: 'https://fleet.test', getToken: () => 'user-token',
    fetchImpl: async (url, options) => {
      calls.push({ url, options });
      return { ok: status >= 200 && status < 300, status, json: async () => data };
    } });
  return { client, calls };
}

test('inbox uses authenticated current-user endpoint and retains notification contract', async () => {
  const data = [{ id: 'notification-id', category: 'Booking', title: 'Booking confirmed', message: 'Booking details',
    relatedEntityId: 'booking-id', vehicleId: 'vehicle-id', createdAt: '2026-09-29T00:00:00Z', isRead: false }];
  const { client, calls } = clientWithResponse(200, data);
  assert.deepEqual(await client.getInbox(), data);
  assert.equal(calls[0].url, 'https://fleet.test/api/notifications');
  assert.equal(calls[0].options.headers.Authorization, 'Bearer user-token');
  assert.equal(calls[0].options.method, 'GET');
});

test('empty inbox remains an empty array', async () => {
  const { client } = clientWithResponse(200, []);
  assert.deepEqual(await client.getInbox(), []);
});

test('unread count uses backend count contract including zero', async () => {
  const { client, calls } = clientWithResponse(200, { count: 0 });
  assert.deepEqual(await client.getUnreadCount(), { count: 0 });
  assert.equal(calls[0].url, 'https://fleet.test/api/notifications/unread-count');
});

test('mark read uses PATCH and handles the empty 204 response', async () => {
  const { client, calls } = clientWithResponse(204);
  assert.equal(await client.markRead('notification-id'), null);
  assert.equal(calls[0].options.method, 'PATCH');
  assert.equal(calls[0].url, 'https://fleet.test/api/notifications/notification-id/read');
});

test('mark read encodes the reference as a path segment', async () => {
  const { client, calls } = clientWithResponse(204);
  await client.markRead('id/extra');
  assert.equal(calls[0].url, 'https://fleet.test/api/notifications/id%2Fextra/read');
});

test('anonymous access fails before making a request', async () => {
  let requested = false;
  const client = createNotificationClient({ getToken: () => null, fetchImpl: async () => { requested = true; } });
  await assert.rejects(client.getInbox(), /Please log in/);
  assert.equal(requested, false);
});

test('expired session exposes an authentication error', async () => {
  const { client } = clientWithResponse(401);
  await assert.rejects(client.getInbox(), /session expired/);
});

test('cross-user or missing notification does not appear successfully marked read', async () => {
  const { client } = clientWithResponse(404);
  await assert.rejects(client.markRead('someone-elses-id'), /not found/);
});

test('service failure is distinguishable from an empty inbox', async () => {
  const { client } = clientWithResponse(503);
  await assert.rejects(client.getInbox(), /unavailable/);
});

test('request cancellation is forwarded for unmounted inboxes', async () => {
  const { client, calls } = clientWithResponse(200, []);
  const controller = new AbortController();
  await client.getInbox(controller.signal);
  assert.equal(calls[0].options.signal, controller.signal);
});

test('each request reads the current session token', async () => {
  let token = 'first-user';
  const headers = [];
  const client = createNotificationClient({ getToken: () => token, fetchImpl: async (_url, options) => {
    headers.push(options.headers.Authorization);
    return { ok: true, status: 200, json: async () => [] };
  } });
  await client.getInbox();
  token = 'second-user';
  await client.getInbox();
  assert.deepEqual(headers, ['Bearer first-user', 'Bearer second-user']);
});
