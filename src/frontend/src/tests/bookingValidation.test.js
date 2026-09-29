import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import { validateBookingDates, calculateRentalCost, validateBookingResult } from '../validation/bookingValidation.js';

describe('Booking Date & Pricing Validation', () => {
  test('passes validation with valid future start and end dates', () => {
    const futureStart = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
    const futureEnd = new Date(Date.now() + 72 * 60 * 60 * 1000).toISOString();

    const errors = validateBookingDates(futureStart, futureEnd);
    assert.deepEqual(errors, {});
  });

  test('fails when startDateTime is in the past', () => {
    const pastStart = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
    const futureEnd = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();

    const errors = validateBookingDates(pastStart, futureEnd);
    assert.match(errors.startDateTime, /must be in the future/);
  });

  test('fails when endDateTime is before or equal to startDateTime', () => {
    const futureStart = new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString();
    const earlierEnd = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();

    const errors = validateBookingDates(futureStart, earlierEnd);
    assert.match(errors.endDateTime, /must be strictly after/);
  });

  test('fails when start or end date is missing', () => {
    const errors = validateBookingDates('', '');
    assert.ok(errors.startDateTime);
    assert.ok(errors.endDateTime);
  });

  test('calculates correct rental duration in days and total cost', () => {
    const start = '2026-10-01T09:00:00.000Z';
    const end = '2026-10-04T09:00:00.000Z'; // 3 days
    const dailyRate = 150;

    const { totalDays, totalCost } = calculateRentalCost(start, end, dailyRate);
    assert.equal(totalDays, 3);
    assert.equal(totalCost, 450);
  });

  test('rounds partial day up to next full day duration', () => {
    const start = '2026-10-01T09:00:00.000Z';
    const end = '2026-10-02T13:00:00.000Z'; // 28 hours -> 2 days
    const dailyRate = 200;

    const { totalDays, totalCost } = calculateRentalCost(start, end, dailyRate);
    assert.equal(totalDays, 2);
    assert.equal(totalCost, 400);
  });

  test('validateBookingResult returns isValid false on failed API responses without trigger confirmation', () => {
    const failedResult409 = {
      success: false,
      status: 409,
      message: 'Vehicle is already booked for the selected date range.',
    };

    const validated409 = validateBookingResult(failedResult409);
    assert.equal(validated409.isValid, false);
    assert.equal(validated409.booking, null);
    assert.match(validated409.errorMessage, /already booked/);

    const failedResult400 = {
      success: false,
      status: 400,
      message: 'Start date and time must be in the future.',
    };

    const validated400 = validateBookingResult(failedResult400);
    assert.equal(validated400.isValid, false);
    assert.equal(validated400.booking, null);
  });

  test('validateBookingResult returns isValid true on successful API response with booking data', () => {
    const successResult = {
      success: true,
      status: 201,
      data: {
        id: 'b1c2d3e4-f5a6-7b8c-9d0e-1f2a3b4c5d6e',
        customerId: 'c1c2c3c4-c5c6-7c8c-9c0c-1c2c3c4c5c6c',
        vehicleId: 'v1v2v3v4-v5v6-7v8v-9v0v-1v2v3v4v5v6v',
        startDateTime: '2026-10-10T09:00:00.000Z',
        endDateTime: '2026-10-13T09:00:00.000Z',
        status: 'Confirmed',
        totalCost: 600.00,
      },
    };

    const validated = validateBookingResult(successResult);
    assert.equal(validated.isValid, true);
    assert.ok(validated.booking);
    assert.equal(validated.booking.id, 'b1c2d3e4-f5a6-7b8c-9d0e-1f2a3b4c5d6e');
    assert.equal(validated.errorMessage, null);
  });
});
