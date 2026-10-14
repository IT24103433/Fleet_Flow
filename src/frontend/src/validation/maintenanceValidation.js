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

export const validateMaintenanceRecord = (record, { requireSchedule = true, requireEnd = false } = {}) => {
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

  if (requireEnd || record?.plannedEndDateTime) Object.assign(errors, validateMaintenanceWindow(record));

  const serviceInformation = String(record?.serviceInformation || '').trim();
  if (serviceInformation.length < 3) {
    errors.serviceInformation = 'Service information must contain at least 3 characters.';
  } else if (serviceInformation.length > 2000) {
    errors.serviceInformation = 'Service information cannot exceed 2000 characters.';
  }

  const cost = Number(record?.cost || 0);
  if (!Number.isFinite(cost) || cost < 0 || cost > 100000000 || !/^\d+(?:\.\d{1,2})?$/.test(String(record?.cost || '0'))) errors.cost = 'Enter a cost from zero to LKR 100,000,000, with at most two decimal places.';

  return errors;
};

export function validateMaintenanceWindow(record) {
  const start = new Date(record?.scheduledDateTime || '');
  const end = new Date(record?.plannedEndDateTime || '');
  const errors = {};
  if (!Number.isFinite(start.getTime()) || start.getTime() < Date.now() - 5 * 60000) errors.scheduledDateTime = 'Choose a current or future start time.';
  if (!Number.isFinite(end.getTime()) || !(end > start) || end - start > 365 * 86400000) errors.plannedEndDateTime = 'Choose an end after the start, within 365 days.';
  return errors;
}
