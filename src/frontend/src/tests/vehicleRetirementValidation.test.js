import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
  canUserRetireVehicle,
  validateRetirement,
} from '../validation/vehicleStatusValidation.js';

describe('Vehicle Retirement Authorization and Validation Rules', () => {
  test('FLEET_MANAGER and ADMIN can retire vehicles', () => {
    assert.equal(canUserRetireVehicle(['FLEET_MANAGER']), true);
    assert.equal(canUserRetireVehicle(['ADMIN']), true);
    assert.equal(canUserRetireVehicle(['admin']), true);
    assert.equal(canUserRetireVehicle(['fleet_manager']), true);
    assert.equal(canUserRetireVehicle(['USER', 'FLEET_MANAGER']), true);
  });

  test('MAINTENANCE_STAFF and CUSTOMER cannot retire vehicles', () => {
    assert.equal(canUserRetireVehicle(['MAINTENANCE_STAFF']), false);
    assert.equal(canUserRetireVehicle(['CUSTOMER']), false);
    assert.equal(canUserRetireVehicle([]), false);
    assert.equal(canUserRetireVehicle(null), false);
  });

  test('validateRetirement succeeds for Available vehicle when requested by FLEET_MANAGER', () => {
    const vehicle = { id: 'v-1', make: 'Toyota', status: 'Available' };
    const res = validateRetirement(vehicle, ['FLEET_MANAGER']);
    assert.equal(res.isValid, true);
    assert.equal(res.error, null);
  });

  test('validateRetirement succeeds for Maintenance vehicle when requested by ADMIN', () => {
    const vehicle = { id: 'v-2', make: 'Ford', status: 'Maintenance' };
    const res = validateRetirement(vehicle, ['ADMIN']);
    assert.equal(res.isValid, true);
    assert.equal(res.error, null);
  });

  test('validateRetirement rejects when vehicle is InUse', () => {
    const vehicle = { id: 'v-3', make: 'Tesla', status: 'InUse' };
    const res = validateRetirement(vehicle, ['FLEET_MANAGER']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /Cannot retire a vehicle that is currently In Use/);
  });

  test('validateRetirement rejects when vehicle is already Retired', () => {
    const vehicle = { id: 'v-4', make: 'Honda', status: 'Retired' };
    const res = validateRetirement(vehicle, ['ADMIN']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /already retired/);
  });

  test('validateRetirement rejects when user lacks role permission', () => {
    const vehicle = { id: 'v-5', make: 'Chevrolet', status: 'Available' };
    const resStaff = validateRetirement(vehicle, ['MAINTENANCE_STAFF']);
    assert.equal(resStaff.isValid, false);
    assert.match(resStaff.error, /Only Fleet Managers and Administrators/);

    const resCustomer = validateRetirement(vehicle, ['CUSTOMER']);
    assert.equal(resCustomer.isValid, false);
    assert.match(resCustomer.error, /Only Fleet Managers and Administrators/);
  });

  test('validateRetirement rejects when vehicle record is missing', () => {
    const res = validateRetirement(null, ['ADMIN']);
    assert.equal(res.isValid, false);
    assert.match(res.error, /Vehicle record is required/);
  });
});
