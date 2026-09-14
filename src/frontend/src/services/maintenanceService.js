const FLEET_API_URL = import.meta.env.VITE_FLEET_API_URL || 'http://localhost:5002';

/**
 * Retrieves the live maintenance dashboard data including persisted status counts,
 * attention items, and vehicles currently in maintenance from the Maintenance API.
 * 
 * @param {string} token - JWT authentication token for authorized staff
 * @param {Object} params - Query filters e.g. { hub: '...' }
 * @returns {Promise<Object>} Status, success flag, and response payload
 */
export const getMaintenanceDashboard = async (token, params = {}) => {
  try {
    const query = new URLSearchParams();
    if (params.hub && params.hub !== 'All Hubs' && params.hub !== 'All Locations' && params.hub !== 'ALL') {
      query.append('hub', params.hub);
    }

    const queryString = query.toString();
    const url = `${FLEET_API_URL}/api/maintenance/dashboard${queryString ? `?${queryString}` : ''}`;

    const headers = {
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      method: 'GET',
      headers,
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
          message = 'Authentication required. Please log in as an authorized staff member.';
        } else if (response.status === 403) {
          message = 'Access denied. You do not have permission to access the maintenance dashboard.';
        } else if (response.status === 404) {
          message = 'The maintenance service endpoint was not found.';
        } else {
          message = 'An unexpected error occurred while retrieving maintenance dashboard metrics.';
        }
      }

      return {
        success: false,
        status: response.status,
        message,
        data: null,
      };
    }

    return {
      success: true,
      status: response.status,
      data: data || {
        statusCounts: {
          totalVehicles: 0,
          undergoingMaintenance: 0,
          available: 0,
          inUse: 0,
          retired: 0,
        },
        attentionItems: [],
        maintenanceVehicles: [],
        generatedAt: new Date().toISOString(),
      },
    };
  } catch {
    return {
      success: false,
      status: 0,
      message: 'The maintenance service is currently unreachable. Please check your network and verify the service is running.',
      data: null,
    };
  }
};
