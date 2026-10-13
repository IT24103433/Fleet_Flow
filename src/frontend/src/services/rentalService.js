const baseUrl = import.meta.env?.VITE_RENTAL_API_URL || import.meta.env?.VITE_FLEET_API_URL || 'http://localhost:5002';
export function createRentalClient({ url = baseUrl, fetchImpl = (...args) => globalThis.fetch(...args),
  getToken = () => sessionStorage.getItem('token') || localStorage.getItem('token') } = {}) {
  async function request(path, { signal, body } = {}) {
    const token = getToken();
    if (!token) throw Object.assign(new Error('Sign in to view rental records.'), { status: 401 });
    let response;
    try {
      response = await fetchImpl(`${url}${path}`, { method: body ? 'POST' : 'GET', signal,
        headers: { Accept: 'application/json', Authorization: `Bearer ${token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
        ...(body ? { body: JSON.stringify(body) } : {}) });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw Object.assign(new Error(body ? 'Pickup could not be confirmed. Refresh the record before handing over the keys.' : 'Rental records are unavailable. Please try again.'), { status: 503 });
    }
    const data = /application\/(?:json|problem\+json)/i.test(response.headers.get('content-type') || '') ? await response.json() : null;
    if (!response.ok) throw Object.assign(new Error(data?.message || data?.title || 'The rental request could not be completed.'), { status: response.status, errors: data?.errors });
    return data;
  }
  return {
    queue: (state, page, signal) => request(`/api/rentals/queue?${new URLSearchParams({ state, page, pageSize: 12 })}`, { signal }),
    booking: (id, signal) => request(`/api/bookings/${encodeURIComponent(id)}`, { signal }),
    record: (id, signal) => request(`/api/rentals/${encodeURIComponent(id)}`, { signal }),
    pickup: (bookingId, body) => request(`/api/rentals/bookings/${encodeURIComponent(bookingId)}/pickup`, { body }),
  };
}
export const rentalClient = createRentalClient();
