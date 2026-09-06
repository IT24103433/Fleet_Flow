import React from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../common/RoleBadge';

const StaffHeader = ({ title = 'Operations Dashboard', onToggleSidebar }) => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'FLEET_MANAGER';

  const todayFormatted = new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  }).format(new Date());

  return (
    <header className="staff-header">
      <div className="staff-header-left">
        <button
          type="button"
          className="staff-mobile-toggle-btn"
          onClick={onToggleSidebar}
          aria-label="Toggle navigation sidebar"
        >
          <svg viewBox="0 0 24 24" width="22" height="22" stroke="currentColor" strokeWidth="2" fill="none">
            <path d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
        <div className="staff-title-group">
          <h1 className="staff-header-title">{title}</h1>
          <span className="staff-date-text">{todayFormatted}</span>
        </div>
      </div>

      <div className="staff-header-right">
        <div className="system-health-badge" title="Identity & Fleet Services Connected">
          <span className="pulse-dot" aria-hidden="true" />
          <span>RBAC Active</span>
        </div>

        <div className="staff-header-profile">
          <RoleBadge role={primaryRole} />
          <span className="staff-header-user">{user?.username}</span>
        </div>
      </div>
    </header>
  );
};

export default StaffHeader;
