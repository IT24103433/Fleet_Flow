const FLEET_API_URL = import.meta.env?.VITE_FLEET_API_URL || 'http://localhost:5002';

export function createNotificationClient({ baseUrl = FLEET_API_URL, fetchImpl = globalThis.fetch,
  getToken = () => sessionStorage.getItem('token') } = {}) {
  async function request(path, method = 'GET', signal) {
    const token = getToken();
    if (!token) throw new Error('Please log in to view notifications.');
    const response = await fetchImpl(`${baseUrl}/api/notifications${path}`, {
      method, headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' }, signal,
    });
    if (!response.ok) {
      throw new Error(response.status === 401 ? 'Your session expired. Please log in again.'
        : response.status === 404 ? 'Notification not found.' : 'Notifications are unavailable. Please try again.');
    }
    return response.status === 204 ? null : response.json();
  }
  return {
    getInbox: (signal) => request('', 'GET', signal),
    getUnreadCount: (signal) => request('/unread-count', 'GET', signal),
    markRead: (id, signal) => request(`/${encodeURIComponent(id)}/read`, 'PATCH', signal),
  };
}

export const notificationClient = createNotificationClient();
