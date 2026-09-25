import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
  SUPPORTED_VEHICLE_STATUSES,
  isValidVehicleStatus,
  getAvailableStatusTransitionsForRoles,
  canUserChangeStatus,
  validateStatusChange,
} from '../validation/vehicleStatusValidation.js';

describe('Vehicle Status Controls Client Validation & Role Rules', () => {
  test('all four standard statuses are defined and supported', () => {
    assert.equal(SUPPORTED_VEHICLE_STATUSES.length, 4);
    assert.ok(isValidVehicleStatus('Available'));
    assert.ok(isValidVehicleStatus('InUse'));
    assert.ok(isValidVehicleStatus('Maintenance'));
    assert.ok(isValidVehicleStatus('Retired'));
    assert.ok(!isValidVehicleStatus('Broken'));
    assert.ok(!isValidVehicleStatus(''));
  });

  test('FLEET_MANAGER role receives all 4 operational statuses', () => {
    const options = getAvailableStatusTransitionsForRoles(['FLEET_MANAGER']);
    assert.equal(options.length, 4);
    const values = options.map((o) => o.value);
    assert.deepEqual(values, ['Available', 'InUse', 'Maintenance', 'Retired']);
    assert.equal(canUserChangeStatus(['FLEET_MANAGER']), true);
  });

  test('ADMIN role receives all 4 operational statuses', () => {
    const options = getAvailableStatusTransitionsForRoles(['ADMIN']);
    assert.equal(options.length, 4);
    assert.equal(canUserChangeStatus(['ADMIN']), true);
  });

  test('MAINTENANCE_STAFF role is restricted to operational health statuses (Available, Maintenance)', () => {
    const options = getAvailableStatusTransitionsForRoles(['MAINTENANCE_STAFF']);
    assert.equal(options.length, 2);
    const values = options.map((o) => o.value);
    assert.deepEqual(values, ['Available', 'Maintenance']);
    assert.equal(canUserChangeStatus(['MAINTENANCE_STAFF']), true);
  });

  test('CUSTOMER role is denied status transitions', () => {
    const options = getAvailableStatusTransitionsForRoles(['CUSTOMER']);
    assert.equal(options.length, 0);
    assert.equal(canUserChangeStatus(['CUSTOMER']), false);
  });

  test('validateStatusChange succeeds for valid manager transition', () => {
    const res = validateStatusChange('Maintenance', 'Available', ['FLEET_MANAGER']);
    assert.equal(res.isValid, true);
    assert.equal(res.error, null);
  });

  test('validateStatusChange succeeds for maintenance staff operational health transitions', () => {
    const toMaint = validateStatusChange('Maintenance', 'Available', ['MAINTENANCE_STAFF']);
    assert.equal(toMaint.isValid, true);

    const toAvail = validateStatusChange('Available', 'Maintenance', ['MAINTENANCE_STAFF']);
    assert.equal(toAvail.isValid, true);
  });

  test('validateStatusChange rejects maintenance staff attempting to set InUse or Retired', () => {
    const resRetired = validateStatusChange('Retired', 'Available', ['MAINTENANCE_STAFF']);
    assert.equal(resRetired.isValid, false);
    assert.match(resRetired.error, /Maintenance staff can only update operational health statuses/);

    const resInUse = validateStatusChange('InUse', 'Maintenance', ['MAINTENANCE_STAFF']);
    assert.equal(resInUse.isValid, false);
    assert.match(resInUse.error, /Maintenance staff can only update operational health statuses/);
  });

  test('validateStatusChange rejects customer role', () => {
    const res = validateStatusChange('Maintenance', 'Available', ['CUSTOMER']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /do not have permission/);
  });

  test('validateStatusChange rejects unchanged status (same as current)', () => {
    const res = validateStatusChange('Available', 'Available', ['FLEET_MANAGER']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /already in "Available" status/);
  });

  test('validateStatusChange rejects invalid status value', () => {
    const res = validateStatusChange('JunkStatus', 'Available', ['FLEET_MANAGER']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /Invalid status "JunkStatus"/);
  });
});
