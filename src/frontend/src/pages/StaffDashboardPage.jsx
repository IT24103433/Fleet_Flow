import React from 'react';
import { useAuth } from '../context/AuthContext';
import RoleBadge from '../components/common/RoleBadge';

const StaffDashboardPage = ({ onNavigate }) => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'FLEET_MANAGER';

  const getRoleResponsibilities = (role) => {
    switch (role) {
      case 'ADMIN':
        return 'Full administrative oversight across identity management, cross-service auditing, and fleet system configuration.';
      case 'FLEET_MANAGER':
        return 'Authorized for vehicle inventory ingestion, vehicle dispatch, schedule management, and operational tracking.';
      case 'MAINTENANCE_STAFF':
        return 'Authorized for recording maintenance tasks, updating vehicle service health status, and managing repair logs.';
      default:
        return 'Standard staff operational access.';
    }
  };

  return (
    <div className="staff-dashboard-container">
      {/* Staff Welcome Banner */}
      <div className="staff-welcome-card">
        <div className="staff-welcome-content">
          <div className="welcome-tag">
            <span className="live-indicator-dot" aria-hidden="true" />
            <span>Staff Session Authenticated</span>
          </div>
          <h2 className="welcome-title">Welcome back, {user?.username || 'Staff Member'}</h2>
          <p className="welcome-subtitle">
            {getRoleResponsibilities(primaryRole)}
          </p>
          <div className="staff-role-chip-row">
            <span className="role-label-text">Active Authorization Level:</span>
            <RoleBadge role={primaryRole} />
          </div>
        </div>
      </div>

      {/* Operational Metrics Placeholders (Awaiting FleetService API) */}
      <div className="dashboard-section-header">
        <h3 className="section-title">Fleet Operations Overview</h3>
        <span className="data-source-badge">Awaiting FleetService API (Phase 2)</span>
      </div>

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

      {/* Operational Modules & Implementation Status */}
      <div className="operations-two-col">
        {/* Module Status Card */}
        <div className="dashboard-panel-card">
          <div className="panel-card-header">
            <h4 className="panel-title">Operations Module Status</h4>
            <span className="panel-status-tag">Sprint 1 Verification</span>
          </div>

          <div className="module-status-list">
            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">Identity & Role Seeding</strong>
                  <p className="module-desc">Four-role RBAC seeding verified in database</p>
                </div>
              </div>
              <span className="module-badge ready">Operational</span>
            </div>

            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">JWT Authentication</strong>
                  <p className="module-desc">Signed 256-bit tokens with ClaimTypes.Role</p>
                </div>
              </div>
              <span className="module-badge ready">Operational</span>
            </div>

            <div className="module-status-item ready">
              <div className="module-item-left">
                <span className="status-indicator-dot ready" aria-hidden="true" />
                <div>
                  <strong className="module-name">Staff Portal Gate</strong>
                  <p className="module-desc">Customer role filtering enforced on entry</p>
                </div>
              </div>
              <span className="module-badge ready">Operational</span>
            </div>

            <div className="module-status-item pending">
              <div className="module-item-left">
                <span className="status-indicator-dot pending" aria-hidden="true" />
                <div>
                  <strong className="module-name">Vehicle Management API</strong>
                  <p className="module-desc">Fleet ingestion & inventory endpoints</p>
                </div>
              </div>
              <span className="module-badge pending">Next Sprint</span>
            </div>
          </div>
        </div>

        {/* Quick Operations Actions Card */}
        <div className="dashboard-panel-card">
          <div className="panel-card-header">
            <h4 className="panel-title">Fast Actions & Shortcuts</h4>
          </div>

          <div className="shortcut-actions-list">
            <button
              type="button"
              className="shortcut-action-btn disabled"
              title="Fleet Management API will be wired in Phase 2"
            >
              <div className="shortcut-icon-box">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
                  <circle cx="7" cy="17" r="2" />
                  <circle cx="17" cy="17" r="2" />
                </svg>
              </div>
              <div className="shortcut-text">
                <span className="shortcut-title">Manage Fleet Inventory</span>
                <span className="shortcut-subtitle">View, filter, and inspect registered vehicles (Phase 2)</span>
              </div>
              <span className="shortcut-badge">Phase 2</span>
            </button>

            <button
              type="button"
              className="shortcut-action-btn disabled"
              title="Vehicle Ingestion API will be wired in Phase 2"
            >
              <div className="shortcut-icon-box">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="16" />
                  <line x1="8" y1="12" x2="16" y2="12" />
                </svg>
              </div>
              <div className="shortcut-text">
                <span className="shortcut-title">Add New Vehicle</span>
                <span className="shortcut-subtitle">Ingest vehicle into inventory database (Phase 2)</span>
              </div>
              <span className="shortcut-badge">Phase 2</span>
            </button>

            <button
              type="button"
              className="shortcut-action-btn"
              onClick={() => onNavigate('landing')}
            >
              <div className="shortcut-icon-box">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                </svg>
              </div>
              <div className="shortcut-text">
                <span className="shortcut-title">Preview Customer Portal</span>
                <span className="shortcut-subtitle">View public vehicle catalog and customer experience</span>
              </div>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default StaffDashboardPage;
