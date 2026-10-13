const vehicleViews = new Set(['vehicle-details', 'staff-vehicle-details', 'edit-vehicle', 'vehicle-images']);
const userViews = new Set(['admin-user-details', 'admin-edit-user']);
const filterNames = ['searchTerm', 'category', 'fuel', 'transmission', 'sort', 'page'];

export function parseRoute(hash = '') {
  const [view, query = ''] = hash.replace(/^#\/?/, '').split('?');
  return { view: view || 'landing', params: Object.fromEntries(new URLSearchParams(query)) };
}

export function routeHash(view, params = {}) {
  const query = new URLSearchParams();
  if (vehicleViews.has(view) && params.vehicleId) query.set('vehicleId', params.vehicleId);
  if (userViews.has(view) && params.userId) query.set('userId', params.userId);
  if (view === 'browse') filterNames.forEach(key => { if (params[key]) query.set(key, params[key]); });
  if (['login', 'register', 'force-password-change'].includes(view) && safeCustomerReturn(params.returnTo)) query.set('returnTo', safeCustomerReturn(params.returnTo));
  return `#/${view}${query.size ? `?${query}` : ''}`;
}

export function safeCustomerReturn(hash) {
  if (typeof hash !== 'string' || !hash.startsWith('#/')) return null;
  const route = parseRoute(hash);
  if (!['browse', 'vehicle-details', 'customer-home'].includes(route.view)) return null;
  if (route.view === 'vehicle-details' && !/^[a-f\d]{8}(?:-[a-f\d]{4}){3}-[a-f\d]{12}$/i.test(route.params.vehicleId || '')) return null;
  return routeHash(route.view, route.params);
}
