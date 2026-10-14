import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';
import { validateMaintenanceWindow, validateMaintenanceRecord } from '../validation/maintenanceValidation.js';
import { auditChanges } from '../utils/maintenanceAudit.js';
test('maintenance windows require a future ordered end and preserve legacy detail editing', () => {
  const form = { vehicleId: 'record', scheduledDateTime: new Date(Date.now() + 3600000).toISOString(), plannedEndDateTime: new Date(Date.now() + 7200000).toISOString(), serviceInformation: 'Inspect brakes', cost: '500.50' };
  assert.deepEqual(validateMaintenanceRecord(form, { requireEnd: true }), {});
  assert.ok(validateMaintenanceWindow({ ...form, plannedEndDateTime: form.scheduledDateTime }).plannedEndDateTime);
  assert.ok(validateMaintenanceRecord({ ...form, plannedEndDateTime: '' }, { requireEnd: true }).plannedEndDateTime);
  assert.deepEqual(validateMaintenanceRecord({ vehicleId: 'record', serviceInformation: 'Historic details', cost: '0' }, { requireSchedule: false }), {});
  assert.ok(validateMaintenanceRecord({ ...form, cost: '0.001' }).cost);
});
test('audit presentation keeps saved before and after values without fabricating missing history', () => {
  const before = { AssignedStaffName: 'First technician', Cost: 500 };
  const after = { AssignedStaffName: 'Second technician', Cost: 500 };
  assert.deepEqual(auditChanges({ before: JSON.stringify(before), after: JSON.stringify(after) }), [{ label: 'Assigned staff', before: 'First technician', after: 'Second technician' }]);
  assert.deepEqual(auditChanges({ before: null, after: 'invalid' }), []);
  assert.deepEqual(auditChanges({ before: null, after: JSON.stringify({ AssignedStaffName: null, Cost: 0 }) }), [{ label: 'Cost (LKR)', before: 'Not recorded', after: 0 }]);
  assert.equal(before.AssignedStaffName, 'First technician');
});
let dom, server, createRoot, Actions;
const previous = new Map();
const id = '11111111-1111-4111-8111-111111111111';
const record = { id, status: 'SCHEDULED', vehicleLicensePlate: 'QA-M', serviceInformation: 'Inspect brakes', scheduledDateTime: new Date(Date.now() + 3600000).toISOString(), plannedEndDateTime: new Date(Date.now() + 7200000).toISOString() };
const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const button = text => [...document.querySelectorAll('button')].find(element => element.textContent.trim() === text);
before(async () => {
  dom = new JSDOM('<html><body><div id="root"></div></body></html>', { url: 'http://localhost' });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  Actions = (await server.ssrLoadModule('/src/components/maintenance/MaintenancePlanActions.jsx')).default;
});
after(async () => { await server?.close(); dom?.window.close(); for (const [key, value] of previous) { if (value) Object.defineProperty(globalThis, key, value); else delete globalThis[key]; } });
test('staff assignment is hidden without permission and historical activity has an honest empty state', async () => {
  globalThis.fetch = async () => json([]);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Actions, { record, token: 'test', canAssign: false })));
    assert.equal(button('Assign staff'), undefined);
    await act(async () => button('Activity').click());
    assert.match(document.body.textContent, /No audit entries were recorded/);
  } finally { await act(async () => root.unmount()); }
});
test('assignment sends an authenticated existing staff ID only once during confirmation', async () => {
  let posts = 0, finish, saved = 0, sent;
  globalThis.fetch = async (_url, options) => {
    if (options.method === 'GET') return json([{ id, fullName: 'Actual technician', username: 'tech' }]);
    posts++; sent = options; return new Promise(resolve => { finish = () => resolve(json({ ...record, assignedStaffId: id })); });
  };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Actions, { record, token: 'test', canAssign: true, onSaved() { saved++; } })));
    await act(async () => button('Assign staff').click());
    await act(async () => { const select = document.getElementById('assigned-maintenance-staff'); select.value = id; select.dispatchEvent(new window.Event('change', { bubbles: true })); });
    await act(async () => { const submit = button('Save assignment'); submit.click(); submit.click(); });
    assert.equal(posts, 1); assert.equal(saved, 0); assert.equal(sent.headers.Authorization, 'Bearer test'); assert.deepEqual(JSON.parse(sent.body), { assignedStaffId: id });
    await act(async () => finish()); assert.equal(saved, 1);
  } finally { await act(async () => root.unmount()); }
});
test('Identity directory failures disable assignment instead of offering fabricated staff', async () => {
  globalThis.fetch = async () => json({ message: 'Staff directory unavailable' }, 503);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Actions, { record, token: 'test', canAssign: true })));
    await act(async () => button('Assign staff').click());
    assert.match(document.body.textContent, /Staff directory unavailable/); assert.equal(button('Save assignment').disabled, true); assert.equal(document.getElementById('assigned-maintenance-staff'), null);
  } finally { await act(async () => root.unmount()); }
});
