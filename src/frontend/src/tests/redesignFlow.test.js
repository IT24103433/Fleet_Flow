import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM, VirtualConsole } from 'jsdom';
import { createServer } from 'vite';
import { parseRoute, routeHash, safeCustomerReturn } from '../utils/navigation.js';

let server, dom, createRoot, App, RecordPage, AuthProvider, VehicleCard, BookingModal, presentation, catalogueFilters, useConfirmAction, ManageFleet, UploadedImage;
const previous = new Map();
const errors = [];
const vehicleId = '11111111-1111-4111-8111-111111111111';
const accountId = '22222222-2222-4222-8222-222222222222';
const vehicle = { id: vehicleId, make: 'Toyota', model: 'QA Corolla', year: 2023, dailyRate: 10000, seatingCapacity: '5', transmission: 'Automatic', fuelType: 'Gasoline', categoryName: 'Sedan', status: 'Available', hubLocation: 'Colombo', images: [] };
const response = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const token = role => `test.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role })).toString('base64url')}.test`;
const seed = (role, extra = {}) => { sessionStorage.clear(); sessionStorage.setItem('token', token(role)); sessionStorage.setItem('user', JSON.stringify({ id: accountId, username: 'qa', roles: [role], ...extra })); };

before(async () => {
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', error => errors.push(error));
  dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: 'http://localhost', virtualConsole });
  dom.window.scrollTo = () => {};
  dom.window.matchMedia = () => ({ matches: false, addEventListener() {}, removeEventListener() {} });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  App = (await server.ssrLoadModule('/src/App.jsx')).default;
  RecordPage = (await server.ssrLoadModule('/src/components/common/RecordPage.jsx')).default;
  ({ AuthProvider } = await server.ssrLoadModule('/src/context/AuthContext.jsx'));
  VehicleCard = (await server.ssrLoadModule('/src/components/common/VehicleCard.jsx')).default;
  BookingModal = (await server.ssrLoadModule('/src/components/BookingModal.jsx')).default;
  useConfirmAction = (await server.ssrLoadModule('/src/components/common/useConfirmAction.jsx')).default;
  ManageFleet = (await server.ssrLoadModule('/src/pages/vehicle/ManageFleetPage.jsx')).default;
  UploadedImage = (await server.ssrLoadModule('/src/components/common/UploadedImage.jsx')).default;
  presentation = await server.ssrLoadModule('/src/utils/vehiclePresentation.js');
  ({ catalogueFilters } = await server.ssrLoadModule('/src/pages/BrowseFleetPage.jsx'));
});
after(async () => {
  await server?.close(); dom?.window.close();
  for (const [key, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key]; }
  assert.deepEqual(errors, [], 'DOM interactions must not throw');
});
const wait = async (condition, label) => {
  for (let n = 0; n < 80 && !condition(); n++) await act(async () => { await new Promise(resolve => setTimeout(resolve, 20)); });
  assert.ok(condition(), label);
};
const fill = (id, value) => { const input = document.getElementById(id); Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set.call(input, value); input.dispatchEvent(new window.Event('input', { bubbles: true })); };
const button = text => [...document.querySelectorAll('button')].find(item => item.textContent.trim() === text);

test('record routes and catalogue filters survive serialization without admitting external return URLs', () => {
  assert.deepEqual(parseRoute(routeHash('vehicle-details', { vehicleId })), { view: 'vehicle-details', params: { vehicleId } });
  assert.equal(parseRoute(routeHash('admin-edit-user', { userId: accountId })).params.userId, accountId);
  const search = routeHash('browse', { category: 'SUV', searchTerm: 'Toyota & Honda', page: '2' });
  assert.equal(parseRoute(search).params.searchTerm, 'Toyota & Honda');
  assert.equal(safeCustomerReturn(`#/vehicle-details?vehicleId=${vehicleId}`), `#/vehicle-details?vehicleId=${vehicleId}`);
  for (const unsafe of ['https://example.test/', '//example.test', '#/admin-users', '#/staff-profile', '#/vehicle-details?vehicleId=invalid']) assert.equal(safeCustomerReturn(unsafe), null);
  assert.equal(routeHash('login', { returnTo: 'https://example.test' }), '#/login');
});

test('catalogue normalizes invalid filters and resets invalid page and sort values', () => {
  assert.deepEqual(catalogueFilters({ fuel: 'unknown', transmission: 'unknown', sort: 'injected', page: '-2' }), { searchTerm: '', category: '', fuel: '', transmission: '', sort: 'createdAt:desc', page: '1' });
  assert.equal(catalogueFilters({ fuel: 'Hybrid', transmission: 'Automatic', page: '3' }).page, '3');
});

test('vehicle photographs resolve deterministically and failed images show a truthful fallback', async () => {
  assert.equal(presentation.vehiclePhoto(vehicle), null);
  const photographed = { ...vehicle, images: [{ id: 'later', relativeUrl: '/later.webp', createdAt: '2026-02-01' }, { id: 'first', relativeUrl: '/first.webp', createdAt: '2026-01-01' }] };
  assert.match(presentation.vehiclePhoto(photographed), /\/first.webp$/);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(VehicleCard, { vehicle: photographed, onSelect() {} })));
    assert.match(document.querySelector('img').src, /\/first.webp$/);
    await act(async () => document.querySelector('img').dispatchEvent(new window.Event('error')));
    assert.match(document.body.textContent, /Photo unavailable/);
    assert.equal(document.querySelector('img'), null);
  } finally { await act(async () => root.unmount()); }
});

test('catalogue cards load uploaded images when the list endpoint omits them', async () => {
  const requests = [];
  globalThis.fetch = async url => { requests.push(String(url)); return response([{ id: 'uploaded', relativeUrl: '/uploaded.webp' }]); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(VehicleCard, { vehicle, onSelect() {} })));
    await wait(() => document.querySelector('img'), 'Uploaded photograph must load');
    assert.match(document.querySelector('img').src, /\/uploaded.webp$/);
    assert.deepEqual(requests.map(url => new URL(url).pathname), [`/api/vehicles/${vehicleId}/images`]);
  } finally { await act(async () => root.unmount()); }
});

test('shared gallery image fallback recovers when a different photograph is selected', async () => {
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(UploadedImage, { src: '/missing.webp', alt: 'QA vehicle' })));
    await act(async () => document.querySelector('img').dispatchEvent(new window.Event('error')));
    assert.match(document.body.textContent, /Photo unavailable/);
    assert.equal(document.querySelector('[role="img"]').getAttribute('aria-label'), 'QA vehicle: photo unavailable');
    await act(async () => root.render(React.createElement(UploadedImage, { src: '/different.webp', alt: 'QA vehicle' })));
    assert.match(document.querySelector('img').src, /\/different.webp$/);
  } finally { await act(async () => root.unmount()); }
});

test('confirmation dialogs prevent duplicate actions and stay open while an action is pending', async () => {
  let finish, calls = 0;
  const pending = new Promise(resolve => { finish = resolve; });
  function Harness() {
    const { requestConfirmation, confirmationDialog } = useConfirmAction();
    return React.createElement(React.Fragment, null, React.createElement('button', { onClick: () => requestConfirmation({ title: 'Confirm QA action', action: async () => { calls++; await pending; } }) }, 'Open confirmation'), confirmationDialog);
  }
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(Harness)));
    await act(async () => button('Open confirmation').click());
    const confirm = button('Confirm change');
    await act(async () => { confirm.click(); confirm.click(); });
    assert.equal(calls, 1);
    await act(async () => window.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true })));
    assert.ok(document.querySelector('[role="dialog"]'), 'Pending action must keep the dialog open');
    await act(async () => finish());
    assert.equal(document.querySelector('[role="dialog"]'), null);
  } finally { finish(); await act(async () => root.unmount()); }
});

test('staff inventory keeps LKR prices and labels mobile record cells', async () => {
  seed('FLEET_MANAGER');
  globalThis.fetch = async url => response(String(url).includes('/api/vehicles') ? [vehicle] : []);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(ManageFleet, { onNavigate() {}, onSelectVehicle() {} }))));
    await wait(() => document.body.textContent.includes('QA Corolla'), 'Staff vehicle must load');
    assert.match(document.body.textContent, /LKR 10,000\/day/);
    assert.doesNotMatch(document.body.textContent, /\$10000/);
    assert.equal(document.querySelector('td[data-label="Pickup location"]')?.textContent.trim(), 'Colombo');
  } finally { await act(async () => root.unmount()); }
});

test('rental estimates round partial days and preserve missing prices as unavailable', () => {
  assert.deepEqual(presentation.bookingEstimate('2026-12-01T09:00', '2026-12-02T10:00', 10000), { days: 2, cost: 20000 });
  assert.deepEqual(presentation.bookingEstimate('2026-12-01T09:00', '2026-12-02T09:00', null), { days: 1, cost: null });
  assert.equal(presentation.bookingEstimate('invalid', 'invalid', 10000).days, 0);
});

test('admin record lookup after refresh shows missing records without fabricated accounts or actions', async () => {
  seed('ADMIN');
  const requests = [];
  globalThis.fetch = async (url, options) => { requests.push({ url, options }); return response([]); };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(RecordPage, { kind: 'user', id: accountId, onNavigate() {} }, record => React.createElement('p', null, record.username)))));
    assert.match(document.body.textContent, /Choose an account/);
    assert.doesNotMatch(document.body.textContent, /admin_sarah|Edit account|Delete/);
    assert.match(requests[0].url, /\/api\/admin\/users$/);
    assert.equal(requests[0].options.headers.Authorization, `Bearer ${sessionStorage.getItem('token')}`);
  } finally { await act(async () => root.unmount()); }
});

test('record loader ignores obsolete results when a different vehicle is opened', async () => {
  const pending = [];
  globalThis.fetch = url => new Promise(resolve => pending.push({ url, resolve }));
  const root = createRoot(document.getElementById('root'));
  const render = id => React.createElement(AuthProvider, null, React.createElement(RecordPage, { kind: 'vehicle', id, onNavigate() {} }, record => React.createElement('h1', null, record.model)));
  try {
    await act(async () => root.render(render(vehicleId)));
    await act(async () => root.render(render(accountId)));
    await act(async () => pending[1].resolve(response({ ...vehicle, id: accountId, model: 'Current vehicle' })));
    await act(async () => pending[0].resolve(response({ ...vehicle, model: 'Obsolete vehicle' })));
    assert.match(document.body.textContent, /Current vehicle/);
    assert.doesNotMatch(document.body.textContent, /Obsolete vehicle/);
  } finally { await act(async () => root.unmount()); }
});

test('booking prevents duplicate requests, handles availability and conflict failures, and displays returned status', async () => {
  seed('CUSTOMER');
  let resolveAvailability;
  let mode = 'pending';
  const requests = [];
  globalThis.fetch = (url, options) => {
    requests.push({ url, options });
    if (String(url).includes('/api/onboarding/config')) return Promise.resolve(response({ enabled: false }));
    if (String(url).includes('check-availability')) return mode === 'pending' ? new Promise(resolve => { resolveAvailability = resolve; }) : Promise.resolve(response({ isAvailable: true }));
    return Promise.resolve(mode === 'conflict' ? response({ message: 'The vehicle was just booked. Choose other dates.' }, 409) : response({ id: accountId, vehicleId, status: 'Pending', startDateTime: new Date(Date.now() + 86400000).toISOString(), endDateTime: new Date(Date.now() + 4 * 86400000).toISOString(), totalCost: 30000 }, 201));
  };
  const root = createRoot(document.getElementById('root'));
  const submit = () => document.querySelector('form').dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true }));
  try {
    await act(async () => root.render(React.createElement(BookingModal, { isOpen: true, onClose() {}, vehicle })));
    await act(async () => { submit(); submit(); });
    assert.equal(requests.filter(request => String(request.url).includes('check-availability')).length, 1, 'Repeated submit must issue one availability request');
    assert.equal(requests.filter(request => String(request.url).includes('/api/onboarding/config')).length, 1, 'Repeated submit must issue one verification configuration request');
    assert.equal(document.querySelector('[type="submit"]').disabled, true);
    await act(async () => resolveAvailability(response({ isAvailable: false })));
    assert.equal(requests.some(request => request.options.method === 'POST'), false);
    assert.match(document.body.textContent, /not available/);
    mode = 'conflict';
    await act(async () => submit());
    assert.match(document.body.textContent, /just booked/);
    assert.doesNotMatch(document.body.textContent, /Your booking is saved/);
    mode = 'success';
    await act(async () => submit());
    assert.match(document.body.textContent, /Your booking is saved/);
    assert.match(document.body.textContent, /Pending/);
    assert.doesNotMatch(document.body.textContent, /Booking Confirmed/);
    const payload = JSON.parse(requests.find(request => request.options.method === 'POST').options.body);
    assert.equal(payload.vehicleId, vehicleId);
    assert.match(payload.startDateTime, /Z$/);
  } finally { await act(async () => root.unmount()); }
});

test('direct vehicle links reload and customer sign-in returns to the selected vehicle', async () => {
  sessionStorage.clear();
  window.history.replaceState(null, '', `#/vehicle-details?vehicleId=${vehicleId}`);
  globalThis.fetch = async url => String(url).includes('/api/auth/login') ? response({ token: token('CUSTOMER'), user: { id: accountId, username: 'qa', roles: ['CUSTOMER'] } }) : String(url).includes(`/api/vehicles/${vehicleId}`) ? response(vehicle) : response([]);
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(App)));
    await wait(() => button('Sign in to book'), 'Direct vehicle route must retrieve the actual record');
    await act(async () => button('Sign in to book').click());
    assert.equal(parseRoute(window.location.hash).view, 'login');
    assert.equal(parseRoute(window.location.hash).params.returnTo, `#/vehicle-details?vehicleId=${vehicleId}`);
    await wait(() => document.getElementById('usernameOrEmail'), 'Sign-in route must finish loading before typing');
    await act(async () => { fill('usernameOrEmail', 'qa'); fill('password', 'Example@123'); });
    await act(async () => document.querySelector('form').dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })));
    await wait(() => button('Choose dates & book'), 'Sign-in must resume the same vehicle');
    assert.equal(parseRoute(window.location.hash).params.vehicleId, vehicleId);
  } finally { await act(async () => root.unmount()); }
});

test('catalogue filters remain selected after opening a vehicle and browser navigation', async () => {
  seed('CUSTOMER');
  window.history.replaceState(null, '', '#/browse?fuel=Gasoline');
  globalThis.fetch = async url => {
    const pathname = new URL(String(url)).pathname;
    if (pathname === `/api/vehicles/${vehicleId}`) return response(vehicle);
    if (pathname === '/api/vehicles') return response([vehicle]);
    if (pathname.includes('unread-count')) return response({ count: 0 });
    return response([]);
  };
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(App)));
    await wait(() => document.querySelector('.vehicle-card button'), 'Catalogue must load');
    await act(async () => fill('catalogue-search', 'Toyota'));
    assert.equal(parseRoute(window.location.hash).params.searchTerm, 'Toyota');
    await wait(() => document.querySelector('.vehicle-card button'), 'Filtered catalogue must load');
    await act(async () => document.querySelector('.vehicle-card button').click());
    await wait(() => button('Back to vehicles'), 'Vehicle record must load');
    await act(async () => button('Back to vehicles').click());
    await wait(() => document.getElementById('catalogue-search'), 'Catalogue must return');
    assert.equal(document.getElementById('catalogue-search').value, 'Toyota');
    assert.equal(document.getElementById('catalogue-fuel').value, 'Gasoline');
    await act(async () => {
      window.history.replaceState(null, '', '#/browse?searchTerm=Honda&fuel=Diesel');
      window.dispatchEvent(new window.PopStateEvent('popstate'));
    });
    assert.equal(document.getElementById('catalogue-search').value, 'Honda');
    assert.equal(document.getElementById('catalogue-fuel').value, 'Diesel');
  } finally { await act(async () => root.unmount()); }
});

test('customer and forced-password sessions cannot open admin record actions', async () => {
  for (const extra of [{}, { mustChangePassword: true }]) {
    seed('CUSTOMER', extra);
    window.history.replaceState(null, '', `#/admin-user-details?userId=${accountId}`);
    const adminRequests = [];
    globalThis.fetch = async url => { if (String(url).includes('/api/admin/users')) adminRequests.push(url); return response([]); };
    const root = createRoot(document.getElementById('root'));
    try {
      await act(async () => root.render(React.createElement(App)));
      await wait(() => parseRoute(window.location.hash).view !== 'admin-user-details', 'Role gate must redirect');
      assert.equal(adminRequests.length, 0);
      assert.doesNotMatch(document.body.textContent, /Delete User|admin_sarah/);
      assert.equal(parseRoute(window.location.hash).view, extra.mustChangePassword ? 'force-password-change' : 'customer-home');
    } finally { await act(async () => root.unmount()); }
  }
});
