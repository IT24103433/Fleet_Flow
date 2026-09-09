const FLEET_API_URL = import.meta.env.VITE_FLEET_API_URL || 'http://localhost:5002';
const IDENTITY_API_URL = import.meta.env.VITE_IDENTITY_API_URL || 'http://localhost:5001';

/**
 * Resolves a relative or absolute vehicle image URL into a full displayable URL.
 */
export const getVehicleImageUrl = (relativeUrl) => {
  if (!relativeUrl) return null;
  if (relativeUrl.startsWith('http://') || relativeUrl.startsWith('https://') || relativeUrl.startsWith('blob:')) {
    return relativeUrl;
  }
  const normalized = relativeUrl.startsWith('/') ? relativeUrl : `/${relativeUrl}`;
  return `${FLEET_API_URL}${normalized}`;
};

/**
 * Resolves a relative or absolute user profile image URL into a full displayable URL.
 */
export const getProfileImageUrl = (relativeUrl) => {
  if (!relativeUrl) return null;
  if (relativeUrl.startsWith('http://') || relativeUrl.startsWith('https://') || relativeUrl.startsWith('blob:')) {
    return relativeUrl;
  }
  const normalized = relativeUrl.startsWith('/') ? relativeUrl : `/${relativeUrl}`;
  return `${IDENTITY_API_URL}${normalized}`;
};
