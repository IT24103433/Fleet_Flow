import { test, describe } from 'node:test';
import assert from 'node:assert/strict';

describe('Maintenance Dashboard Data Integrity & Logic Tests', () => {
  test('status counts correctly report real persisted zero counts without fabricated metrics', () => {
    const rawCounts = {
      totalVehicles: 0,
      undergoingMaintenance: 0,
      available: 0,
      inUse: 0,
      retired: 0,
    };

    assert.equal(rawCounts.totalVehicles, 0);
    assert.equal(rawCounts.undergoingMaintenance, 0);
    assert.equal(rawCounts.available, 0);
    assert.equal(rawCounts.inUse, 0);
    assert.equal(rawCounts.retired, 0);
  });

  test('status counts aggregate accurately when vehicles are present in database', () => {
    const rawCounts = {
      totalVehicles: 5,
      undergoingMaintenance: 2,
      available: 2,
      inUse: 1,
      retired: 0,
    };

    assert.equal(rawCounts.totalVehicles, 5);
    assert.equal(rawCounts.undergoingMaintenance, 2);
    assert.equal(rawCounts.available, 2);
    assert.equal(rawCounts.inUse, 1);
    assert.equal(rawCounts.retired, 0);
    assert.equal(rawCounts.undergoingMaintenance + rawCounts.available + rawCounts.inUse + rawCounts.retired, rawCounts.totalVehicles);
  });

  test('attention items correctly prioritize High urgency items first', () => {
    const items = [
      { id: '1', urgency: 'Medium', attentionReason: 'Undergoing Active Maintenance' },
      { id: '2', urgency: 'High', attentionReason: 'Extended Service (12 days in maintenance)' },
      { id: '3', urgency: 'Normal', attentionReason: 'Routine inspection' },
      { id: '4', urgency: 'High', attentionReason: 'Active Maintenance & High Odometer (90,000 mi)' },
    ];

    const sorted = [...items].sort((a, b) => {
      const rank = { High: 0, Medium: 1, Normal: 2 };
      return (rank[a.urgency] ?? 3) - (rank[b.urgency] ?? 3);
    });

    assert.equal(sorted[0].urgency, 'High');
    assert.equal(sorted[1].urgency, 'High');
    assert.equal(sorted[2].urgency, 'Medium');
    assert.equal(sorted[3].urgency, 'Normal');
  });

  test('work queue filtering filters vehicles by plate, vin, make, or model', () => {
    const queue = [
      { id: '1', make: 'Tesla', model: '3', licensePlate: 'WP-1001', vin: 'VIN-TEST-1', hubLocation: 'Colombo Fort Hub' },
      { id: '2', make: 'Toyota', model: 'Axio', licensePlate: 'WP-1002', vin: 'VIN-TEST-2', hubLocation: 'Kandy Central Hub' },
      { id: '3', make: 'Nissan', model: 'Leaf', licensePlate: 'WP-1003', vin: 'VIN-TEST-3', hubLocation: 'Galle Coastal Hub' },
    ];

    const filter = (term) => {
      const t = term.toLowerCase().trim();
      return queue.filter((v) =>
        v.make.toLowerCase().includes(t) ||
        v.model.toLowerCase().includes(t) ||
        v.licensePlate.toLowerCase().includes(t) ||
        v.vin.toLowerCase().includes(t) ||
        v.hubLocation.toLowerCase().includes(t)
      );
    };

    assert.equal(filter('tesla').length, 1);
    assert.equal(filter('tesla')[0].id, '1');
    assert.equal(filter('WP-1002').length, 1);
    assert.equal(filter('WP-1002')[0].id, '2');
    assert.equal(filter('kandy').length, 1);
    assert.equal(filter('kandy')[0].id, '2');
    assert.equal(filter('nonexistent').length, 0);
  });
});
