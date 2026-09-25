import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import { validateBookingDates, calculateRentalCost } from '../validation/bookingValidation.js';

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
});
