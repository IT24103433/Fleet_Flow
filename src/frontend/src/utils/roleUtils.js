export const decodeJwtPayload = (token) => {
  try {
    if (!token || typeof token !== 'string') return null;
    const parts = token.split('.');
    if (parts.length < 2) return null;
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch {
    return null;
  }
};

export const isTokenExpired = (token) => {
  try {
    const payload = decodeJwtPayload(token);
    if (!payload || typeof payload !== 'object') {
      return true;
    }
    if (typeof payload.exp !== 'number' || !Number.isFinite(payload.exp)) {
      return true;
    }
    // exp is in seconds since Unix epoch
    return Date.now() >= payload.exp * 1000;
  } catch {
    return true;
  }
};

export const extractRolesFromToken = (token) => {
  try {
    const payload = decodeJwtPayload(token);
    if (!payload) return [];

    const rawRole =
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
      payload['role'] ??
      [];

    if (Array.isArray(rawRole)) {
      return rawRole
        .filter((r) => typeof r === 'string' && r.trim().length > 0)
        .map((r) => r.toUpperCase().trim());
    }
    if (typeof rawRole === 'string' && rawRole.trim().length > 0) {
      return [rawRole.toUpperCase().trim()];
    }
    return [];
  } catch {
    return [];
  }
};

export const getRoleDefaultView = (roles) => {
  const normalized = (roles || []).map((r) => String(r).toUpperCase().trim());
  if (normalized.includes('ADMIN')) {
    return 'admin-dashboard';
  }
  if (normalized.includes('FLEET_MANAGER') || normalized.includes('MAINTENANCE_STAFF')) {
    return 'staff-dashboard';
  }
  if (normalized.includes('CUSTOMER')) {
    return 'customer-home';
  }
  return 'landing';
};
