export const SUPPORTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

export const canUserManageVehicleImages = (roles = []) => {
  const normalized = (roles || []).map((r) => String(r).toUpperCase().trim());
  return normalized.includes('FLEET_MANAGER') || normalized.includes('ADMIN');
};

export const validateVehicleImageFile = (file) => {
  if (!file) {
    return { isValid: false, error: 'No image file was provided.' };
  }

  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    return {
      isValid: false,
      error: 'Selected photo exceeds the 5 MB limit. Please select a smaller image file.',
    };
  }

  if (!SUPPORTED_IMAGE_TYPES.includes(file.type)) {
    return {
      isValid: false,
      error: 'Unsupported file format. Only JPEG, PNG, and WebP images are accepted.',
    };
  }

  return { isValid: true, error: null };
};

export const validateImageActionAuthorization = (roles = []) => {
  if (!canUserManageVehicleImages(roles)) {
    return {
      isValid: false,
      error: 'Access denied. Only Fleet Managers and Administrators have permission to manage vehicle images.',
    };
  }
  return { isValid: true, error: null };
};
