const FLEET_API_URL = import.meta.env?.VITE_FLEET_API_URL || 'http://localhost:5002';

export function createReportClient({ baseUrl = FLEET_API_URL, fetchImpl = globalThis.fetch,
  getToken = () => sessionStorage.getItem('token') } = {}) {
  async function getReport(path, signal) {
    const token = getToken();
    if (!token) throw new Error('Please log in to view reports.');
    const response = await fetchImpl(`${baseUrl}/api/reports/${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' }, signal, cache: 'no-store',
    });
    if (!response.ok) throw new Error(response.status === 401 ? 'Your session expired. Please log in again.'
      : response.status === 403 ? 'You do not have permission to view this report.'
        : 'Reporting data is unavailable. Please try again.');
    return response.json();
  }
  return {
    getReport,
    getFleetSummary: signal => getReport('fleet-summary', signal),
    getOperationalStatistics: signal => getReport('operational-statistics', signal),
    getMaintenanceReport: signal => getReport('maintenance', signal),
    getBookingReport: ({ page = 1, pageSize = 25, status } = {}, signal) => {
      const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (status) query.set('status', status);
      return getReport(`bookings?${query}`, signal);
    },
  };
}

export const reportClient = createReportClient();
