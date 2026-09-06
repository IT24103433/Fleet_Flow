import React from 'react';

const ROLE_CONFIGS = {
  CUSTOMER: {
    label: 'Customer',
    badgeClass: 'role-badge-customer',
  },
  FLEET_MANAGER: {
    label: 'Fleet Manager',
    badgeClass: 'role-badge-fleet-manager',
  },
  MAINTENANCE_STAFF: {
    label: 'Maintenance Staff',
    badgeClass: 'role-badge-maintenance-staff',
  },
  ADMIN: {
    label: 'Administrator',
    badgeClass: 'role-badge-admin',
  },
};

const RoleBadge = ({ role, className = '' }) => {
  const normalizedRole = typeof role === 'string' ? role.toUpperCase().trim() : '';
  const config = ROLE_CONFIGS[normalizedRole] || {
    label: role || 'User',
    badgeClass: 'role-badge-default',
  };

  return (
    <span className={`role-badge ${config.badgeClass} ${className}`.trim()}>
      <span className="role-badge-dot" aria-hidden="true" />
      <span>{config.label}</span>
    </span>
  );
};

export default RoleBadge;
