import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import { validateEditVehicle } from '../validation/vehicleValidation.js';

describe('Vehicle Edit Client Validation', () => {
  const validVehicle = {
    vin: '1HGCR2F83HA000001',
    licensePlate: 'WP-CAB-1001',
    make: 'Honda',
    model: 'Accord',
    year: '2024',
    vehicleCategoryId: 'cat-guid-1234',
    dailyRate: '18500',
    transmission: 'Automatic',
    fuelType: 'Gasoline',
    seatingCapacity: '5 Passengers',
    hubLocation: 'Colombo Fort Hub',
    mileage: '12000',
  };

  test('passes validation with completely valid payload', () => {
    const errors = validateEditVehicle(validVehicle);
    assert.deepEqual(errors, {});
  });

  test('fails when VIN is missing or whitespace', () => {
    const errors = validateEditVehicle({ ...validVehicle, vin: '   ' });
    assert.match(errors.vin, /Vehicle Identification Number \(VIN\) is required/);
  });

  test('fails when VIN length is not exactly 17 characters', () => {
    const shortVinErrors = validateEditVehicle({ ...validVehicle, vin: '12345' });
    assert.match(shortVinErrors.vin, /VIN must be exactly 17 characters/);

    const longVinErrors = validateEditVehicle({ ...validVehicle, vin: '1HGCR2F83HA000001EXTRA' });
    assert.match(longVinErrors.vin, /VIN must be exactly 17 characters/);
  });

  test('fails when license plate is missing or exceeds 20 characters', () => {
    const missingErrors = validateEditVehicle({ ...validVehicle, licensePlate: '' });
    assert.match(missingErrors.licensePlate, /License plate number is required/);

    const longErrors = validateEditVehicle({ ...validVehicle, licensePlate: 'WP-PLATE-VERY-LONG-EXCEEDING-20' });
    assert.match(longErrors.licensePlate, /cannot exceed 20 characters/);
  });

  test('fails when make or model is missing or exceeds 50 characters', () => {
    const missingErrors = validateEditVehicle({ ...validVehicle, make: '', model: '' });
    assert.match(missingErrors.make, /Make is required/);
    assert.match(missingErrors.model, /Model name is required/);

    const longErrors = validateEditVehicle({
      ...validVehicle,
      make: 'A'.repeat(51),
      model: 'B'.repeat(51),
    });
    assert.match(longErrors.make, /cannot exceed 50 characters/);
    assert.match(longErrors.model, /cannot exceed 50 characters/);
  });

  test('fails when year is below 1900 or beyond max allowable year', () => {
    const oldYearErrors = validateEditVehicle({ ...validVehicle, year: '1899' });
    assert.ok(oldYearErrors.year);

    const futureYearErrors = validateEditVehicle({ ...validVehicle, year: '2200' });
    assert.ok(futureYearErrors.year);
  });

  test('fails when vehicle category is not selected', () => {
    const errors = validateEditVehicle({ ...validVehicle, vehicleCategoryId: '' });
    assert.match(errors.vehicleCategoryId, /Please select a vehicle category/);
  });

  test('fails when daily rate is zero or negative', () => {
    const zeroErrors = validateEditVehicle({ ...validVehicle, dailyRate: '0' });
    assert.match(zeroErrors.dailyRate, /greater than LKR 0/);

    const negativeErrors = validateEditVehicle({ ...validVehicle, dailyRate: '-50' });
    assert.match(negativeErrors.dailyRate, /greater than LKR 0/);
  });

  test('fails when mileage is negative', () => {
    const errors = validateEditVehicle({ ...validVehicle, mileage: '-10' });
    assert.match(errors.mileage, /Odometer mileage must be 0 or greater/);
  });

  test('fails when required operational options are empty', () => {
    const errors = validateEditVehicle({
      ...validVehicle,
      transmission: '',
      fuelType: '',
      seatingCapacity: '',
      hubLocation: '',
    });
    assert.ok(errors.transmission);
    assert.ok(errors.fuelType);
    assert.ok(errors.seatingCapacity);
    assert.ok(errors.hubLocation);
  });
});
