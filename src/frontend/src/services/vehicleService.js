const FLEET_API_URL = import.meta.env.VITE_FLEET_API_URL || 'http://localhost:5002';

export const getVehicles = async (params = {}) => {
  try {
    const query = new URLSearchParams();
    if (params.category && params.category !== 'All Categories' && params.category !== 'ALL') {
      query.append('category', params.category);
    }
    if (params.status && params.status !== 'All Statuses' && params.status !== 'ALL') {
      query.append('status', params.status);
    }
    if (params.fuel && params.fuel !== 'All Fuels' && params.fuel !== 'ALL') {
      query.append('fuel', params.fuel);
    }
    if (params.searchTerm) {
      query.append('searchTerm', params.searchTerm.trim());
    }
    if (params.page) {
      query.append('page', params.page);
    }
    if (params.pageSize) {
      query.append('pageSize', params.pageSize);
    }

    const queryString = query.toString();
    const url = `${FLEET_API_URL}/api/vehicles${queryString ? `?${queryString}` : ''}`;

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
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
        message: data?.message || 'Failed to fetch vehicles.',
        errors: data?.errors || null,
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
      message: 'The fleet service is currently unreachable. Please check your network and ensure FleetService is running.',
    };
  }
};

export const getVehicleById = async (id) => {
  try {
    const response = await fetch(`${FLEET_API_URL}/api/vehicles/${id}`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
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
        message: data?.message || (response.status === 404 ? 'Vehicle not found.' : 'Failed to retrieve vehicle details.'),
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
      message: 'The fleet service is currently unreachable. Please check your network and try again.',
    };
  }
};

export const getCategories = async () => {
  try {
    const response = await fetch(`${FLEET_API_URL}/api/vehicle-categories`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
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
        message: data?.message || 'Failed to retrieve vehicle categories.',
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
      message: 'The fleet service is currently unreachable. Please check your network and try again.',
    };
  }
};

export const createVehicle = async (vehicleData, token) => {
  try {
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${FLEET_API_URL}/api/vehicles`, {
      method: 'POST',
      headers,
      body: JSON.stringify(vehicleData),
    });

    const contentType = response.headers.get('content-type');
    let data = null;
    if (contentType && contentType.includes('application/json')) {
      data = await response.json();
    }

    if (!response.ok) {
      let message = data?.message;
      if (!message) {
        if (response.status === 401) {
          message = 'Authentication required. Please log in as Fleet Manager or Admin.';
        } else if (response.status === 403) {
          message = 'Access denied. You do not have permission to add vehicles to the fleet.';
        } else if (response.status === 409) {
          message = 'A vehicle with this VIN or license plate already exists in the system.';
        } else if (response.status === 400) {
          message = 'Invalid vehicle parameters. Please check your input fields.';
        } else {
          message = 'An unexpected error occurred while adding the vehicle.';
        }
      }

      return {
        success: false,
        status: response.status,
        message,
        errors: data?.errors || null,
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
      message: 'The fleet service is currently unreachable. Please check your network and try again.',
    };
  }
};
