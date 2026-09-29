export const REPORT_VIEWS = ['fleet-performance', 'booking-reports', 'maintenance-reports'];

export function canViewReport(view, roles = []) {
  const normalized = roles.map(role => String(role).trim().toUpperCase());
  if (!REPORT_VIEWS.includes(view)) return false;
  return normalized.includes('ADMIN') || view === 'maintenance-reports' &&
    normalized.some(role => ['FLEET_MANAGER', 'MAINTENANCE_STAFF'].includes(role));
}

export function formatReportMetric(value, suffix = '') {
  return typeof value === 'number' && Number.isFinite(value) ? `${value.toLocaleString()}${suffix}` : 'Unavailable';
}
