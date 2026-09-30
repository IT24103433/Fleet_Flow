const FLEET_API_URL = import.meta.env.VITE_FLEET_API_URL || 'http://localhost:5002';
const isJsonResponse = contentType => /^application\/(?:json|problem\+json)(?:\s*;|\s*$)/i.test(contentType || '');

const getAuthToken = () => {
  try {
    return sessionStorage.getItem('token') || localStorage.getItem('token');
  } catch {
    return null;
  }
};

export const createBooking = async (bookingData) => {
  try {
    const token = getAuthToken();
    if (!token) {
      return {
        success: false,
        status: 401,
        message: 'Authentication is required to create a rental booking. Please log in.',
      };
    }

    const response = await fetch(`${FLEET_API_URL}/api/bookings`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({
        vehicleId: bookingData.vehicleId,
        startDateTime: bookingData.startDateTime,
        endDateTime: bookingData.endDateTime,
      }),
    });

    const contentType = response.headers.get('content-type');
    let data = null;
    if (isJsonResponse(contentType)) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || (response.status === 409 ? 'Vehicle is already booked for the selected date range.' : 'Failed to create rental booking.'),
        errors: data?.errors || null,
      };
    }

    return {
      success: true,
      status: response.status,
      data,
    };
  } catch (error) {
    console.error('Error creating booking:', error);
    return {
      success: false,
      status: 500,
      message: 'Network error or service unavailable. Please check your connection and try again.',
    };
  }
};

export const getMyBookings = async () => {
  try {
    const token = getAuthToken();
    if (!token) {
      return {
        success: false,
        status: 401,
        message: 'Authentication required.',
        data: [],
      };
    }

    const response = await fetch(`${FLEET_API_URL}/api/bookings`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
    });

    const contentType = response.headers.get('content-type');
    let data = null;
    if (isJsonResponse(contentType)) {
      data = await response.json();
    }

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || 'Failed to fetch customer bookings.',
        data: [],
      };
    }

    return {
      success: true,
      status: response.status,
      data: Array.isArray(data) ? data : [],
    };
  } catch (error) {
    console.error('Error fetching bookings:', error);
    return {
      success: false,
      status: 500,
      message: 'Network error fetching bookings.',
      data: [],
    };
  }
};

export const cancelBooking = async (bookingId) => {
  try {
    const token = getAuthToken();
    if (!token) {
      return {
        success: false,
        status: 401,
        message: 'Authentication is required to cancel a rental booking.',
      };
    }

    const response = await fetch(`${FLEET_API_URL}/api/bookings/${bookingId}/cancel`, {
      method: 'POST',
      headers: {
        'Accept': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
    });

    const contentType = response.headers.get('content-type');
    const data = isJsonResponse(contentType) ? await response.json() : null;

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        message: data?.message || 'Failed to cancel the rental booking.',
      };
    }

    return {
      success: true,
      status: response.status,
      data,
    };
  } catch (error) {
    console.error('Error cancelling booking:', error);
    return {
      success: false,
      status: 500,
      message: 'Network error while cancelling the booking.',
    };
  }
};

export const checkVehicleAvailability = async (vehicleId, startDateTime, endDateTime) => {
  try {
    const query = new URLSearchParams({
      vehicleId,
      startDateTime,
      endDateTime,
    });

    const response = await fetch(`${FLEET_API_URL}/api/bookings/check-availability?${query.toString()}`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
    });

    const contentType = response.headers.get('content-type');
    const data = isJsonResponse(contentType) ? await response.json() : null;

    if (!response.ok) {
      return {
        success: false,
        status: response.status,
        isAvailable: false,
        message: data?.message || 'The selected vehicle is not available for that rental period.',
      };
    }

    return { success: true, status: response.status, isAvailable: Boolean(data?.isAvailable) };
  } catch (error) {
    console.error('Error checking vehicle availability:', error);
    return {
      success: false,
      status: 500,
      isAvailable: false,
      message: 'Unable to verify vehicle availability. Please try again.',
    };
  }
};
