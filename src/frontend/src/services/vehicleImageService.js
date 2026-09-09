const FLEET_API_URL = import.meta.env.VITE_FLEET_API_URL || 'http://localhost:5002';

export const getVehicleImages = async (vehicleId, token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${FLEET_API_URL}/api/vehicles/${vehicleId}/images`, {
      method: 'GET',
      headers,
    });

    const contentType = response.headers.get('content-type');
    let data = null;
    if (contentType && contentType.includes('application/json')) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || 'Failed to retrieve vehicle images.',
        data: [],
      };
    }

    return {
      success: true,
      status: response.status,
      data: Array.isArray(data) ? data : [],
    };
  } catch {
    return {
      success: false,
      status: 0,
      message: 'Fleet service is currently unreachable. Please check network connectivity.',
      data: [],
    };
  }
};

export const uploadVehicleImage = async (vehicleId, file, caption, token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    // Do NOT set Content-Type header so fetch creates the multipart/form-data boundary automatically

    const formData = new FormData();
    formData.append('file', file);
    if (caption && caption.trim()) {
      formData.append('caption', caption.trim());
    }

    const response = await fetch(`${FLEET_API_URL}/api/vehicles/${vehicleId}/images`, {
      method: 'POST',
      headers,
      body: formData,
    });

    const contentType = response.headers.get('content-type');
    let data = null;
    if (contentType && contentType.includes('application/json')) {
      data = await response.json();
    }

    if (!response.ok) {
      let message = data?.message || data?.title;
      if (!message) {
        if (response.status === 401) {
          message = 'Authentication required. Please log in.';
        } else if (response.status === 403) {
          message = 'Access denied. Administrator or Fleet Manager privileges are required to upload vehicle photos.';
        } else if (response.status === 404) {
          message = 'Vehicle was not found in the fleet registry.';
        } else if (response.status === 400) {
          message = 'Invalid image file. Please provide a valid JPEG, PNG, or WebP image under 5 MB.';
        } else {
          message = 'An unexpected error occurred while uploading the vehicle image.';
        }
      }

      return {
        success: false,
        status: response.status,
        message,
      };
    }

    return {
      success: true,
      status: response.status,
      data,
    };
  } catch {
    return {
      success: false,
      status: 0,
      message: 'Fleet service is currently unreachable. Please check network connectivity.',
    };
  }
};

export const deleteVehicleImage = async (vehicleId, imageId, token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${FLEET_API_URL}/api/vehicles/${vehicleId}/images/${imageId}`, {
      method: 'DELETE',
      headers,
    });

    if (response.status === 204) {
      return {
        success: true,
        status: 204,
      };
    }

    const contentType = response.headers.get('content-type');
    let data = null;
    if (contentType && contentType.includes('application/json')) {
      data = await response.json();
    }

    let message = data?.message || data?.title;
    if (!message) {
      if (response.status === 401) {
        message = 'Authentication required. Please log in.';
      } else if (response.status === 403) {
        message = 'Access denied. Administrator or Fleet Manager privileges are required to delete vehicle photos.';
      } else if (response.status === 404) {
        message = 'Photo or vehicle not found.';
      } else {
        message = 'An unexpected error occurred while deleting the vehicle photo.';
      }
    }

    return {
      success: false,
      status: response.status,
      message,
    };
  } catch {
    return {
      success: false,
      status: 0,
      message: 'Fleet service is currently unreachable. Please check network connectivity.',
    };
  }
};
