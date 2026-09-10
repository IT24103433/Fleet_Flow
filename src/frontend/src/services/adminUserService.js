const IDENTITY_API_URL = import.meta.env.VITE_IDENTITY_API_URL || 'http://localhost:5001';

export const createAdminUser = async (userData, token) => {
  try {
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/admin/users`, {
      method: 'POST',
      headers,
      body: JSON.stringify(userData),
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
          message = 'Authentication required. Please log in as Administrator.';
        } else if (response.status === 403) {
          message = 'Access denied. Administrator privileges are required to create users.';
        } else if (response.status === 409) {
          message = 'A user with this username or email already exists.';
        } else if (response.status === 400) {
          message = 'Invalid user parameters. Please check your input fields.';
        } else {
          message = 'An unexpected error occurred while creating the user account.';
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
      message: 'The identity service is currently unreachable. Please check your network and try again.',
    };
  }
};

export const getAdminUsers = async (token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/admin/users`, {
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
        message: data?.message || 'Failed to retrieve user directory.',
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
      message: 'The identity service is currently unreachable. Please check your network and try again.',
    };
  }
};

export const updateAdminUser = async (id, userData, token) => {
  try {
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/admin/users/${id}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(userData),
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
          message = 'Authentication required. Please log in as Administrator.';
        } else if (response.status === 403) {
          message = 'Access denied. Administrator privileges are required to modify users.';
        } else if (response.status === 404) {
          message = 'User account was not found in the identity service.';
        } else if (response.status === 409) {
          message = 'A user with this username or email already exists.';
        } else if (response.status === 400) {
          message = 'Invalid user parameters. Please check your input fields.';
        } else {
          message = 'An unexpected error occurred while updating the user account.';
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
      message: 'The identity service is currently unreachable. Please check your network and try again.',
    };
  }
};

export const deleteAdminUser = async (id, token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/admin/users/${id}`, {
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
        message = 'Authentication required. Please log in as Administrator.';
      } else if (response.status === 403) {
        message = 'Access denied. Administrator privileges are required to delete users.';
      } else if (response.status === 404) {
        message = 'User account was not found.';
      } else if (response.status === 400) {
        message = 'Cannot delete this user account.';
      } else {
        message = 'An unexpected error occurred while deleting the user account.';
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
      message: 'The identity service is currently unreachable. Please check your network and try again.',
    };
  }
};

