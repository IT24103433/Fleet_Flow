const keys = ['VITE_IDENTITY_API_URL', 'VITE_FLEET_API_URL', 'VITE_RENTAL_API_URL', 'VITE_NOTIFICATION_API_URL'];
export function validateApiBuildSettings(settings, { localReview = false } = {}) {
  for (const key of keys) {
    let url;
    try { url = new URL(settings[key]); } catch { throw new Error(`${key} must be explicitly configured for the production build.`); }
    const loopback = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname.toLowerCase());
    const privateHost = loopback || /(?:\.local|\.internal|\.test)$/.test(url.hostname) || /^(?:10\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)/.test(url.hostname);
    if (url.username || url.password || url.search || url.hash || url.pathname !== '/') throw new Error(`${key} must contain only a trusted API origin.`);
    if (localReview ? !loopback || !['http:', 'https:'].includes(url.protocol) : url.protocol !== 'https:' || privateHost)
      throw new Error(`${key} must use ${localReview ? 'a loopback review origin' : 'a public HTTPS origin; localhost is not deployable'}.`);
  }
}
