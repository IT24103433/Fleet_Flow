import test from 'node:test';
import assert from 'node:assert/strict';
import { createOutboxClient } from '../services/notificationOutboxService.js';
test('delivery filter and replay retain the Fleet producer endpoint and bearer contract', async () => {
  const calls = [];
  const client = createOutboxClient({ baseUrl: 'http://localhost:5002', getToken: () => 'token',
    fetchImpl: async (url, init) => { calls.push({ url, init }); return new Response(init.method === 'POST' ? null : '[]', { status: init.method === 'POST' ? 202 : 200 }); } });
  assert.deepEqual(await client.getEvents('Failed'), []);
  assert.equal(await client.replay('id'), null);
  assert.equal(calls[0].url, 'http://localhost:5002/api/notifications/outbox?status=Failed');
  assert.equal(calls[1].init.method, 'POST'); assert.equal(calls[1].init.headers.Authorization, 'Bearer token');
});
test('replay conflict and permission denial remain distinct', async () => {
  for (const [status, message] of [[409, /no longer eligible/], [403, /Administrator/], [401, /session expired/]]) {
    const client = createOutboxClient({ getToken: () => 'token', fetchImpl: async () => new Response(null, { status }) });
    await assert.rejects(client.replay('id'), message);
  }
});
test('signed-out replay does not issue a request and aborts are preserved', async () => {
  const client = createOutboxClient({ getToken: () => null, fetchImpl: () => { throw new Error('must not run'); } });
  await assert.rejects(client.replay('id'), /sign in/);
  const abort = new DOMException('Aborted', 'AbortError');
  const aborted = createOutboxClient({ getToken: () => 'token', fetchImpl: async () => { throw abort; } });
  await assert.rejects(aborted.getEvents(), error => error === abort);
});
