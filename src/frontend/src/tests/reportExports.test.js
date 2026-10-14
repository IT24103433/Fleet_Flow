import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM, VirtualConsole } from 'jsdom';
import { createServer } from 'vite';
import { createReportClient } from '../services/reportService.js';

test('CSV download authenticates, preserves selected status and requests fresh data', async () => {
  const calls = []; const controller = new AbortController();
  const client = createReportClient({ baseUrl: 'http://127.0.0.1:5002', getToken: () => 'admin-token',
    fetchImpl: async (url, options) => { calls.push({ url, options }); return new Response('\uFEFF"Booking ID"\r\n', { headers: { 'Content-Type': 'text/csv; charset=utf-8' } }); } });
  assert.match(await (await client.exportReport('bookings/export?status=Cancelled', controller.signal)).text(), /Booking ID/);
  assert.equal(calls[0].url, 'http://127.0.0.1:5002/api/reports/bookings/export?status=Cancelled');
  assert.equal(calls[0].options.headers.Authorization, 'Bearer admin-token'); assert.equal(calls[0].options.headers.Accept, 'text/csv');
  assert.equal(calls[0].options.cache, 'no-store'); assert.equal(calls[0].options.signal, controller.signal);
});
test('failed or non-CSV responses never become report downloads', async () => {
  await assert.rejects(createReportClient({ getToken: () => null }).exportReport('bookings/export'), /log in/);
  for (const [status, expected] of [[401, /session expired/], [403, /permission/], [503, /unavailable/]]) {
    const client = createReportClient({ getToken: () => 'token', fetchImpl: async () => new Response('{"title":"Export unavailable"}', { status, headers: { 'Content-Type': 'application/problem+json' } }) });
    await assert.rejects(client.exportReport('maintenance/export'), expected);
  }
  await assert.rejects(createReportClient({ getToken: () => 'token', fetchImpl: async () => new Response('<html>Sign in</html>', { headers: { 'Content-Type': 'text/html' } }) }).exportReport('bookings/export'), /did not return a CSV/);
});
test('export cancellation preserves AbortError instead of displaying a failure', async () => {
  const controller = new AbortController(); controller.abort();
  const client = createReportClient({ getToken: () => 'token', fetchImpl: async () => { throw new DOMException('cancelled', 'AbortError'); } });
  await assert.rejects(client.exportReport('maintenance/export', controller.signal), e => e.name === 'AbortError');
});

let server, dom, createRoot, ExportButton, AuthProvider;
const previous = new Map(); const errors = [];
let downloadCount = 0;
before(async () => {
  const console = new VirtualConsole(); console.on('jsdomError', e => errors.push(e));
  dom = new JSDOM('<html><body><div id="root"></div></body></html>', { url: 'http://localhost', virtualConsole: console });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement,
    sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { value, configurable: true, writable: true });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  previous.set('createObjectURL', Object.getOwnPropertyDescriptor(URL, 'createObjectURL'));
  previous.set('revokeObjectURL', Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL'));
  URL.createObjectURL = () => 'blob:test-export'; URL.revokeObjectURL = () => {};
  dom.window.HTMLAnchorElement.prototype.click = () => { downloadCount++; };
  sessionStorage.setItem('token', `x.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role: 'ADMIN' })).toString('base64url')}.x`);
  sessionStorage.setItem('user', JSON.stringify({ id: 'admin-test', username: 'admin', roles: ['ADMIN'] }));
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  ExportButton = (await server.ssrLoadModule('/src/pages/reports/ReportExportButton.jsx')).default;
  AuthProvider = (await server.ssrLoadModule('/src/context/AuthContext.jsx')).AuthProvider;
});
after(async () => {
  await server?.close(); dom?.window.close();
  for (const [key, descriptor] of previous) {
    const target = ['createObjectURL', 'revokeObjectURL'].includes(key) ? URL : globalThis;
    if (descriptor) Object.defineProperty(target, key, descriptor); else delete target[key];
  }
  assert.deepEqual(errors, []);
});
const render = path => React.createElement(AuthProvider, {}, React.createElement(ExportButton, { path, filename: 'fleetflow-bookings.csv' }));
const csv = () => new Response('"Booking ID"\r\n', { headers: { 'Content-Type': 'text/csv' } });
test('download action blocks duplicate clicks and waits for successful response', async () => {
  let complete; let requests = 0; downloadCount = 0;
  globalThis.fetch = async () => { requests++; return new Promise(resolve => { complete = () => resolve(csv()); }); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(render('bookings/export')));
    await act(async () => { document.querySelector('button').click(); document.querySelector('button').click(); });
    assert.equal(requests, 1); assert.equal(downloadCount, 0); assert.ok(document.querySelector('button').disabled);
    await act(async () => complete()); assert.equal(downloadCount, 1); assert.match(document.body.textContent, /Check your downloads/);
  } finally { await act(async () => root.unmount()); }
});
test('changing filters aborts the previous export and prevents stale-file download', async () => {
  let complete; let signal; downloadCount = 0;
  globalThis.fetch = async (_url, options) => { signal = options.signal; return new Promise(resolve => { complete = () => resolve(csv()); }); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(render('bookings/export?status=Confirmed')));
    await act(async () => document.querySelector('button').click());
    await act(async () => root.render(render('bookings/export?status=Cancelled'))); assert.ok(signal.aborted);
    await act(async () => complete()); assert.equal(downloadCount, 0); assert.ok(!document.querySelector('button').disabled);
  } finally { await act(async () => root.unmount()); }
});
test('failed export displays a useful error and leaves the action available for retry', async () => {
  globalThis.fetch = async () => new Response('{"title":"Export unavailable"}', { status: 503, headers: { 'Content-Type': 'application/problem+json' } });
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(render('bookings/export'))); await act(async () => document.querySelector('button').click());
    assert.match(document.querySelector('[role=alert]').textContent, /Export unavailable/); assert.ok(!document.querySelector('button').disabled);
  } finally { await act(async () => root.unmount()); }
});
