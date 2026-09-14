export const SUPPORTED_VEHICLE_STATUSES = [
  {
    value: 'Available',
    label: 'Available',
    description: 'Ready for customer booking, dispatch, and active road use.',
  },
  {
    value: 'InUse',
    label: 'In Use',
    description: 'Currently assigned, dispatched, or rented to an active customer trip.',
  },
  {
    value: 'Maintenance',
    label: 'Maintenance',
    description: 'Under inspection, servicing, mechanical repair, or workshop conditioning.',
  },
  {
    value: 'Retired',
    label: 'Retired',
    description: 'Decommissioned or permanently removed from the commercial fleet catalog.',
  },
];

export const isValidVehicleStatus = (status) => {
  return SUPPORTED_VEHICLE_STATUSES.some((s) => s.value === status);
};

export const getAvailableStatusTransitionsForRoles = (roles = []) => {
  const normalized = (roles || []).map((r) => String(r).toUpperCase().trim());
  const isManagerOrAdmin = normalized.includes('FLEET_MANAGER') || normalized.includes('ADMIN');
  const isMaintenanceStaff = normalized.includes('MAINTENANCE_STAFF');

  if (isManagerOrAdmin) {
    return SUPPORTED_VEHICLE_STATUSES;
  }

  if (isMaintenanceStaff) {
    return SUPPORTED_VEHICLE_STATUSES.filter(
      (s) => s.value === 'Available' || s.value === 'Maintenance'
    );
  }

  return [];
};

export const canUserChangeStatus = (roles = []) => {
  const allowed = getAvailableStatusTransitionsForRoles(roles);
  return allowed.length > 0;
};

export const validateStatusChange = (newStatus, currentStatus, roles = []) => {
  if (!newStatus) {
    return { isValid: false, error: 'Please select a valid operational status.' };
  }

  if (!isValidVehicleStatus(newStatus)) {
    return {
      isValid: false,
      error: `Invalid status "${newStatus}". Supported statuses are: Available, InUse, Maintenance, Retired.`,
    };
  }

  const allowedStatuses = getAvailableStatusTransitionsForRoles(roles);
  if (allowedStatuses.length === 0) {
    return {
      isValid: false,
      error: 'You do not have permission to change vehicle operational status.',
    };
  }

  const isAllowed = allowedStatuses.some((s) => s.value === newStatus);
  if (!isAllowed) {
    return {
      isValid: false,
      error: `Your role does not have permission to transition vehicles to "${newStatus}". Maintenance staff can only update operational health statuses (Available, Maintenance).`,
    };
  }

  if (newStatus === currentStatus) {
    return { isValid: false, error: `Vehicle is already in "${newStatus}" status.` };
  }

  return { isValid: true, error: null };
};
