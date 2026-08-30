const IDENTITY_API_URL = import.meta.env.VITE_IDENTITY_API_URL || 'http://localhost:5001';

export const registerUser = async (username, email, password) => {
  try {
    const response = await fetch(`${IDENTITY_API_URL}/api/auth/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ username, email, password }),
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
    return {
      success: false,
      status: 0,
      message: "The authentication server is currently unreachable. Please check your network and try again."
    };
  }
};

export const loginUser = async (usernameOrEmail, password) => {
  try {
    const response = await fetch(`${IDENTITY_API_URL}/api/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ usernameOrEmail, password }),
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
    return {
      success: false,
      status: 0,
      message: "The authentication server is currently unreachable. Please check your network and try again."
    };
  }
};

