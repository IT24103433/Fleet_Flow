import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';
let dom, server, createRoot, AuthProvider, useAuth, ForcePage;
const previous = new Map();
const user = { id: '11111111-1111-4111-8111-111111111111', fullName: 'Actual account', roles: ['CUSTOMER'], mustChangePassword: true };
const token = version => `${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role: 'CUSTOMER', access_version: String(version) })).toString('base64url')}.signature`;
before(async () => {
  dom = new JSDOM('<html><body><div id="root"></div></body></html>', { url: 'http://localhost' });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  ({ AuthProvider, useAuth } = await server.ssrLoadModule('/src/context/AuthContext.jsx'));
  ForcePage = (await server.ssrLoadModule('/src/pages/security/ForcePasswordChangePage.jsx')).default;
});
after(async () => { await server?.close(); dom?.window.close(); for (const [key, value] of previous) { if (value) Object.defineProperty(globalThis, key, value); else delete globalThis[key]; } });
function Probe() { const auth = useAuth(); return React.createElement('output', { id: 'session-state' }, auth.isAuthenticated ? `${auth.token}:${auth.user.mustChangePassword}` : 'signed-out'); }
function seed() { sessionStorage.setItem('token', token(0)); sessionStorage.setItem('user', JSON.stringify(user)); }

test('forced password completion submits once and replaces the revoked token with the returned session', async () => {
  seed(); let finish, requests = 0, completed = 0;
  globalThis.fetch = async () => { requests++; return new Promise(resolve => { finish = () => resolve(new Response(JSON.stringify({ token: token(1), user: { ...user, mustChangePassword: false } }), { status: 200, headers: { 'Content-Type': 'application/json' } })); }); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(ForcePage, { onComplete() { completed++; } }), React.createElement(Probe))));
    await act(async () => {
      for (const id of ['newPassword', 'confirmPassword']) {
        const input = document.getElementById(id);
        Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set.call(input, 'NewPassword@123');
        input.dispatchEvent(new window.Event('input', { bubbles: true }));
      }
    });
    await act(async () => { const form = document.querySelector('form'); form.dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })); form.dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })); });
    assert.equal(requests, 1); assert.equal(completed, 0);
    await act(async () => finish());
    assert.equal(completed, 1); assert.equal(sessionStorage.getItem('token'), token(1));
    assert.equal(JSON.parse(sessionStorage.getItem('user')).mustChangePassword, false);
    assert.match(document.getElementById('session-state').textContent, /:false$/);
  } finally { await act(async () => root.unmount()); }
});

test('revoked-session events clear authentication and stored account data', async () => {
  seed(); const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(Probe))));
    assert.notEqual(document.getElementById('session-state').textContent, 'signed-out');
    await act(async () => window.dispatchEvent(new window.Event('fleetflow:session-revoked')));
    assert.equal(document.getElementById('session-state').textContent, 'signed-out');
    assert.equal(sessionStorage.getItem('token'), null); assert.equal(sessionStorage.getItem('user'), null);
  } finally { await act(async () => root.unmount()); }
});

test('a signed-out password-change route returns to sign-in instead of trapping the user', async () => {
  sessionStorage.clear(); window.location.hash = '#/force-password-change'; window.scrollTo = () => {};
  const App = (await server.ssrLoadModule('/src/App.jsx')).default;
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(App)));
    assert.equal(window.location.hash, '#/login');
    assert.doesNotMatch(document.body.textContent, /Set Password & Access Workspace/);
  } finally { await act(async () => root.unmount()); }
});
