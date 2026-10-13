import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';

let server;
let dom;
let createRoot;
let AuthProvider;
let ChangePasswordModal;
let CustomerHomePage;
let MaintenanceDashboardPage;
const globals = new Map();

before(async () => {
  dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: 'http://localhost' });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    globals.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  globals.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  ({ AuthProvider } = await server.ssrLoadModule('/src/context/AuthContext.jsx'));
  ChangePasswordModal = (await server.ssrLoadModule('/src/components/security/ChangePasswordModal.jsx')).default;
  CustomerHomePage = (await server.ssrLoadModule('/src/pages/customer/CustomerHomePage.jsx')).default;
  MaintenanceDashboardPage = (await server.ssrLoadModule('/src/pages/maintenance/MaintenanceDashboardPage.jsx')).default;
});

const seedSession = (role) => {
  sessionStorage.setItem('token', `test.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role })).toString('base64url')}.test`);
  sessionStorage.setItem('user', JSON.stringify({ username: 'local-test', roles: [role] }));
};

test('failed customer requests show unavailable history and vehicles without claiming there are no bookings', async () => {
  seedSession('CUSTOMER');
  globalThis.fetch = async () => new Response(JSON.stringify({ message: 'Service unavailable' }), { status: 503, headers: { 'Content-Type': 'application/json' } });
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(CustomerHomePage, { onNavigate() {} }))));
    assert.match(document.body.textContent, /Booking history unavailable/);
    assert.match(document.body.textContent, /Vehicles unavailable/);
    assert.doesNotMatch(document.body.textContent, /No Booking History Found|0 Reservations/);
  } finally {
    await act(async () => root.unmount());
  }
});

test('failed maintenance overview hides healthy and empty claims, and refresh recovers real zero counts', async () => {
  seedSession('FLEET_MANAGER');
  let failed = true;
  globalThis.fetch = async url => new Response(JSON.stringify(String(url).includes('/maintenance/dashboard')
    ? failed ? { message: 'Service unavailable' } : { statusCounts: { totalVehicles: 0, available: 0, inUse: 0, undergoingMaintenance: 0, retired: 0 }, attentionItems: [], maintenanceVehicles: [] }
    : []), { status: String(url).includes('/maintenance/dashboard') && failed ? 503 : 200, headers: { 'Content-Type': 'application/json' } });
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(MaintenanceDashboardPage, { onNavigate() {} }))));
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 20)); });
    assert.match(document.body.textContent, /Attention queue unavailable/);
    assert.match(document.body.textContent, /Maintenance queue unavailable/);
    assert.doesNotMatch(document.body.textContent, /No vehicles flagged for attention|No Vehicles In Active Maintenance/);
    assert.match(document.body.textContent, /Unavailable/);
    failed = false;
    await act(async () => [...document.querySelectorAll('button')].find(button => button.textContent.trim() === 'Refresh').click());
    assert.match(document.body.textContent, /No vehicles flagged for attention/);
    assert.doesNotMatch(document.body.textContent, /Attention queue unavailable|Service unavailable/);
  } finally {
    await act(async () => root.unmount());
  }
});

after(async () => {
  await server?.close();
  dom?.window.close();
  for (const [key, descriptor] of globals) {
    if (descriptor) Object.defineProperty(globalThis, key, descriptor);
    else delete globalThis[key];
  }
});

test('password form waits for server confirmation, shows rejection and clears the session after success', async () => {
  const token = `test.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role: 'CUSTOMER' })).toString('base64url')}.test`;
  sessionStorage.setItem('token', token);
  sessionStorage.setItem('user', JSON.stringify({ username: 'local-test', roles: ['CUSTOMER'] }));
  const requests = [];
  let resolveRequest;
  globalThis.fetch = (url, options) => {
    requests.push({ url, options });
    return new Promise(resolve => { resolveRequest = resolve; });
  };
  const root = createRoot(document.getElementById('root'));
  const fill = (id, value) => {
    const input = document.getElementById(id);
    Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set.call(input, value);
    input.dispatchEvent(new window.Event('input', { bubbles: true }));
  };
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(ChangePasswordModal, { isOpen: true, onClose() {} }))));
    await act(async () => {
      fill('currentPassword', 'Previous@123');
      fill('newPassword', 'Updated@123');
      fill('confirmPassword', 'Updated@123');
    });
    await act(async () => document.querySelector('form').dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })));
    assert.equal(requests.length, 1);
    assert.match(requests[0].url, /\/api\/auth\/change-password$/);
    assert.equal(requests[0].options.headers.Authorization, `Bearer ${token}`);
    assert.deepEqual(JSON.parse(requests[0].options.body), { currentPassword: 'Previous@123', newPassword: 'Updated@123' });
    assert.doesNotMatch(document.body.textContent, /Password updated/);
    assert.equal(document.querySelector('[type="submit"]').disabled, true);
    await act(async () => resolveRequest(new Response(JSON.stringify({ message: 'Current password is incorrect.' }), { status: 400, headers: { 'Content-Type': 'application/json' } })));
    assert.match(document.body.textContent, /Current password is incorrect/);
    assert.doesNotMatch(document.body.textContent, /Password updated/);
    assert.equal(sessionStorage.getItem('token'), token);
    await act(async () => document.querySelector('form').dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })));
    await act(async () => resolveRequest(new Response(JSON.stringify({ message: 'Saved' }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    assert.match(document.body.textContent, /Password updated/);
    assert.equal(document.querySelector('form'), null);
    const signIn = [...document.querySelectorAll('button')].find(button => button.textContent === 'Sign in again');
    await act(async () => signIn.click());
    assert.equal(sessionStorage.getItem('token'), null);
    assert.equal(sessionStorage.getItem('user'), null);
  } finally {
    await act(async () => root.unmount());
  }
});
