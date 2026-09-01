import React from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../common/RoleBadge';

const StaffSidebar = ({ currentView, onNavigate, sidebarCollapsed, onToggleCollapse }) => {
  const { user, roles, logout } = useAuth();
  const primaryRole = roles?.[0] || 'FLEET_MANAGER';

  return (
    <aside className={`staff-sidebar ${sidebarCollapsed ? 'collapsed' : ''}`}>
      <div className="staff-sidebar-header">
        <div className="staff-brand-row" onClick={() => onNavigate('staff-dashboard')} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && onNavigate('staff-dashboard')}>
          <div className="brand-logo-mark small">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2">
              <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 10 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
              <circle cx="7" cy="17" r="2" />
              <path d="M9 17h6" />
              <circle cx="17" cy="17" r="2" />
            </svg>
          </div>
          {!sidebarCollapsed && (
            <div className="staff-brand-text">
              <span className="brand-name">FleetFlow</span>
              <span className="portal-subtag">Operations Portal</span>
            </div>
          )}
        </div>

        <button
          type="button"
          className="sidebar-collapse-btn"
          onClick={onToggleCollapse}
          aria-label={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
          title={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18">
            {sidebarCollapsed ? (
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 5l7 7-7 7M5 5l7 7-7 7" />
            ) : (
              <path strokeLinecap="round" strokeLinejoin="round" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
            )}
          </svg>
        </button>
      </div>

      {/* Main Staff Navigation */}
      <nav className="staff-sidebar-nav" aria-label="Staff Navigation">
        <div className="nav-section-label">{!sidebarCollapsed && 'OPERATIONS'}</div>

        <button
          type="button"
          className={`staff-nav-item ${currentView === 'staff-dashboard' ? 'active' : ''}`}
          onClick={() => onNavigate('staff-dashboard')}
          title="Operations Dashboard"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="nav-item-icon">
            <rect x="3" y="3" width="7" height="9" rx="1" />
            <rect x="14" y="3" width="7" height="5" rx="1" />
            <rect x="14" y="12" width="7" height="9" rx="1" />
            <rect x="3" y="16" width="7" height="5" rx="1" />
          </svg>
          {!sidebarCollapsed && <span className="nav-item-label">Dashboard</span>}
        </button>

        <button
          type="button"
          className="staff-nav-item nav-item-disabled"
          title="Fleet Management (Phase 2 integration)"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="nav-item-icon">
            <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
            <circle cx="7" cy="17" r="2" />
            <circle cx="17" cy="17" r="2" />
          </svg>
          {!sidebarCollapsed && (
            <div className="nav-item-label-group">
              <span className="nav-item-label">Manage Fleet</span>
              <span className="feature-pill">Phase 2</span>
            </div>
          )}
        </button>

        <button
          type="button"
          className="staff-nav-item nav-item-disabled"
          title="Add New Vehicle (Phase 2 integration)"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="nav-item-icon">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="16" />
            <line x1="8" y1="12" x2="16" y2="12" />
          </svg>
          {!sidebarCollapsed && (
            <div className="nav-item-label-group">
              <span className="nav-item-label">Add Vehicle</span>
              <span className="feature-pill">Phase 2</span>
            </div>
          )}
        </button>

        <div className="nav-section-label">{!sidebarCollapsed && 'PORTAL SWITCH'}</div>
        <button
          type="button"
          className="staff-nav-item"
          onClick={() => onNavigate('landing')}
          title="Switch to Customer Portal"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="nav-item-icon">
            <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
          </svg>
          {!sidebarCollapsed && <span className="nav-item-label">Customer Portal</span>}
        </button>
      </nav>

      {/* Staff Profile & Logout */}
      <div className="staff-sidebar-footer">
        {!sidebarCollapsed && (
          <div className="staff-user-card">
            <div className="staff-user-avatar">
              {user?.username?.charAt(0)?.toUpperCase() || 'S'}
            </div>
            <div className="staff-user-meta">
              <span className="staff-username" title={user?.username}>{user?.username}</span>
              <RoleBadge role={primaryRole} />
            </div>
          </div>
        )}
        <button
          type="button"
          className="staff-logout-btn"
          onClick={logout}
          title="Log out of Staff Portal"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18">
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
          {!sidebarCollapsed && <span>Log Out</span>}
        </button>
      </div>
    </aside>
  );
};

export default StaffSidebar;
