// Assigned owner: Aqqil M.R.M. (IT24104314), SE3112 load/performance layer.
import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const identity = __ENV.IDENTITY_BASE_URL || 'http://127.0.0.1:15001';
const fleet = __ENV.FLEET_BASE_URL || 'http://127.0.0.1:15002';
for (const url of [identity, fleet]) {
  if (!/^http:\/\/(127\.0\.0\.1|localhost):\d+$/.test(url)) throw new Error('Load tests only target explicit loopback HTTP services');
}
if (!__ENV.ASE_TEST_PASSWORD || !__ENV.RESULTS_DIR) throw new Error('ASE_TEST_PASSWORD and RESULTS_DIR are required');
const profile = __ENV.PROFILE || 'smoke';
const profiles = {
  smoke: { executor: 'constant-vus', vus: 1, duration: '5s', gracefulStop: '5s' },
  baseline: { executor: 'constant-vus', vus: 1, duration: '10s', gracefulStop: '5s' },
  ramp: { executor: 'ramping-vus', startVUs: 0, stages: [{ duration:'10s', target:5 }, { duration:'10s', target:10 }, { duration:'10s', target:0 }], gracefulRampDown:'5s', gracefulStop:'5s' },
  stress: { executor: 'ramping-vus', startVUs: 0, stages: [{ duration:'10s', target:10 }, { duration:'10s', target:25 }, { duration:'10s', target:50 }, { duration:'10s', target:0 }], gracefulRampDown:'5s', gracefulStop:'5s' },
};
if (!profiles[profile]) throw new Error('Unknown PROFILE');
if (profile === 'stress' && __ENV.ALLOW_STRESS !== '1') throw new Error('Set ALLOW_STRESS=1 for the bounded local stress profile');
const http2xx = new Counter('responses_2xx');
const http4xx = new Counter('responses_4xx');
const http5xx = new Counter('responses_5xx');
const other = new Counter('responses_other_or_transport');
const loginTime = new Trend('login_duration', true);
const listTime = new Trend('vehicle_list_duration', true);
const detailTime = new Trend('vehicle_detail_duration', true);
export const options = {
  scenarios: { [profile]: profiles[profile] },
  thresholds: { http_req_failed: ['rate<0.01'], checks: ['rate==1'], http_req_duration: ['p(95)<2000'],
    login_duration:['p(95)<2000'], vehicle_list_duration:['p(95)<1000'], vehicle_detail_duration:['p(95)<1000'] },
  summaryTrendStats: ['avg','min','med','max','p(90)','p(95)'],
};
function record(response, metric) {
  metric.add(response.timings.duration);
  http2xx.add(response.status >= 200 && response.status < 300 ? 1 : 0);
  http4xx.add(response.status >= 400 && response.status < 500 ? 1 : 0);
  http5xx.add(response.status >= 500 ? 1 : 0);
  other.add(response.status < 200 || (response.status >= 300 && response.status < 400) ? 1 : 0);
}
function body(response) { try { return response.json(); } catch { return null; } }
export function setup() {
  for (const base of [identity, fleet]) {
    if (http.get(`${base}/health/ready`, { timeout:'5s' }).status !== 200) fail('Application is not ready');
  }
  const list = http.get(`${fleet}/api/vehicles?paged=true&pageSize=20`, { timeout:'5s' });
  const data = body(list);
  if (list.status !== 200 || !data?.items?.some(v => v.make === 'ASE')) fail('Seeded assessment vehicles are missing');
  return { vehicleId: data.items.find(v => v.model === 'Roadster')?.id || data.items[0].id };
}
export default function(data) {
  const session = http.post(`${identity}/api/auth/login`, JSON.stringify({ usernameOrEmail: __ENV.ASE_TEST_USERNAME || 'customer', password: __ENV.ASE_TEST_PASSWORD, loginChannel:'customer' }),
    { headers:{ 'Content-Type':'application/json' }, tags:{ name:'customer_login' }, timeout:'10s' });
  record(session,loginTime);
  check(session, { 'login returns a real session': r => r.status === 200 && typeof body(r)?.token === 'string' && body(r)?.user?.username === (__ENV.ASE_TEST_USERNAME || 'customer') });
  const list = http.get(`${fleet}/api/vehicles?paged=true&pageSize=20`, { tags:{ name:'vehicle_list' }, timeout:'10s' });
  record(list,listTime);
  check(list, { 'vehicle list has seeded data and pagination': r => r.status === 200 && body(r)?.items?.some(v => v.id === data.vehicleId) && body(r)?.totalCount >= 2 });
  const detail = http.get(`${fleet}/api/vehicles/${data.vehicleId}`, { tags:{ name:'vehicle_detail' }, timeout:'10s' });
  record(detail,detailTime);
  check(detail, { 'vehicle detail preserves the requested identity': r => r.status === 200 && body(r)?.id === data.vehicleId && body(r)?.make === 'ASE' });
  sleep(0.2);
}
export function handleSummary(data) {
  const summary = { owner:'Aqqil M.R.M.', studentId:'IT24104314', profile, capturedUtc:new Date().toISOString(),
    targets:{ identity, fleet }, policy:'local controlled assessment; thresholds are project hypotheses, not official SLAs', ...data };
  return { [`${__ENV.RESULTS_DIR}/summary.json`]: JSON.stringify(summary,null,2) };
}
