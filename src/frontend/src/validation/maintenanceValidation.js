export const MAINTENANCE_STATUSES = ['SCHEDULED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'];

const TRANSITIONS = {
  SCHEDULED: ['IN_PROGRESS', 'CANCELLED'],
  IN_PROGRESS: ['COMPLETED', 'CANCELLED'],
  COMPLETED: [],
  CANCELLED: [],
};

export const getMaintenanceStatusTransitions = (status) => TRANSITIONS[String(status || '').toUpperCase()] || [];

export const isTerminalMaintenanceStatus = (status) => {
  const normalized = String(status || '').toUpperCase();
  return normalized === 'COMPLETED' || normalized === 'CANCELLED';
};

export const validateMaintenanceRecord = (record, { requireSchedule = true } = {}) => {
  const errors = {};

  if (!record?.vehicleId) errors.vehicleId = 'Select a vehicle.';

  if (requireSchedule) {
    const scheduled = new Date(record?.scheduledDateTime || '');
    if (!record?.scheduledDateTime || Number.isNaN(scheduled.getTime())) {
      errors.scheduledDateTime = 'Enter a valid maintenance date and time.';
    } else if (scheduled < new Date(Date.now() - 5 * 60 * 1000)) {
      errors.scheduledDateTime = 'Maintenance date and time must be current or in the future.';
    }
  }

  const serviceInformation = String(record?.serviceInformation || '').trim();
  if (serviceInformation.length < 3) {
    errors.serviceInformation = 'Service information must contain at least 3 characters.';
  } else if (serviceInformation.length > 2000) {
    errors.serviceInformation = 'Service information cannot exceed 2000 characters.';
  }

  const cost = Number(record?.cost || 0);
  if (!Number.isFinite(cost) || cost < 0) errors.cost = 'Cost must be zero or greater.';

  return errors;
};
