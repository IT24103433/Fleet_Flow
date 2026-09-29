import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

let server;
let components;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  components = await server.ssrLoadModule('/src/pages/reports/ReportComponents.jsx');
});
after(async () => { await server?.close(); });

test('summary cards render supplied data and explicit unavailable values', () => {
  const html = renderToStaticMarkup(React.createElement(components.ReportMetrics, { metrics: [
    { label: 'Total vehicles', value: '13' }, { label: 'Work-order costs', value: 'Unavailable' },
  ] }));
  assert.match(html, /13/);
  assert.match(html, /Unavailable/);
  assert.match(html, /Total vehicles/);
});

test('report table shows persisted cancelled status and booking reference', () => {
  const html = renderToStaticMarkup(React.createElement(components.ReportTable, {
    label: 'Bookings', rows: [{ id: 'persisted-reference', status: 'Cancelled' }], rowKey: 'id',
    columns: [{ label: 'Reference', render: row => row.id }, { label: 'Status', render: row => row.status }], emptyMessage: 'No records',
  }));
  assert.match(html, /persisted-reference/);
  assert.match(html, /Cancelled/);
  assert.match(html, /<caption>Bookings/);
});

test('empty results render an empty state without example table rows', () => {
  const html = renderToStaticMarkup(React.createElement(components.ReportTable, {
    label: 'Bookings', rows: [], rowKey: 'id', columns: [], emptyMessage: 'No bookings match this report.',
  }));
  assert.match(html, /No bookings match this report/);
  assert.doesNotMatch(html, /<table/);
});

test('loading reports hide previous metrics', () => {
  const html = renderToStaticMarkup(React.createElement(components.ReportShell, {
    title: 'Fleet report', report: { loading: true, refresh() {} },
  }, React.createElement('p', null, 'Previous metrics')));
  assert.match(html, /Loading report/);
  assert.doesNotMatch(html, /Previous metrics/);
});

test('unavailable reports render an error without fabricated summary cards', () => {
  const html = renderToStaticMarkup(React.createElement(components.ReportShell, {
    title: 'Fleet report', report: { loading: false, error: 'Reporting data is unavailable', refresh() {} },
  }, React.createElement('p', null, 'Fabricated totals')));
  assert.match(html, /role="alert"/);
  assert.match(html, /Reporting data is unavailable/);
  assert.doesNotMatch(html, /Fabricated totals/);
});
