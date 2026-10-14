import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';
import { normalizeValidationErrors } from '../utils/validationErrorUtils.js';

let server;
let statusBadge;
let notifications;
let maintenanceReports;
let staffDashboard;
let inputField;

before(async () => {
  server = await createServer({ server: { middlewareMode: true, hmr: { port: 0 } }, appType: 'custom' });
  statusBadge = await server.ssrLoadModule('/src/components/common/StatusBadge.jsx');
  notifications = await server.ssrLoadModule('/src/pages/NotificationsPage.jsx');
  maintenanceReports = await server.ssrLoadModule('/src/pages/reports/MaintenanceReportsPage.jsx');
  staffDashboard = await server.ssrLoadModule('/src/pages/StaffDashboardPage.jsx');
  inputField = await server.ssrLoadModule('/src/components/InputField.jsx');
});

after(async () => { await server?.close(); });

test('staff overview distinguishes zero vehicles, loading, errors and unavailable counts', () => {
  const render = props => renderToStaticMarkup(React.createElement(staffDashboard.FleetSnapshot, { onRefresh() {}, ...props }));
  const empty = render({ data: { statusCounts: { totalVehicles: 0, available: 0, inUse: 0, undergoingMaintenance: 0 } } });
  assert.match(empty, /No vehicles have been added yet/);
  assert.doesNotMatch(empty, /Unavailable|Phase 2/);
  const loading = render({ loading: true, data: { statusCounts: { totalVehicles: 99 } } });
  assert.match(loading, /Loading fleet overview/);
  assert.doesNotMatch(loading, />99</);
  const failed = render({ error: 'Could not load fleet', data: { statusCounts: { totalVehicles: 99 } } });
  assert.match(failed, /Could not load fleet/);
  assert.doesNotMatch(failed, />99</);
  assert.match(render({ data: { statusCounts: {} } }), /Unavailable/);
});

test('shared inputs expose required fields and allow registration password autofill', () => {
  const html = renderToStaticMarkup(React.createElement(inputField.default, { id: 'password', name: 'password', type: 'password', label: 'Password', required: true, autoComplete: 'new-password' }));
  assert.match(html, /aria-required="true"/);
  assert.match(html, /autoComplete="new-password"/);
});

test('maintenance status badges display friendly scheduled and in-progress labels', () => {
  const scheduled = renderToStaticMarkup(React.createElement(statusBadge.default, { status: 'SCHEDULED' }));
  const inProgress = renderToStaticMarkup(React.createElement(statusBadge.default, { status: 'IN_PROGRESS' }));

  assert.match(scheduled, />Scheduled</);
  assert.match(inProgress, />In Progress</);
  assert.doesNotMatch(inProgress, /IN_PROGRESS/);
});

test('ASP.NET validation dictionaries become readable field messages', () => {
  assert.deepEqual(normalizeValidationErrors({
    StartDateTime: ['A start date is required.'],
    'Request.ServiceInformation': ['Service information is required.', 'Keep it concise.'],
    Cost: 'Enter a valid cost.',
  }), {
    startDateTime: 'A start date is required.',
    serviceInformation: 'Service information is required. Keep it concise.',
    cost: 'Enter a valid cost.',
  });
  assert.deepEqual(normalizeValidationErrors(null), {});
});

test('an empty notification inbox is distinct from a failed request', () => {
  const emptyHtml = renderToStaticMarkup(React.createElement(notifications.NotificationInboxView, {
    items: [], loading: false, error: '', pendingIds: [], onMarkRead() {}, onRefresh() {},
  }));
  const errorHtml = renderToStaticMarkup(React.createElement(notifications.NotificationInboxView, {
    items: [], loading: false, error: 'Notification service unavailable', pendingIds: [], onMarkRead() {}, onRefresh() {},
  }));

  assert.match(emptyHtml, /You&#x27;re all caught up/);
  assert.doesNotMatch(emptyHtml, /role="alert"/);
  assert.match(errorHtml, /role="alert"/);
  assert.match(errorHtml, /Notification service unavailable/);
  assert.doesNotMatch(errorHtml, /You&#x27;re all caught up/);
});

test('notification refresh state exposes loading status and disables refresh', () => {
  const html = renderToStaticMarkup(React.createElement(notifications.NotificationInboxView, {
    items: [{ id: 'old', title: 'Old', message: 'Old item', isRead: false, createdAt: '2026-01-01T00:00:00Z' }],
    loading: true, error: '', pendingIds: [], onMarkRead() {}, onRefresh() {},
  }));

  assert.match(html, /role="status"/);
  assert.match(html, /Loading notifications/);
  assert.match(html, /<button[^>]*disabled=""[^>]*>Refresh/);
  assert.doesNotMatch(html, /Old item/);
});

test('persisted zero maintenance cost is displayed rather than marked unavailable', () => {
  const zeroCost = renderToStaticMarkup(React.createElement(maintenanceReports.MaintenanceCost, { cost: 0 }));
  const missingCost = renderToStaticMarkup(React.createElement(maintenanceReports.MaintenanceCost, { cost: null }));

  assert.equal(zeroCost, 'LKR 0');
  assert.equal(missingCost, 'Not recorded');
});
