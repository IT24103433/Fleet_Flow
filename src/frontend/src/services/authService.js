const IDENTITY_API_URL = import.meta.env.VITE_IDENTITY_API_URL || 'http://localhost:5001';

export const registerUser = async (dataOrUsername, email, password) => {
  const payload = typeof dataOrUsername === 'object' && dataOrUsername !== null
    ? dataOrUsername
    : { username: dataOrUsername, email, password };

  try {
    const response = await fetch(`${IDENTITY_API_URL}/api/auth/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(10000),
    });

    const contentType = response.headers.get("content-type");
    let data = null;
    if (contentType && contentType.includes("application/json")) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || data?.title || "An error occurred during registration.",
        errors: data?.errors || null
      };
    }

    return {
      success: true,
      status: response.status,
      data
    };
  } catch (error) {
    if (error?.name === 'TimeoutError' || error?.name === 'AbortError') {
      return {
        success: false,
        status: 0,
        message: "The authentication server took too long to respond (timeout after 10s). Please check that the backend is running."
      };
    }
    return {
      success: false,
      status: 0,
      message: "The authentication server is currently unreachable. Please check your network and try again."
    };
  }
};

export const loginUser = async (usernameOrEmail, password, loginChannel = 'customer') => {
  try {
    const response = await fetch(`${IDENTITY_API_URL}/api/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ usernameOrEmail, password, loginChannel }),
      signal: AbortSignal.timeout(10000),
    });

    const contentType = response.headers.get("content-type");
    let data = null;
    if (contentType && contentType.includes("application/json")) {
      data = await response.json();
    }

    if (!response.ok) {
      if (response.status === 401) {
        return {
          success: false,
          status: 401,
          message: "Invalid username/email or password."
        };
      }
      if (response.status === 403) {
        return {
          success: false,
          status: 403,
          message: data?.message || "You do not have access to this portal."
        };
      }
      return {
        success: false,
        status: response.status,
        message: data?.message || data?.title || "An error occurred during login.",
        errors: data?.errors || null
      };
    }

    return {
      success: true,
      status: response.status,
      data
    };
  } catch (error) {
    if (error?.name === 'TimeoutError' || error?.name === 'AbortError') {
      return {
        success: false,
        status: 0,
        message: "The authentication server took too long to respond (timeout after 10s). Please check that the backend is running."
      };
    }
    return {
      success: false,
      status: 0,
      message: "The authentication server is currently unreachable. Please check your network and try again."
    };
  }
};

export const uploadProfilePicture = async (file, token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const formData = new FormData();
    formData.append('file', file);

    const response = await fetch(`${IDENTITY_API_URL}/api/auth/profile-picture`, {
      method: 'POST',
      headers,
      body: formData,
    });

    const contentType = response.headers.get("content-type");
    let data = null;
    if (contentType && contentType.includes("application/json")) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || "Failed to upload profile picture. Please verify the file is a valid image under 5 MB.",
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
      message: "The identity service is currently unreachable. Please check your network and try again.",
    };
  }
};

export const getProfilePicture = async (token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/auth/profile-picture`, {
      method: 'GET',
      headers,
    });

    const contentType = response.headers.get("content-type");
    let data = null;
    if (contentType && contentType.includes("application/json")) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || "Failed to retrieve profile picture.",
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
      message: "The identity service is currently unreachable. Please check your network and try again.",
    };
  }
};

export const deleteProfilePicture = async (token) => {
  try {
    const headers = {
      'Accept': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${IDENTITY_API_URL}/api/auth/profile-picture`, {
      method: 'DELETE',
      headers,
    });

    if (response.status === 204) {
      return {
        success: true,
        status: 204,
      };
    }

    const contentType = response.headers.get("content-type");
    let data = null;
    if (contentType && contentType.includes("application/json")) {
      data = await response.json();
    }

    return {
      success: false,
      status: response.status,
      message: data?.message || "Failed to delete profile picture.",
    };
  } catch {
    return {
      success: false,
      status: 0,
      message: "The identity service is currently unreachable. Please check your network and try again.",
    };
  }
};

