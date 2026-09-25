export const validateBookingDates = (startDateTime, endDateTime) => {
  const errors = {};

  if (!startDateTime) {
    errors.startDateTime = 'Start date and time is required.';
  }

  if (!endDateTime) {
    errors.endDateTime = 'End date and time is required.';
  }

  if (startDateTime && endDateTime) {
    const startDateObj = new Date(startDateTime);
    const endDateObj = new Date(endDateTime);
    const now = new Date();

    if (isNaN(startDateObj.getTime())) {
      errors.startDateTime = 'Invalid start date format.';
    } else if (startDateObj < new Date(now.getTime() - 5 * 60000)) {
      errors.startDateTime = 'Start date and time must be in the future.';
    }

    if (isNaN(endDateObj.getTime())) {
      errors.endDateTime = 'Invalid end date format.';
    } else if (endDateObj <= startDateObj) {
      errors.endDateTime = 'End date and time must be strictly after start date and time.';
    }
  }

  return errors;
};

export const calculateRentalCost = (startDateTime, endDateTime, dailyRate) => {
  if (!startDateTime || !endDateTime || !dailyRate || isNaN(Number(dailyRate))) {
    return { totalDays: 0, totalCost: 0 };
  }

  const startMs = new Date(startDateTime).getTime();
  const endMs = new Date(endDateTime).getTime();

  if (isNaN(startMs) || isNaN(endMs) || endMs <= startMs) {
    return { totalDays: 0, totalCost: 0 };
  }

  const diffHours = (endMs - startMs) / (1000 * 60 * 60);
  const totalDays = Math.max(1, Math.ceil(diffHours / 24));
  const totalCost = totalDays * Number(dailyRate);

  return { totalDays, totalCost };
};
