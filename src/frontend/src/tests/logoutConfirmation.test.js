import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';
let dom, server, createRoot, AuthProvider, useAuth, CustomerNav, StaffHeader;
const previous = new Map();
const token = role => `${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now()/1000)+3600, role, access_version: '0' })).toString('base64url')}.test`;
const button = (name, scope = document) => [...scope.querySelectorAll('button')].find(item => item.textContent.trim().toLowerCase() === name.toLowerCase());
const dialog = () => document.querySelector('[role="dialog"]');
before(async () => {
  dom = new JSDOM('<html><body><div id="root"></div></body></html>', { url: 'http://localhost' });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  globalThis.fetch = async () => new Response('{"count":0}', { headers: { 'Content-Type': 'application/json' } });
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  ({ AuthProvider, useAuth } = await server.ssrLoadModule('/src/context/AuthContext.jsx'));
  CustomerNav = (await server.ssrLoadModule('/src/components/layout/CustomerNav.jsx')).default;
  StaffHeader = (await server.ssrLoadModule('/src/components/layout/StaffHeader.jsx')).default;
});
after(async () => { await server?.close(); dom?.window.close(); for (const [key, value] of previous) { if (value) Object.defineProperty(globalThis, key, value); else delete globalThis[key]; } });
function Probe() { const auth = useAuth(); return React.createElement('output', { id: 'auth-state' }, auth.isAuthenticated ? 'signed-in' : 'signed-out'); }
async function mount(role = 'CUSTOMER', forced = false) {
  sessionStorage.setItem('token', token(role)); sessionStorage.setItem('user', JSON.stringify({ username: 'review', roles: [role], mustChangePassword: forced }));
  const navigations = [], root = createRoot(document.getElementById('root'));
  const header = role === 'CUSTOMER' ? CustomerNav : StaffHeader;
  await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(header, { currentView: forced ? 'force-password-change' : 'customer-home', onNavigate(view) { navigations.push(view); } }), React.createElement(Probe))));
  return { root, navigations };
}
test('customer logout requires confirmation; Cancel and Escape preserve session and focus', async () => {
  const { root, navigations } = await mount();
  try {
    const trigger = button('Log Out'); trigger.focus();
    await act(async () => trigger.click()); await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
    assert.ok(dialog()); assert.equal(document.activeElement, button('Cancel', dialog()));
    assert.equal(document.getElementById('auth-state').textContent, 'signed-in'); assert.ok(sessionStorage.getItem('token'));
    await act(async () => button('Cancel', dialog()).click()); assert.equal(dialog(), null); assert.equal(document.activeElement, trigger);
    await act(async () => trigger.click());
    await act(async () => window.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true })));
    assert.equal(dialog(), null); assert.equal(document.getElementById('auth-state').textContent, 'signed-in'); assert.deepEqual(navigations, []);
    await act(async () => trigger.click()); await act(async () => button('Log out', dialog()).click());
    assert.equal(document.getElementById('auth-state').textContent, 'signed-out'); assert.equal(sessionStorage.getItem('token'), null); assert.equal(sessionStorage.getItem('user'), null);
  } finally { await act(async () => root.unmount()); }
});
test('staff and admin confirmation navigates only after choosing Log out', async () => {
  for (const role of ['ADMIN', 'FLEET_MANAGER', 'MAINTENANCE_STAFF']) {
    const { root, navigations } = await mount(role);
    try {
      await act(async () => button('Log out').click()); assert.deepEqual(navigations, []);
      await act(async () => button('Cancel', dialog()).click()); assert.ok(sessionStorage.getItem('token'));
      await act(async () => button('Log out').click()); await act(async () => { const confirm = button('Log out', dialog()); confirm.click(); confirm.click(); });
      assert.deepEqual(navigations, ['staff-login']); assert.equal(sessionStorage.getItem('token'), null);
    } finally { await act(async () => root.unmount()); }
  }
});
test('forced-password logout is also confirmable without clearing the session on cancellation', async () => {
  const { root } = await mount('CUSTOMER', true);
  try {
    await act(async () => button('Log Out').click()); await act(async () => button('Cancel', dialog()).click());
    assert.equal(JSON.parse(sessionStorage.getItem('user')).mustChangePassword, true);
    await act(async () => button('Log Out').click()); await act(async () => button('Log out', dialog()).click());
    assert.equal(document.getElementById('auth-state').textContent, 'signed-out');
  } finally { await act(async () => root.unmount()); }
});
test('security revocation bypasses confirmation and dismisses a pending logout dialog', async () => {
  const { root, navigations } = await mount('ADMIN');
  try {
    await act(async () => button('Log out').click()); assert.ok(dialog());
    await act(async () => window.dispatchEvent(new window.Event('fleetflow:session-revoked')));
    assert.equal(dialog(), null); assert.equal(sessionStorage.getItem('token'), null); assert.deepEqual(navigations, []);
    assert.equal(document.getElementById('auth-state').textContent, 'signed-out');
  } finally { await act(async () => root.unmount()); }
});
