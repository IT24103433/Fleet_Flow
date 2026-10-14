import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM, VirtualConsole } from 'jsdom';
import { createServer } from 'vite';
import { createRentalClient } from '../services/rentalService.js';
import { pickupErrors, canStartPickup } from '../validation/pickupValidation.js';
import { isBookingCancellable } from '../validation/bookingValidation.js';
import { routeHash, parseRoute, safeCustomerReturn } from '../utils/navigation.js';
const id = '11111111-1111-4111-8111-111111111111';
const rentalId = '22222222-2222-4222-8222-222222222222';
test('rental routes retain identifiers and restrict sign-in returns to customer records', () => {
  assert.equal(parseRoute(routeHash('staff-rental-record', { bookingId: id })).params.bookingId, id);
  assert.equal(parseRoute(routeHash('rental-record', { rentalId })).params.rentalId, rentalId);
  assert.equal(safeCustomerReturn(`#/rental-record?rentalId=${rentalId}`), `#/rental-record?rentalId=${rentalId}`);
  assert.equal(safeCustomerReturn(`#/staff-rental-record?bookingId=${id}`), null);
  assert.equal(safeCustomerReturn('#/rental-record?rentalId=bad'), null);
});
test('pickup validates whole odometers, fuel percentages and meaningful condition notes', () => {
  assert.deepEqual(pickupErrors({ odometer: '1200', fuelPercentage: '60', condition: 'Bodywork checked' }, 1200), {});
  for (const value of ['', '-1', '1199', '1200.5', '2147483648']) assert.ok(pickupErrors({ odometer: value, fuelPercentage: '60', condition: 'Checked' }, 1200).odometer);
  for (const value of ['', '-1', '101', '2.5']) assert.ok(pickupErrors({ odometer: '1200', fuelPercentage: value, condition: 'Checked' }).fuelPercentage);
  assert.ok(pickupErrors({ odometer: '1200', fuelPercentage: '60', condition: '  ' }).condition);
});
test('pickup timing includes start, excludes return and cancellation stays blocked during handover', () => {
  const booking = { status: 'Confirmed', startDateTime: '2026-10-14T10:00:00Z', endDateTime: '2026-10-14T12:00:00Z' };
  assert.ok(canStartPickup(booking, Date.parse(booking.startDateTime)));
  assert.equal(canStartPickup(booking, Date.parse(booking.endDateTime)), false);
  assert.equal(canStartPickup({ ...booking, rentalId }, Date.parse(booking.startDateTime)), false);
  assert.equal(isBookingCancellable('Confirmed', 'Active'), false);
  assert.equal(isBookingCancellable('Confirmed', 'PickupPending'), false);
  assert.ok(isBookingCancellable('Confirmed'));
});
test('rental client uses configured ownership, authenticates writes and preserves conflict errors', async () => {
  const calls = [];
  const client = createRentalClient({ url: 'http://127.0.0.1:5003', getToken: () => 'test-token', fetchImpl: async (url, options) => { calls.push({ url, options }); return new Response(JSON.stringify({ message: 'Pickup already recorded.' }), { status: 409, headers: { 'Content-Type': 'application/json' } }); } });
  await assert.rejects(client.pickup(id, { odometer: 1200, fuelPercentage: 60, condition: 'Checked' }), e => e.status === 409 && e.message === 'Pickup already recorded.');
  assert.equal(calls[0].url, `http://127.0.0.1:5003/api/rentals/bookings/${id}/pickup`);
  assert.equal(calls[0].options.headers.Authorization, 'Bearer test-token');
  await assert.rejects(createRentalClient({ getToken: () => null }).queue('Active', 1), e => e.status === 401);
  await assert.rejects(createRentalClient({ getToken: () => 'token', fetchImpl: async () => { throw new Error('offline'); } }).pickup(id, {}), e => e.status === 503 && /before handing over/.test(e.message));
});
let server, dom, createRoot, Record, Queue;
const previous = new Map();
const domErrors = [];
const booking = { id, customerId: id, vehicleId: id, status: 'Confirmed', totalCost: 9000,
  startDateTime: new Date(Date.now() - 60000).toISOString(), endDateTime: new Date(Date.now() + 7200000).toISOString(),
  vehicle: { id, make: 'Toyota', model: 'Corolla QA', licensePlate: 'QA-01', hubLocation: 'Colombo', mileage: 1200, images: [] } };
const record = { id: rentalId, bookingId: id, state: 'Active', booking, pickupAt: new Date().toISOString(), pickupOdometer: 1200, pickupFuelPercentage: 60, pickupCondition: 'Bodywork checked', pickedUpByUserId: id };
before(async () => {
  const console = new VirtualConsole(); console.on('jsdomError', e => domErrors.push(e));
  dom = new JSDOM('<html><body><div id="root"></div></body></html>', { url: 'http://localhost', virtualConsole: console });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  sessionStorage.setItem('token', 'test-token');
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  Record = (await server.ssrLoadModule('/src/pages/rental/RentalRecordPage.jsx')).default;
  Queue = (await server.ssrLoadModule('/src/pages/rental/RentalQueuePage.jsx')).default;
});
after(async () => {
  await server?.close(); dom?.window.close();
  for (const [key, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key]; }
  assert.deepEqual(domErrors, []);
});
const json = data => new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } });
const wait = async condition => { for (let i = 0; i < 80 && !condition(); i++) await act(async () => { await new Promise(r => setTimeout(r, 15)); }); assert.ok(condition()); };
const button = text => [...document.querySelectorAll('button')].find(b => b.textContent.trim() === text);
function fill(id, value) { const el = document.getElementById(id); const proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, value); el.dispatchEvent(new window.Event('input', { bubbles: true })); }
test('record refresh resolves booking to saved rental and displays the honest photo fallback', async () => {
  const calls = []; globalThis.fetch = async url => { calls.push(url); return json(url.includes('/api/bookings/') ? { ...booking, rentalId, rentalState: 'Active' } : record); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Record, { bookingId: id, staff: true, onNavigate() {} })));
    await wait(() => document.body.textContent.includes('Pickup confirmed'));
    assert.ok(calls.some(url => url.endsWith(`/api/rentals/${rentalId}`)));
    assert.ok(document.body.textContent.includes('Vehicle photo unavailable'));
    assert.equal(button('Review pickup'), undefined);
    assert.ok(document.body.textContent.includes('1,200 km'));
  } finally { await act(async () => root.unmount()); }
});
test('pickup confirmation prevents duplicate submissions and only shows success after acknowledgement', async () => {
  let resolvePost; let posts = 0;
  globalThis.fetch = async (url, options) => options.method === 'POST' ? (posts++, new Promise(resolve => { resolvePost = () => resolve(json(record)); })) : json(booking);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Record, { bookingId: id, staff: true, onNavigate() {} })));
    await wait(() => Boolean(button('Review pickup')));
    await act(async () => fill('fuelPercentage', '60')); await act(async () => fill('condition', 'Bodywork checked'));
    await act(async () => document.querySelector('form').dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })));
    await wait(() => Boolean(button('Confirm pickup')));
    await act(async () => { button('Confirm pickup').click(); button('Processing...')?.click(); });
    assert.equal(posts, 1); assert.equal(document.body.textContent.includes('Pickup confirmed.'), false);
    await act(async () => resolvePost()); await wait(() => document.body.textContent.includes('Pickup confirmed.'));
    assert.equal(document.querySelector('[role=dialog]'), null);
  } finally { await act(async () => root.unmount()); }
});
test('customer record never exposes staff handover controls and missing records recover to bookings', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ message: 'Rental was not found.' }), { status: 404, headers: { 'Content-Type': 'application/json' } });
  let destination; const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Record, { rentalId, onNavigate: view => { destination = view; } })));
    await wait(() => document.body.textContent.includes('Record unavailable'));
    assert.equal(button('Review pickup'), undefined); await act(async () => button('My bookings').click()); assert.equal(destination, 'customer-home');
  } finally { await act(async () => root.unmount()); }
});
test('queue distinguishes an unavailable service from empty results and preserves booking navigation', async () => {
  globalThis.fetch = async () => json({ items: [{ booking, rental: null }], totalCount: 1, totalPages: 1 });
  let destination; const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Queue, { onNavigate: (view, params) => { destination = { view, params }; } })));
    await wait(() => Boolean(button('Open record'))); await act(async () => button('Open record').click());
    assert.deepEqual(destination, { view: 'staff-rental-record', params: { bookingId: id } });
    globalThis.fetch = async () => new Response(JSON.stringify({ message: 'Rental service unavailable.' }), { status: 503, headers: { 'Content-Type': 'application/json' } });
    await act(async () => button('Refresh queue').click()); await wait(() => document.body.textContent.includes('Rental queue unavailable'));
    assert.equal(document.body.textContent.includes('No rentals in this view.'), false);
  } finally { await act(async () => root.unmount()); }
});
