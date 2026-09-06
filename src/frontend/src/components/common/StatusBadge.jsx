import React from 'react';

const STATUS_CONFIGS = {
  AVAILABLE: {
    label: 'Available',
    badgeClass: 'status-badge-available',
  },
  IN_USE: {
    label: 'In Use',
    badgeClass: 'status-badge-in-use',
  },
  INUSE: {
    label: 'In Use',
    badgeClass: 'status-badge-in-use',
  },
  RENTED: {
    label: 'Rented',
    badgeClass: 'status-badge-in-use',
  },
  MAINTENANCE: {
    label: 'Maintenance',
    badgeClass: 'status-badge-maintenance',
  },
  RETIRED: {
    label: 'Retired',
    badgeClass: 'status-badge-retired',
  },
};

const StatusBadge = ({ status, className = '' }) => {
  const normalized = typeof status === 'string' ? status.toUpperCase().replace(/\s+/g, '_') : '';
  const config = STATUS_CONFIGS[normalized] || {
    label: status || 'Unknown',
    badgeClass: 'status-badge-default',
  };

  return (
    <span className={`status-badge ${config.badgeClass} ${className}`.trim()}>
      <span className="status-badge-dot" aria-hidden="true" />
      <span>{config.label}</span>
    </span>
  );
};

export default StatusBadge;
