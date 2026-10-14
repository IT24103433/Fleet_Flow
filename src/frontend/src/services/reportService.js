import { fetchWithSession } from './sessionFetch.js';
const FLEET_API_URL = import.meta.env?.VITE_FLEET_API_URL || 'http://localhost:5002';

export function createReportClient({ baseUrl = FLEET_API_URL, fetchImpl = (...args) => fetchWithSession(...args),
  getToken = () => sessionStorage.getItem('token') } = {}) {
  async function getReport(path, signal) {
    const token = getToken();
    if (!token) throw new Error('Please log in to view reports.');
    const response = await fetchImpl(`${baseUrl}/api/reports/${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' }, signal, cache: 'no-store',
    }).catch(error => {
      if (error.name === 'AbortError') throw error;
      throw new Error('Reports are unavailable right now. Check your connection and try again.');
    });
    if (!response.ok) throw new Error(response.status === 401 ? 'Your session expired. Please log in again.'
      : response.status === 403 ? 'You do not have permission to view this report.'
        : 'Reporting data is unavailable. Please try again.');
    return response.json();
  }
  return {
    exportReport: async (path, signal) => {
      const token = getToken();
      if (!token) throw new Error('Please log in to export reports.');
      let response;
      try { response = await fetchImpl(`${baseUrl}/api/reports/${path}`, { headers: { Authorization: `Bearer ${token}`, Accept: 'text/csv' }, signal, cache: 'no-store' }); }
      catch (error) { if (signal?.aborted || error.name === 'AbortError') throw error; throw new Error('The export could not be downloaded. Please try again.'); }
      if (!response.ok) {
        let message;
        if (/application\/(json|problem\+json)/i.test(response.headers?.get('content-type') || '')) { const data = await response.json().catch(() => null); message = data?.message || data?.title; }
        throw new Error(response.status === 401 ? 'Your session expired. Please log in again.' : response.status === 403 ? 'You do not have permission to export this report.' : message || 'The report could not be exported. Please try again.');
      }
      if (!/^text\/csv(?:;|$)/i.test(response.headers.get('content-type') || '')) throw new Error('The server did not return a CSV file. Please try again.');
      return response.blob();
    },
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
