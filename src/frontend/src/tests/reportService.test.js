import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createReportClient } from '../services/reportService.js';
import { canViewReport, formatReportMetric } from '../utils/reportPermissions.js';

function clientFor(status, data) {
  const calls = [];
  const client = createReportClient({ baseUrl: 'https://fleet.test', getToken: () => 'staff-token', fetchImpl: async (url, options) => {
    calls.push({ url, options });
    return { ok: status >= 200 && status < 300, status, json: async () => data };
  } });
  return { client, calls };
}

test('fleet summary reads authenticated persisted-data endpoint without caching', async () => {
  const data = { vehicles: { total: 13, available: 7 }, currentBookingUtilizationPercent: 12.5 };
  const { client, calls } = clientFor(200, data);
  assert.deepEqual(await client.getFleetSummary(), data);
  assert.equal(calls[0].url, 'https://fleet.test/api/reports/fleet-summary');
  assert.equal(calls[0].options.headers.Authorization, 'Bearer staff-token');
  assert.equal(calls[0].options.cache, 'no-store');
});

test('booking report preserves cancelled status, references and paginated result', async () => {
  const data = { items: [{ bookingId: 'booking-id', customerId: 'customer-id', vehicleId: 'vehicle-id', status: 'Cancelled', totalCost: 125 }], totalCount: 1, page: 2, pageSize: 25, totalPages: 2 };
  const { client, calls } = clientFor(200, data);
  assert.deepEqual(await client.getBookingReport({ page: 2, status: 'Cancelled' }), data);
  assert.equal(calls[0].url, 'https://fleet.test/api/reports/bookings?page=2&pageSize=25&status=Cancelled');
});

test('empty booking report is preserved as an empty dataset', async () => {
  const data = { items: [], totalCount: 0, totalPages: 0 };
  const { client } = clientFor(200, data);
  assert.deepEqual(await client.getBookingReport(), data);
});

test('maintenance unavailable work orders remain unavailable rather than fabricated data', async () => {
  const data = { currentVehicles: [], workOrders: { available: false, unavailableReason: 'Not recorded yet', records: [] } };
  const { client, calls } = clientFor(200, data);
  assert.deepEqual(await client.getMaintenanceReport(), data);
  assert.equal(calls[0].url, 'https://fleet.test/api/reports/maintenance');
});

test('operational statistics retain nullable maintenance metrics', async () => {
  const data = { bookings: { total: 5 }, maintenance: { workOrderCount: null, recordedCostTotal: null } };
  const { client, calls } = clientFor(200, data);
  assert.deepEqual(await client.getOperationalStatistics(), data);
  assert.equal(calls[0].url, 'https://fleet.test/api/reports/operational-statistics');
});

test('anonymous reports fail before any HTTP request', async () => {
  let requested = false;
  const client = createReportClient({ getToken: () => null, fetchImpl: async () => { requested = true; } });
  await assert.rejects(client.getFleetSummary(), /Please log in/);
  assert.equal(requested, false);
});

for (const [status, message] of [[401, /session expired/], [403, /permission/], [503, /unavailable/]]) {
  test(`HTTP ${status} is an error rather than a zero-value report`, async () => {
    const { client } = clientFor(status);
    await assert.rejects(client.getOperationalStatistics(), message);
  });
}

test('refresh issues fresh requests and uses the latest session token', async () => {
  let version = 0;
  let token = 'first-admin';
  const calls = [];
  const client = createReportClient({ getToken: () => token, fetchImpl: async (_url, options) => {
    calls.push(options.headers.Authorization);
    return { ok: true, status: 200, json: async () => ({ vehicles: { total: ++version } }) };
  } });
  assert.equal((await client.getFleetSummary()).vehicles.total, 1);
  token = 'second-admin';
  assert.equal((await client.getFleetSummary()).vehicles.total, 2);
  assert.deepEqual(calls, ['Bearer first-admin', 'Bearer second-admin']);
});

test('unmount cancellation signals reach the report request', async () => {
  const { client, calls } = clientFor(200, {});
  const controller = new AbortController();
  await client.getMaintenanceReport(controller.signal);
  assert.equal(calls[0].options.signal, controller.signal);
});

for (const view of ['fleet-performance', 'booking-reports', 'maintenance-reports']) {
  for (const role of ['CUSTOMER', 'ADMIN', 'FLEET_MANAGER', 'MAINTENANCE_STAFF']) {
    test(`${role} reporting navigation permission for ${view}`, () => {
      const permitted = role === 'ADMIN' || view === 'maintenance-reports' && ['FLEET_MANAGER', 'MAINTENANCE_STAFF'].includes(role);
      assert.equal(canViewReport(view, [role]), permitted);
    });
  }
}

test('report permissions check all roles and handle missing/unknown roles', () => {
  assert.equal(canViewReport('booking-reports', ['CUSTOMER', 'admin']), true);
  assert.equal(canViewReport('fleet-performance', []), false);
  assert.equal(canViewReport('maintenance-reports', ['UNKNOWN']), false);
  assert.equal(canViewReport('unknown-view', ['ADMIN']), false);
});

test('metric formatting preserves actual zero and distinguishes unavailable data', () => {
  assert.equal(formatReportMetric(0), '0');
  assert.equal(formatReportMetric(12.5, '%'), '12.5%');
  for (const value of [null, undefined, NaN, Infinity]) assert.equal(formatReportMetric(value), 'Unavailable');
});
