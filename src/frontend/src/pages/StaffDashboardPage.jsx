import React from 'react';
import { useAuth } from '../context/AuthContext';
import RoleBadge from '../components/common/RoleBadge';

const StaffDashboardPage = ({ onNavigate }) => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'FLEET_MANAGER';
  const isMaintenanceStaff = primaryRole === 'MAINTENANCE_STAFF';

  return (
    <div className="staff-dashboard-container">
      {/* Staff Welcome Banner */}
      <div className="staff-welcome-card">
        <div className="staff-welcome-content">
          <div className="welcome-tag">
            <span className="live-indicator-dot" aria-hidden="true" />
            <span>Staff Session Authenticated</span>
          </div>
          <h2 className="welcome-title">
            {isMaintenanceStaff ? 'Maintenance & Service Workspace' : `Welcome back, ${user?.username || 'Fleet Operations'}`}
          </h2>
          <p className="welcome-subtitle">
            {isMaintenanceStaff
              ? 'Authorized for recording maintenance tasks, inspecting vehicle health statuses, and managing service logs.'
              : 'Authorized for vehicle inventory ingestion, vehicle dispatch, schedule management, and operational tracking.'}
          </p>
          <div className="staff-role-chip-row">
            <span className="role-label-text">Active Authorization Level:</span>
            <RoleBadge role={primaryRole} />
          </div>
        </div>
      </div>

      {/* Operational Metrics Placeholders (Awaiting FleetService API) */}
      <div className="dashboard-section-header">
        <h3 className="section-title">
          {isMaintenanceStaff ? 'Maintenance Work Queue Overview' : 'Fleet Operations Overview'}
        </h3>
        <span className="data-source-badge">Awaiting FleetService API (Sprint 2)</span>
      </div>

      {isMaintenanceStaff ? (
        <div className="metrics-grid">
          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Pending Inspections</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting inspection queue</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Vehicles in Service</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting workshop logs</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Scheduled Repairs</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting repair schedule</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Completed This Month</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting historical logs</span>
            </div>
          </div>
        </div>
      ) : (
        <div className="metrics-grid">
          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Total Fleet Size</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting fleet database</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Active Rentals</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting booking service</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">In Maintenance</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting service logs</span>
            </div>
          </div>

          <div className="stat-card placeholder-card">
            <div className="stat-card-header">
              <span className="stat-label">Utilization Rate</span>
              <span className="stat-badge-phase">Phase 2</span>
            </div>
            <div className="stat-value-placeholder">
              <span className="placeholder-dash">—</span>
              <span className="placeholder-sub">Awaiting analytics backend</span>
            </div>
          </div>
        </div>
      )}

      {/* Operational Modules & Implementation Status */}
      <div className="operations-two-col">
        {/* Module Status Card */}
        <div className="dashboard-panel-card">
          <div className="panel-card-header">
            <h4 className="panel-title">Operations Services Status</h4>
            <span className="panel-status-tag">Operational</span>
          </div>

          <div className="module-status-list">
            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">Fleet Inventory Management</strong>
                  <p className="module-desc">Vehicle catalog, availability, and pricing management</p>
                </div>
              </div>
              <span className="module-badge ready">Active</span>
            </div>

            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">Staff Access & Permissions</strong>
                  <p className="module-desc">Role-authorized operational workspace controls</p>
                </div>
              </div>
              <span className="module-badge ready">Active</span>
            </div>

            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">Operations Console</strong>
                  <p className="module-desc">Unified interface for fleet monitoring and controls</p>
                </div>
              </div>
              <span className="module-badge ready">Active</span>
            </div>

            <div className="module-status-item pending">
              <div className="module-item-left">
                <span className="status-indicator-dot pending" aria-hidden="true" />
                <div>
                  <strong className="module-name">Maintenance & Telematics</strong>
                  <p className="module-desc">Vehicle maintenance scheduling and status tracking</p>
                </div>
              </div>
              <span className="module-badge pending">Planned</span>
            </div>
          </div>
        </div>

        {/* Quick Operations Actions Card */}
        <div className="dashboard-panel-card">
          <div className="panel-card-header">
            <h4 className="panel-title">Operations Shortcuts</h4>
          </div>

          <div className="shortcut-actions-list">
            <button
              type="button"
              className="shortcut-action-btn"
              onClick={() => onNavigate('staff-profile')}
            >
              <div className="shortcut-icon-box">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
              </div>
              <div className="shortcut-text">
                <span className="shortcut-title">Staff Profile & Credentials</span>
                <span className="shortcut-subtitle">View staff identity, update station details, or change password</span>
              </div>
            </button>

            {primaryRole === 'ADMIN' && (
              <button
                type="button"
                className="shortcut-action-btn"
                onClick={() => onNavigate('admin-dashboard')}
              >
                <div className="shortcut-icon-box">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                </div>
                <div className="shortcut-text">
                  <span className="shortcut-title">Administrator Workspace</span>
                  <span className="shortcut-subtitle">Manage user accounts, roles, and administrative resets</span>
                </div>
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default StaffDashboardPage;
