const FLEET_API_URL = import.meta.env?.VITE_FLEET_API_URL || 'http://localhost:5002';
export function createOutboxClient({ baseUrl = FLEET_API_URL, fetchImpl = globalThis.fetch,
  getToken = () => sessionStorage.getItem('token') } = {}) {
  async function request(path, method, signal) {
    const token = getToken();
    if (!token) throw new Error('Please sign in to review notification delivery.');
    let response;
    try {
      response = await fetchImpl(`${baseUrl}/api/notifications/outbox${path}`, {
        method, headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' }, signal,
      });
    } catch (error) {
      if (error.name === 'AbortError') throw error;
      throw new Error('Delivery records are unavailable. Check your connection and try again.');
    }
    if (!response.ok) {
      throw new Error(response.status === 401 ? 'Your session expired. Please sign in again.'
        : response.status === 403 ? 'Administrator access is required.'
          : response.status === 409 ? 'This event is no longer eligible for replay. Refresh the delivery queue.'
            : response.status === 404 ? 'Delivery records are unavailable on this service.'
              : 'Delivery records are unavailable. Please try again.');
    }
    return response.status === 202 ? null : response.json();
  }
  return {
    getEvents: (status = '', signal) => request(status ? `?status=${encodeURIComponent(status)}` : '', 'GET', signal),
    replay: (id, signal) => request(`/${encodeURIComponent(id)}/replay`, 'POST', signal),
  };
}
export const outboxClient = createOutboxClient();
const rentalUrl = import.meta.env?.VITE_RENTAL_API_URL;
export const deliverySources = rentalUrl ? [
  { value: 'fleet', label: 'Fleet · maintenance', client: outboxClient },
  { value: 'rental', label: 'Rental · bookings', client: createOutboxClient({ baseUrl: rentalUrl }) },
] : [{ value: 'fleet', label: 'Fleet', client: outboxClient }];
