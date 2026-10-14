import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import { fetchWithSession, isApiJson } from '../services/sessionFetch.js';

test('only a rejected request from the current session triggers sign-out', async () => {
  const dom = new JSDOM('', { url: 'http://localhost' });
  const previousWindow = globalThis.window, previousFetch = globalThis.fetch;
  globalThis.window = dom.window;
  let revoked = 0;
  dom.window.addEventListener('fleetflow:session-revoked', () => revoked++);
  try {
    dom.window.sessionStorage.setItem('token', 'current');
    globalThis.fetch = async () => new Response('{"message":"Sign in again"}', { status: 401 });
    const result = await fetchWithSession('/protected', { headers: { Authorization: 'Bearer current' } });
    assert.equal(revoked, 1);
    assert.equal((await result.json()).message, 'Sign in again');
    await fetchWithSession('/protected', { headers: { Authorization: 'Bearer old' } });
    await fetchWithSession('/login');
    assert.equal(revoked, 1);
  } finally { globalThis.window = previousWindow; globalThis.fetch = previousFetch; dom.window.close(); }
});

test('an Identity outage preserves the session and the retryable response', async () => {
  const dom = new JSDOM('', { url: 'http://localhost' });
  const previousWindow = globalThis.window, previousFetch = globalThis.fetch;
  globalThis.window = dom.window;
  let revoked = false;
  dom.window.addEventListener('fleetflow:session-revoked', () => { revoked = true; });
  try {
    dom.window.sessionStorage.setItem('token', 'current');
    globalThis.fetch = async () => new Response('{"code":"access_verification_unavailable"}', { status: 503 });
    const result = await fetchWithSession('/protected', { headers: new Headers({ Authorization: 'Bearer current' }) });
    assert.equal(result.status, 503); assert.equal(revoked, false);
    assert.equal(dom.window.sessionStorage.getItem('token'), 'current');
    assert.equal((await result.json()).code, 'access_verification_unavailable');
  } finally { globalThis.window = previousWindow; globalThis.fetch = previousFetch; dom.window.close(); }
});

test('a late unauthorized response cannot sign out a newly authenticated session', async () => {
  const dom = new JSDOM('', { url: 'http://localhost' });
  const previousWindow = globalThis.window, previousFetch = globalThis.fetch;
  globalThis.window = dom.window;
  let finish, revoked = false;
  dom.window.addEventListener('fleetflow:session-revoked', () => { revoked = true; });
  try {
    dom.window.sessionStorage.setItem('token', 'old');
    globalThis.fetch = () => new Promise(resolve => { finish = resolve; });
    const pending = fetchWithSession('/protected', { headers: { Authorization: 'Bearer old' } });
    dom.window.sessionStorage.setItem('token', 'new');
    finish(new Response(null, { status: 401 })); await pending;
    assert.equal(revoked, false);
    assert.equal(dom.window.sessionStorage.getItem('token'), 'new');
  } finally { globalThis.window = previousWindow; globalThis.fetch = previousFetch; dom.window.close(); }
});

test('API services recognise JSON problem responses without accepting HTML', () => {
  assert.equal(isApiJson('application/problem+json'), true);
  assert.equal(isApiJson('application/json; charset=utf-8'), true);
  assert.equal(isApiJson('text/html'), false);
  assert.equal(isApiJson(null), false);
});
