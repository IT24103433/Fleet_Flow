import { fetchWithSession, isApiJson } from './sessionFetch';
export const identityRoot = import.meta.env.VITE_IDENTITY_API_URL || 'http://localhost:5001';
export async function onboardingRequest(path, { method = 'GET', body, signal, anonymous = false } = {}) {
  const headers = {};
  if (!anonymous) { const token = sessionStorage.getItem('token'); if (token) headers.Authorization = `Bearer ${token}`; }
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
  const response = await fetchWithSession(`${identityRoot}/api/onboarding/${path}`, {
    method, headers, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
    signal: signal || AbortSignal.timeout(15000),
  });
  const data = isApiJson(response.headers.get('content-type')) ? await response.json() : null;
  if (!response.ok) {
    const error = new Error(data?.message || Object.values(data?.errors || {}).flat().join(' ') || 'This request could not be completed. Try again.');
    error.status = response.status; throw error;
  }
  return data;
}

export async function privatePhoto(id, signal) {
  const response = await fetchWithSession(`${identityRoot}/api/onboarding/driver/photos/${id}`, {
    headers: { Authorization: `Bearer ${sessionStorage.getItem('token')}` }, signal,
  });
  if (!response.ok) throw new Error('Document photograph unavailable.');
  return URL.createObjectURL(await response.blob());
}
