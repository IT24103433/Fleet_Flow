import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
const [directory, project, compose] = process.argv.slice(2);
assert.match(project || '', /^fleetflow-s411-[a-f0-9]{12}$/);
assert.equal(path.resolve(compose), path.join(path.resolve(directory), 'compose.json'));
const bases = { identity: 'http://127.0.0.1:15101', fleet: 'http://127.0.0.1:15102', rental: 'http://127.0.0.1:15103', notification: 'http://127.0.0.1:15104' };
const configuration = JSON.parse(fs.readFileSync(compose));
const evidence = { environment: JSON.parse(fs.readFileSync(path.join(directory, 'environment.json'))), checks: [], performance: [] };
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
function control(...args) { execFileSync('docker', ['compose', '-p', project, '-f', compose, ...args], { stdio: 'pipe' }); }
async function call(service, route, { method = 'GET', token, body, key, expected = 200 } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (key) headers['Idempotency-Key'] = key;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(bases[service] + route, { method, headers, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(15000) });
  const text = await response.text();
  assert.equal(response.status, expected, `${service} ${method} ${route}: unexpected status ${response.status}`);
  return { data: /application\/(json|problem\+json)/i.test(response.headers.get('content-type') || '') && text ? JSON.parse(text) : text, headers: response.headers };
}
const read = async (service, route, token) => (await call(service, route, { token })).data;
const write = async (service, route, token, body, expected = 200, key) => (await call(service, route, { method: 'POST', token, body, expected, key })).data;
async function login(username, password = 'Password@123', channel = 'customer') { return write('identity', '/api/auth/login', null, { usernameOrEmail: username, password, loginChannel: channel }); }
async function until(action, predicate, label) {
  for (let i = 0; i < 50; i++) { try { const result = await action(); if (predicate(result)) return result; } catch { /* Bounded readiness/recovery retry. */ } await delay(300); }
  throw new Error(`Timed out: ${label}`);
}
async function check(name, action) { await action(); evidence.checks.push({ name, passed: true }); console.log(`PASS ${name}`); }
async function sample(name, count, concurrency, action) {
  const times = []; let next = 0, errors = 0;
  const started = performance.now();
  await Promise.all(Array.from({ length: concurrency }, async () => {
    while (next < count) { const index = next++; const start = performance.now(); try { await action(index); } catch { errors++; } times.push(performance.now() - start); }
  }));
  const elapsed = performance.now() - started; times.sort((a, b) => a - b);
  const percentile = fraction => Math.round(times[Math.ceil(fraction * times.length) - 1] * 100) / 100;
  evidence.performance.push({ name, requests: count, concurrency, errors, p50Ms: percentile(.5), p95Ms: percentile(.95), maxMs: Math.round(times.at(-1) * 100) / 100, elapsedMs: Math.round(elapsed), requestsPerSecond: Math.round(count / elapsed * 100000) / 100 });
  assert.equal(errors, 0, `${name}: request failures`);
}
try {
  await check('Four independently running APIs expose readiness and OpenAPI', async () => {
    for (const service of Object.keys(bases)) { await read(service, '/health/ready'); assert.ok((await read(service, '/openapi/v1.json')).paths); }
  });
  const admin = await login('admin', 'Password@123', 'staff');
  const manager = await login('manager', 'Password@123', 'staff');
  let customer = await login('customer');
  const categories = await read('fleet', '/api/vehicle-categories');
  const category = categories.find(item => /sedan/i.test(item.name)) || categories[0]; assert.ok(category);
  const vehicle = await write('fleet', '/api/vehicles', admin.token, { vin: 'S411QA00000000001', licensePlate: 'QA-S411-01', make: 'Toyota', model: 'Local integration demonstration', year: 2019, vehicleCategoryId: category.id, dailyRate: 9000, transmission: 'Automatic', fuelType: 'Hybrid', seatingCapacity: '5', hubLocation: 'Disposable integration lab', mileage: 15000 }, 201);
  const starts = Date.now() + 8000, ends = starts + 2 * 86400000;
  const body = { vehicleId: vehicle.id, startDateTime: new Date(starts).toISOString(), endDateTime: new Date(ends).toISOString() };
  const key = randomUUID();
  const booking = await write('rental', '/api/bookings', customer.token, body, 201, key);
  await check('Booking is durable, LKR estimate is correct, duplicates and overlaps are guarded', async () => {
    assert.equal(booking.status, 'Confirmed'); assert.equal(booking.totalCost, 18000);
    assert.equal((await write('rental', '/api/bookings', customer.token, body, 201, key)).id, booking.id);
    await write('rental', '/api/bookings', customer.token, body, 409, randomUUID());
    assert.equal((await read('fleet', `/api/bookings/${booking.id}`, customer.token)).id, booking.id);
  });
  const pickup = { odometer: 15000, fuelPercentage: 90, condition: 'Checked and ready for this local demonstration.' };
  await check('Pickup roles and booked-time rules are enforced', async () => {
    await write('rental', `/api/rentals/bookings/${booking.id}/pickup`, customer.token, pickup, 403);
    // Create a separate later reservation to make the early-pickup check independent of cold-start duration.
    const early = await write('rental', '/api/bookings', customer.token, { ...body, startDateTime: new Date(ends + 86400000).toISOString(), endDateTime: new Date(ends + 2 * 86400000).toISOString() }, 201, randomUUID());
    await write('rental', `/api/rentals/bookings/${early.id}/pickup`, admin.token, pickup, 400);
    await write('rental', `/api/bookings/${early.id}/cancel`, customer.token);
  });
  await delay(Math.max(0, starts - Date.now() + 100));
  const rental = await write('rental', `/api/rentals/bookings/${booking.id}/pickup`, manager.token, pickup);
  await check('Active rental prevents cancellation and return preserves estimate and timeline', async () => {
    assert.equal(rental.state, 'Active'); await write('rental', `/api/bookings/${booking.id}/cancel`, customer.token, undefined, 409);
    await write('rental', `/api/rentals/${rental.id}/complete`, manager.token, { odometer: 14999, fuelPercentage: 70, condition: 'Returned safely.', extraCharges: 0 }, 400);
    const returned = await write('rental', `/api/rentals/${rental.id}/complete`, manager.token, { odometer: 15040, fuelPercentage: 70, condition: 'Returned safely.', extraCharges: 500, extraChargeReason: 'Illustrative staff-entered cleaning charge.' });
    assert.equal(returned.state, 'Completed'); assert.equal(returned.return.finalAmount, 18500); assert.equal(returned.return.isLate, false);
    const final = await read('rental', `/api/bookings/${booking.id}`, customer.token); assert.equal(final.totalCost, 18000); assert.equal(final.finalRentalAmount, 18500);
    assert.equal((await read('rental', `/api/bookings/${booking.id}/timeline`, customer.token)).items.at(-1).kind, 'Completed');
    assert.equal((await read('fleet', `/api/vehicles/${vehicle.id}`)).mileage, 15040);
  });
  const guest = await write('identity', '/api/admin/users', admin.token, { username: 's411-guest', email: 's411@example.test', fullName: 'Local test customer', phoneNumber: '0770000000', address: 'Disposable lab', drivingLicenseNumber: 'QA-S411', initialPassword: 'GuestPassword@123', role: 'CUSTOMER' }, 201);
  const foreign = await login(guest.username, 'GuestPassword@123');
  await check('Customer ownership, queue access and report permissions remain restricted', async () => {
    await call('rental', `/api/bookings/${booking.id}`, { token: foreign.token, expected: 404 });
    await call('rental', `/api/bookings/${booking.id}/timeline`, { token: foreign.token, expected: 404 });
    await call('rental', '/api/rentals/queue', { token: customer.token, expected: 403 });
    await call('fleet', '/api/reports/bookings/export', { token: customer.token, expected: 403 });
    assert.ok((await read('rental', '/api/rentals/queue?state=Completed', admin.token)).items.some(item => item.booking.id === booking.id));
  });
  const plannedStart = ends + 2 * 86400000, plannedEnd = plannedStart + 7200000;
  const maintenance = await write('fleet', '/api/maintenance/records', admin.token, { vehicleId: vehicle.id, scheduledDateTime: new Date(plannedStart).toISOString(), plannedEndDateTime: new Date(plannedEnd).toISOString(), serviceInformation: 'Inspect brakes and fluids', details: 'Local demonstration work order', cost: 2500 }, 201);
  await check('Planned maintenance, staff assignment and immutable activity are integrated', async () => {
    const staff = await read('fleet', '/api/maintenance/staff', admin.token); assert.ok(staff.length);
    await call('fleet', `/api/maintenance/records/${maintenance.id}/assignment`, { method: 'PUT', token: customer.token, body: { assignedStaffId: staff[0].id }, expected: 403 });
    const assigned = (await call('fleet', `/api/maintenance/records/${maintenance.id}/assignment`, { method: 'PUT', token: admin.token, body: { assignedStaffId: staff[0].id } })).data;
    assert.equal(assigned.assignedStaffId, staff[0].id); assert.equal(assigned.assignedStaffName, staff[0].fullName || staff[0].username);
    await call('fleet', `/api/maintenance/records/${maintenance.id}/schedule`, { method: 'PUT', token: admin.token, body: { scheduledDateTime: new Date(plannedStart).toISOString(), plannedEndDateTime: new Date(plannedEnd + 3600000).toISOString() } });
    const audit = await read('fleet', `/api/maintenance/records/${maintenance.id}/audit`, admin.token); assert.equal(audit.length, 3); assert.ok(audit.every(item => item.actorId === admin.user.id));
    await write('rental', '/api/bookings', customer.token, { ...body, startDateTime: new Date(plannedStart).toISOString(), endDateTime: new Date(plannedEnd).toISOString() }, 409, randomUUID());
    const adjacent = await write('rental', '/api/bookings', customer.token, { ...body, startDateTime: new Date(plannedEnd + 3600000).toISOString(), endDateTime: new Date(plannedEnd + 7200000).toISOString() }, 201, randomUUID());
    await write('rental', `/api/bookings/${adjacent.id}/cancel`, customer.token);
    assert.match((await read('fleet', '/api/reports/maintenance/export', admin.token)), /Inspect brakes/);
    assert.match((await read('fleet', '/api/reports/bookings/export', admin.token)), /Completed/);
    await read('fleet', '/api/maintenance/dashboard', manager.token);
  });
  await until(() => read('rental', '/api/notifications/outbox', admin.token), rows => rows.every(row => row.status === 'Delivered'), 'initial durable outbox delivery');
  await check('Notification outage preserves successful bookings and recovers durable delivery', async () => {
    const known = new Set((await read('rental', '/api/notifications/outbox', admin.token)).map(row => row.id));
    control('stop', 'notification-service');
    const later = await write('rental', '/api/bookings', customer.token, { ...body, startDateTime: new Date(plannedEnd + 10 * 86400000).toISOString(), endDateTime: new Date(plannedEnd + 11 * 86400000).toISOString() }, 201, randomUUID());
    const pending = (await read('rental', '/api/notifications/outbox', admin.token)).find(row => !known.has(row.id)); assert.ok(pending); assert.notEqual(pending.status, 'Delivered'); assert.equal(later.status, 'Confirmed');
    control('start', 'notification-service'); await until(() => read('notification', '/health/ready'), () => true, 'Notification restart');
    await until(() => read('rental', '/api/notifications/outbox', admin.token), rows => rows.find(row => row.id === pending.id)?.status === 'Delivered', 'durable outbox retry');
  });
  await check('Duplicate notification events are deduplicated and read state is maintained', async () => {
    const internal = await write('identity', '/internal/service-token', null, { clientId: 'rental-service', clientSecret: configuration.services['rental-service'].environment.SERVICE_AUTH_RENTAL_SECRET });
    const event = { eventType: 'BookingCreatedEvent', eventId: randomUUID(), occurredAt: new Date().toISOString(), bookingId: booking.id, customerId: customer.user.id, vehicleId: vehicle.id };
    assert.equal((await write('notification', '/internal/notifications/events', internal.accessToken, event)).processed, true);
    assert.equal((await write('notification', '/internal/notifications/events', internal.accessToken, event)).processed, false);
    const inbox = await read('notification', '/api/notifications', customer.token); assert.ok(inbox.length);
    const unread = await read('notification', '/api/notifications/unread-count', customer.token);
    await call('notification', `/api/notifications/${inbox.find(item => !item.isRead).id}/read`, { method: 'PATCH', token: customer.token, expected: 204 });
    assert.equal((await read('notification', '/api/notifications/unread-count', customer.token)).count, unread.count - 1);
    await call('notification', '/internal/notifications/events', { method: 'POST', token: customer.token, body: event, expected: 401 });
  });
  // Warm each endpoint before measuring. These are Development-container timings, not a production SLO.
  evidence.performanceDatasetBefore = { vehicles: (await read('fleet', '/api/vehicles')).length, customerBookings: (await read('rental', '/api/bookings', customer.token)).length, maintenanceRecords: 1 };
  await read('fleet', '/api/vehicles'); await read('rental', '/api/bookings', customer.token); await read('fleet', '/api/reports/operational-statistics', admin.token);
  await sample('public fleet reads', 100, 5, () => read('fleet', '/api/vehicles'));
  await sample('protected Rental booking reads (Identity + Fleet calls)', 50, 5, () => read('rental', '/api/bookings', customer.token));
  await sample('protected Fleet report composition (Identity + Rental calls)', 30, 3, () => read('fleet', '/api/reports/operational-statistics', admin.token));
  await sample('durable bookings, distinct non-overlapping windows', 10, 2, index => write('rental', '/api/bookings', customer.token, { ...body, startDateTime: new Date(plannedEnd + (20 + index * 2) * 86400000).toISOString(), endDateTime: new Date(plannedEnd + (21 + index * 2) * 86400000).toISOString() }, 201, randomUUID()));
  await check('Password changes revoke every service and forced renewal returns a usable fresh session', async () => {
    const old = customer.token;
    await write('identity', '/api/auth/change-password', old, { currentPassword: 'Password@123', newPassword: 'ReviewPassword@123' });
    for (const [service, route] of [['identity','/api/auth/profile-picture'],['fleet','/api/notifications'],['rental','/api/bookings'],['notification','/api/notifications']]) await call(service, route, { token: old, expected: 401 });
    customer = await login('customer', 'ReviewPassword@123');
    await write('identity', `/api/admin/users/${customer.user.id}/reset-password`, admin.token, { customTemporaryPassword: 'TemporaryReview@123', forcePasswordChange: true });
    const forced = await login('customer', 'TemporaryReview@123');
    await call('rental', '/api/bookings', { token: forced.token, expected: 403 });
    const renewed = await write('identity', '/api/auth/change-password', forced.token, { newPassword: 'PermanentReview@123' }); assert.ok(renewed.token);
    customer = { ...renewed }; await read('rental', '/api/bookings', customer.token);
  });
  await check('Identity outage fails closed; restart preserves changed QA credentials', async () => {
    control('stop','identity-service');
    for (const [service, route] of [['fleet','/api/notifications'],['rental','/api/bookings'],['notification','/api/notifications']]) await call(service, route, { token: customer.token, expected: 503 });
    await read('fleet', '/api/vehicles');
    control('start','identity-service'); await until(() => read('identity', '/health/ready'), () => true, 'Identity restart');
    customer = await login('customer', 'PermanentReview@123'); await read('rental', '/api/bookings', customer.token);
  });
  evidence.success = true; evidence.completedAt = new Date().toISOString();
  evidence.dataset = { vehicles: (await read('fleet','/api/vehicles')).length, customerBookings: (await read('rental','/api/bookings',customer.token)).length, maintenanceRecords: 1, images: 0, photoFallback: 'Existing frontend regression coverage; no photograph invented.' };
} catch (error) { evidence.success = false; evidence.failure = error.message; throw error; }
finally { fs.writeFileSync(path.join(directory, 'result.json'), JSON.stringify(evidence, null, 2) + '\n'); }
