// Clear only the session that sent the rejected request. A late response must
// not sign out an account that has since signed in with a different token.
export async function fetchWithSession(url, options = {}) {
  const response = await globalThis.fetch(url, options);
  if (response.status === 401 && typeof window !== 'undefined') {
    const authorization = new Headers(options.headers).get('Authorization');
    const token = authorization?.startsWith('Bearer ') ? authorization.slice(7) : null;
    try {
      if (token && window.sessionStorage.getItem('token') === token)
        window.dispatchEvent(new window.Event('fleetflow:session-revoked'));
    } catch { /* Session storage can be unavailable; the API result remains intact. */ }
  }
  return response;
}

export const isApiJson = contentType => /^application\/(?:json|problem\+json)(?:\s*;|\s*$)/i.test(contentType || '');
