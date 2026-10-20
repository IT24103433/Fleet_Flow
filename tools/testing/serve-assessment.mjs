// Real Identity + Fleet APIs and React UI for the controlled SE3112 assessment.
import { spawn } from 'node:child_process';
import { createWriteStream } from 'node:fs';
import { mkdir, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const required = name => { if (!process.env[name]) throw new Error(`${name} is required`); return process.env[name]; };
const password = required('ASE_TEST_PASSWORD');
const dbPassword = required('ASE_DB_PASSWORD');
const dbHost = process.env.ASE_DB_HOST || '127.0.0.1';
if (!['127.0.0.1', 'localhost'].includes(dbHost)) throw new Error('Assessment database must be loopback-only');
const dbPort = process.env.ASE_DB_PORT || '5432';
const runId = required('ASE_RUN_ID');
if (!/^[a-z0-9_]{1,30}$/.test(runId)) throw new Error('Invalid ASE_RUN_ID');
const output = path.resolve(root, process.env.ASE_ENV_DIR || `artifacts/environment-${runId}`);
await mkdir(output, { recursive: false });
const children = [];
const logs = [];
let stopping = false;
function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  for (const child of children) child.kill();
  for (const log of logs) log.end();
  setTimeout(() => process.exit(code), 1000).unref();
}
process.on('SIGINT', () => stop());
process.on('SIGTERM', () => stop());
async function free(port) {
  await new Promise((resolve, reject) => {
    const server = createServer(); server.once('error', reject);
    server.listen(port, '127.0.0.1', () => server.close(resolve));
  });
}
function start(command, args, env, name, cwd = root) {
  const log = createWriteStream(path.join(output, `${name}.log`), { flags: 'wx' }); logs.push(log);
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.pipe(log); child.stderr.pipe(log); children.push(child);
  child.on('error', error => { console.error(`${name}: ${error.message}`); stop(1); });
  return child;
}
async function build(service) {
  const project = `src/backend/services/${service}/${service}.Api/${service}.Api.csproj`;
  const child = start('dotnet', ['build', project, '-c', 'Release', '--nologo'], {}, `build-${service}`);
  await new Promise((resolve, reject) => child.on('exit', code => code === 0 ? resolve() : reject(new Error(`${service} build failed; inspect environment logs`))));
}
async function ready(url) {
  const end = Date.now() + 120000;
  while (Date.now() < end && !stopping) {
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return; } catch { /* bounded readiness retry */ }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`Readiness timeout: ${url}`);
}
async function json(url, options = {}) {
  const response = await fetch(url, { ...options, signal: AbortSignal.timeout(10000) });
  if (!response.ok) throw new Error(`Assessment setup request failed: ${new URL(url).pathname}, HTTP ${response.status}`);
  return response.json();
}
try {
  for (const port of [15001, 15002, 15173]) await free(port);
  // Serialize builds because both projects share output directories.
  await build('IdentityService'); await build('FleetService');
  const common = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ConnectionStrings__DefaultConnection: '',
    DB_HOST: dbHost, DB_PORT: dbPort, DB_USER: process.env.ASE_DB_USER || 'postgres', DB_PASSWORD: dbPassword,
    DB_SSL: 'false', QA_SEED_PASSWORD: password,
    Jwt__Key: 'FleetFlow_assessment_only_signing_key_2026_at_least_32_bytes',
    Jwt__Issuer: 'FleetFlow.IdentityService', Jwt__Audience: 'FleetFlow.Client',
    Kafka__Enabled: 'false', ONBOARDING_ENABLED: 'false',
    SERVICE_AUTH_ENABLED: 'true',
    SERVICE_AUTH_SIGNING_KEY: 'FleetFlow_assessment_internal_signing_key_2026_different_from_user',
    SERVICE_AUTH_FLEET_SECRET: 'FleetFlow_assessment_fleet_client_credential_2026_only',
    RENTAL_OWNERSHIP: 'Legacy', NOTIFICATION_OWNERSHIP: 'Legacy',
    INTERNAL_IDENTITY_URL: 'http://127.0.0.1:15001/',
    ALLOWED_ORIGINS: 'http://127.0.0.1:15173',
    Logging__LogLevel__Default: 'Warning', Logging__LogLevel__Microsoft: 'Warning',
  };
  for (const [service, port, databaseKey, suffix] of [
    ['IdentityService', 15001, 'AUTH_DB_NAME', 'identity'], ['FleetService', 15002, 'FLEET_DB_NAME', 'fleet'],
  ]) {
    const dir = path.join(root, `src/backend/services/${service}/${service}.Api`);
    const child = start('dotnet', [path.join(dir, `bin/Release/net10.0/${service}.Api.dll`)], {
      ...common, ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
      [databaseKey]: `fleetflow_ase_${runId}_${suffix}`,
      IDENTITY_UPLOAD_ROOT: path.join(output, 'identity-uploads'), FLEET_UPLOAD_ROOT: path.join(output, 'fleet-uploads'),
    }, service, dir);
    child.on('exit', code => { if (!stopping) { console.error(`${service} exited ${code}`); stop(1); } });
  }
  await ready('http://127.0.0.1:15001/health/ready');
  await ready('http://127.0.0.1:15002/health/ready');
  const session = await json('http://127.0.0.1:15001/api/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ usernameOrEmail: 'manager', password, loginChannel: 'staff' }),
  });
  const categories = await json('http://127.0.0.1:15002/api/vehicle-categories');
  if (!categories.length) throw new Error('No seeded vehicle categories');
  const vehicles = [];
  for (const [i, model] of ['Roadster', 'City'].entries()) {
    vehicles.push(await json('http://127.0.0.1:15002/api/vehicles', {
      method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${session.token}` },
      body: JSON.stringify({ vin: `ASE0000000000000${i+1}`, licensePlate: `ASE-00${i+1}`, make: 'ASE', model, year: 2026,
        vehicleCategoryId: categories[0].id, dailyRate: 4500-i*1000, transmission: 'Automatic', fuelType: 'Gasoline',
        seatingCapacity: '5', hubLocation: 'Assessment hub', mileage: 100 }),
    }));
  }
  const frontend = path.join(root, 'src/frontend');
  start(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '15173', '--strictPort'], {
    VITE_IDENTITY_API_URL: 'http://127.0.0.1:15001', VITE_FLEET_API_URL: 'http://127.0.0.1:15002',
    VITE_RENTAL_API_URL: 'http://127.0.0.1:15002', VITE_NOTIFICATION_API_URL: 'http://127.0.0.1:15002',
  }, 'frontend', frontend);
  await ready('http://127.0.0.1:15173');
  await writeFile(path.join(output, 'environment.json'), JSON.stringify({ runId, readyUtc: new Date().toISOString(),
    identity: 'http://127.0.0.1:15001', fleet: 'http://127.0.0.1:15002', frontend: 'http://127.0.0.1:15173',
    databaseHost: dbHost, databasePort: dbPort, childPids: children.filter(c => c.exitCode === null).map(c => c.pid), vehicles: vehicles.map(v => ({ id: v.id, make: v.make, model: v.model })),
    limitations: 'Legacy onboarding, rental and notifications; Kafka disabled; disposable synthetic accounts and vehicles',
  }, null, 2));
  console.log('ASSESSMENT_READY: real APIs + frontend, isolated synthetic data');
} catch (error) { console.error(error.message); stop(1); }
