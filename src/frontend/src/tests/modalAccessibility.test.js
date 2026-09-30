import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM, VirtualConsole } from 'jsdom';
import { createServer } from 'vite';

let server;
let Modal;
let dom;
let createRoot;
const domErrors = [];
const previousGlobals = new Map();
before(async () => {
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', error => domErrors.push(error));
  dom = new JSDOM('<!doctype html><html><body><button id="opener">Open</button><div id="root"></div></body></html>', { url: 'http://localhost', virtualConsole });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, IS_REACT_ACT_ENVIRONMENT: true })) {
    previousGlobals.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  // React DOM detects browser event support at import time; install the DOM first.
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  Modal = (await server.ssrLoadModule('/src/components/common/Modal.jsx')).default;
});
after(async () => {
  await server?.close();
  dom?.window.close();
  for (const [key, descriptor] of previousGlobals) {
    if (descriptor) Object.defineProperty(globalThis, key, descriptor);
    else delete globalThis[key];
  }
  assert.deepEqual(domErrors, [], 'DOM event handlers must not throw during the focus tests');
});

const flushFocus = () => act(async () => { await new Promise(resolve => setTimeout(resolve, 10)); });

test('modal prioritizes autofocus, preserves editing focus through rerenders and restores its opener', async () => {
  const opener = document.getElementById('opener');
  opener.focus();
  document.body.style.overflow = 'auto';
  const root = createRoot(document.getElementById('root'));
  const render = (isOpen, onClose, value = '') => React.createElement(Modal, { isOpen, onClose, title: 'Edit booking' },
    React.createElement('input', { id: 'first', 'data-autofocus': true, defaultValue: '' }),
    React.createElement('input', { id: 'second', value, onChange() {} }));
  try {
    await act(async () => root.render(render(true, () => {})));
    await flushFocus();
    assert.equal(document.activeElement.id, 'first');
    const dialog = document.querySelector('[role="dialog"]');
    assert.equal(document.getElementById(dialog.getAttribute('aria-labelledby')).textContent, 'Edit booking');
    document.getElementById('second').focus();
    let closed = 0;
    await act(async () => root.render(render(true, () => { closed++; }, 'typed text')));
    await flushFocus();
    assert.equal(document.activeElement.id, 'second');
    assert.equal(document.activeElement.value, 'typed text');
    window.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape' }));
    assert.equal(closed, 1, 'Escape must use the latest callback without rerunning initial focus');
    await act(async () => root.render(render(false, () => {})));
    assert.equal(document.activeElement, opener);
    assert.equal(document.body.style.overflow, 'auto');
    await act(async () => root.render(render(true, () => {})));
    await flushFocus();
    assert.equal(document.activeElement.id, 'first', 'Reopening should focus the intended input again');
  } finally {
    await act(async () => root.unmount());
  }
});

test('modal safely closes when the opener has been removed', async () => {
  const opener = document.createElement('button');
  document.body.append(opener);
  opener.focus();
  const root = createRoot(document.getElementById('root'));
  await act(async () => root.render(React.createElement(Modal, { isOpen: true, onClose() {}, title: 'Test' })));
  await flushFocus();
  opener.remove();
  await act(async () => root.unmount());
  assert.equal(document.querySelector('[role="dialog"]'), null);
});
