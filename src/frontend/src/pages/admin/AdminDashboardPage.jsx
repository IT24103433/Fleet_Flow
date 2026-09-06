import React from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';

const AdminDashboardPage = ({ onNavigate }) => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'ADMIN';

  return (
    <div className="admin-dashboard-container">
      {/* Admin Header Card */}
      <div className="admin-welcome-card">
        <div className="admin-welcome-left">
          <div className="admin-security-pill">
            <span className="admin-pulse-dot" aria-hidden="true" />
            <span>Administrator Session Active</span>
          </div>
          <h1 className="admin-welcome-title">System Administration & RBAC Control</h1>
          <p className="admin-welcome-subtitle">
            Centralized governance for user provisioning, role-based access control, security policies, and identity audit logs.
          </p>
          <div className="admin-user-meta-row">
            <span className="admin-logged-as">Admin User: <strong>{user?.username}</strong></span>
            <RoleBadge role={primaryRole} />
          </div>
        </div>

        <div className="admin-header-actions">
          <Button variant="primary" size="md" onClick={() => onNavigate('admin-create-user')}>
            + Provision New User
          </Button>
          <Button variant="outline" size="md" onClick={() => onNavigate('admin-users')}>
            Manage User Directory
          </Button>
        </div>
      </div>

      {/* Identity & Role Distribution Cards */}
      <div className="admin-stats-grid">
        <div className="admin-stat-card">
          <div className="stat-top">
            <span className="stat-title">RBAC Roles Defined</span>
            <span className="stat-badge">System Verified</span>
          </div>
          <span className="stat-number">4</span>
          <span className="stat-detail">CUSTOMER, FLEET_MGR, MAINT, ADMIN</span>
        </div>

        <div className="admin-stat-card">
          <div className="stat-top">
            <span className="stat-title">Identity Authority</span>
            <span className="stat-badge success">Online</span>
          </div>
          <span className="stat-number">Active</span>
          <span className="stat-detail">ASP.NET Identity Core + EF Core</span>
        </div>

        <div className="admin-stat-card">
          <div className="stat-top">
            <span className="stat-title">Token Security</span>
            <span className="stat-badge success">Enforced</span>
          </div>
          <span className="stat-number">HS256</span>
          <span className="stat-detail">Signed JWT with ClaimTypes.Role</span>
        </div>

        <div className="admin-stat-card">
          <div className="stat-top">
            <span className="stat-title">Audit Log Status</span>
            <span className="stat-badge">Phase 2 API</span>
          </div>
          <span className="stat-number">—</span>
          <span className="stat-detail">Awaiting audit persistence stream</span>
        </div>
      </div>

      {/* Administration Panels Grid */}
      <div className="admin-panels-grid">
        {/* Quick Management Shortcuts */}
        <div className="admin-panel">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Identity Management Shortcuts</h3>
          </div>

          <div className="admin-shortcuts-list">
            <button
              type="button"
              className="admin-shortcut-btn"
              onClick={() => onNavigate('admin-users')}
            >
              <div className="shortcut-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                </svg>
              </div>
              <div className="shortcut-meta">
                <strong>User Directory & Security Claims</strong>
                <p>Inspect user accounts, inspect roles, reset passwords, or force password changes.</p>
              </div>
            </button>

            <button
              type="button"
              className="admin-shortcut-btn"
              onClick={() => onNavigate('admin-create-user')}
            >
              <div className="shortcut-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" />
                </svg>
              </div>
              <div className="shortcut-meta">
                <strong>Provision Staff / Customer Account</strong>
                <p>Create a new account with specific role assignment and temporary credentials.</p>
              </div>
            </button>
          </div>
        </div>

        {/* System Health & Verification Status */}
        <div className="admin-panel">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Role & Permission Policy Matrix</h3>
            <span className="read-only-badge">Strict 4-Role Model</span>
          </div>

          <div className="policy-matrix-list">
            <div className="policy-row">
              <div className="policy-role-cell">
                <RoleBadge role="ADMIN" />
              </div>
              <span className="policy-desc">Full access to User Management, Identity Seeding, Fleet, Maintenance</span>
            </div>
            <div className="policy-row">
              <div className="policy-role-cell">
                <RoleBadge role="FLEET_MANAGER" />
              </div>
              <span className="policy-desc">Authorized for Fleet Ingestion, Vehicle Specs, Dispatch, and Status updates</span>
            </div>
            <div className="policy-row">
              <div className="policy-role-cell">
                <RoleBadge role="MAINTENANCE_STAFF" />
              </div>
              <span className="policy-desc">Authorized for Service Queue inspection, Maintenance logging, Repair status</span>
            </div>
            <div className="policy-row">
              <div className="policy-role-cell">
                <RoleBadge role="CUSTOMER" />
              </div>
              <span className="policy-desc">Consumer self-service, Vehicle browsing, Reservation creation, Personal profile</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AdminDashboardPage;
