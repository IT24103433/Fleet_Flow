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
    if (params.fuel && params.fuel !== 'All Fuels' && params.fuel !== 'All Powertrains' && params.fuel !== 'ALL') {
      query.append('fuel', params.fuel);
    }
    if (params.transmission && params.transmission !== 'All Transmissions' && params.transmission !== 'ALL') {
      query.append('transmission', params.transmission);
    }
    if (params.hub && params.hub !== 'All Hubs' && params.hub !== 'All Locations' && params.hub !== 'ALL') {
      query.append('hub', params.hub);
    }
    if (params.searchTerm && params.searchTerm.trim()) {
      query.append('searchTerm', params.searchTerm.trim());
    }
    if (params.sortBy) {
      query.append('sortBy', params.sortBy);
    }
    if (params.sortOrder) {
      query.append('sortOrder', params.sortOrder);
    }
    if (params.page) {
      query.append('page', params.page);
    }
    if (params.pageSize) {
      query.append('pageSize', params.pageSize);
    }
    // Request paged result structure
    query.append('paged', 'true');

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

    // Support both wrapped PagedVehicleResult ({ items, totalCount, page, pageSize, totalPages })
    // and legacy raw array with HTTP header fallbacks
    let items = [];
    let totalCount = 0;
    let page = Number(params.page) || 1;
    let pageSize = Number(params.pageSize) || 20;
    let totalPages = 1;
    let hasNextPage = false;
    let hasPreviousPage = false;

    if (data && typeof data === 'object' && Array.isArray(data.items)) {
      items = data.items;
      totalCount = Number(data.totalCount) || items.length;
      page = Number(data.page) || page;
      pageSize = Number(data.pageSize) || pageSize;
      totalPages = Number(data.totalPages) || (pageSize > 0 ? Math.ceil(totalCount / pageSize) : 1);
      hasNextPage = Boolean(data.hasNextPage);
      hasPreviousPage = Boolean(data.hasPreviousPage);
    } else if (Array.isArray(data)) {
      items = data;
      const headerTotal = response.headers.get('X-Total-Count');
      totalCount = headerTotal ? Number(headerTotal) : items.length;
      const headerPage = response.headers.get('X-Page');
      if (headerPage) page = Number(headerPage);
      const headerPageSize = response.headers.get('X-Page-Size');
      if (headerPageSize) pageSize = Number(headerPageSize);
      totalPages = pageSize > 0 ? Math.ceil(totalCount / pageSize) : 1;
      hasNextPage = page < totalPages;
      hasPreviousPage = page > 1;
    }

    return {
      success: true,
      status: response.status,
      data: items,
      pagination: {
        totalCount,
        page,
        pageSize,
        totalPages,
        hasNextPage,
        hasPreviousPage,
      },
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

export const updateVehicle = async (id, vehicleData, token) => {
  try {
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${FLEET_API_URL}/api/vehicles/${id}`, {
      method: 'PUT',
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
          message = 'Access denied. You do not have permission to edit vehicle records.';
        } else if (response.status === 404) {
          message = 'The specified vehicle was not found in the fleet catalog.';
        } else if (response.status === 409) {
          message = 'A vehicle with this license plate or VIN already exists.';
        } else if (response.status === 400) {
          message = 'Invalid vehicle parameters. Please check your input fields.';
        } else {
          message = 'An unexpected error occurred while updating the vehicle.';
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
