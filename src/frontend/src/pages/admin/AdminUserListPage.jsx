import React, { useState } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import InputField from '../../components/InputField';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';

const INITIAL_USERS = [
  {
    id: 'u-1',
    username: 'admin_sarah',
    email: 'sarah.admin@fleetflow.io',
    role: 'ADMIN',
    status: 'ACTIVE',
    createdAt: '2026-08-15',
    lastLogin: '2026-09-01',
  },
  {
    id: 'u-2',
    username: 'manager_dan',
    email: 'dan.ops@fleetflow.io',
    role: 'FLEET_MANAGER',
    status: 'ACTIVE',
    createdAt: '2026-08-18',
    lastLogin: '2026-09-01',
  },
  {
    id: 'u-3',
    username: 'tech_mike',
    email: 'mike.service@fleetflow.io',
    role: 'MAINTENANCE_STAFF',
    status: 'ACTIVE',
    createdAt: '2026-08-20',
    lastLogin: '2026-08-31',
  },
  {
    id: 'u-4',
    username: 'customer_alex',
    email: 'alex.mobility@example.com',
    role: 'CUSTOMER',
    status: 'ACTIVE',
    createdAt: '2026-08-25',
    lastLogin: '2026-09-01',
  },
];

const AdminUserListPage = ({ onNavigate, onSelectUser }) => {
  const [users] = useState(INITIAL_USERS);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRoleFilter, setSelectedRoleFilter] = useState('ALL');

  // Reset Password Modal state
  const [resetModalUser, setResetModalUser] = useState(null);
  const [tempPassword, setTempPassword] = useState('');
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [resetNotice, setResetNotice] = useState(null);

  const filteredUsers = users.filter((u) => {
    const matchesSearch =
      u.username.toLowerCase().includes(searchTerm.toLowerCase()) ||
      u.email.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesRole = selectedRoleFilter === 'ALL' || u.role === selectedRoleFilter;
    return matchesSearch && matchesRole;
  });

  const handleOpenReset = (user) => {
    setResetModalUser(user);
    setTempPassword('Temp#' + Math.random().toString(36).substring(2, 8).toUpperCase() + '!');
    setResetNotice(null);
  };

  const handleExecuteReset = (e) => {
    e.preventDefault();
    setResetNotice({
      type: 'success',
      title: 'Password Reset Generated',
      message: `Temporary password generated for ${resetModalUser.username}. Force password change on next login: ${forceChangeToggle ? 'Enabled' : 'Disabled'}. (Backend integration ready for Sprint 2).`,
    });
  };

  return (
    <div className="admin-users-container">
      {/* Page Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">User Directory & Role Management</h1>
          <p className="admin-page-subtitle">
            Inspect registered accounts, verify assigned security claims, and perform administrative credential management.
          </p>
        </div>
        <Button variant="primary" onClick={() => onNavigate('admin-create-user')}>
          + Provision New User
        </Button>
      </div>

      {/* Filter and Search Bar */}
      <div className="users-filter-bar">
        <div className="search-box-wrapper">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            placeholder="Search by username or email..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="filter-search-input"
          />
        </div>

        <div className="role-filter-group">
          <label htmlFor="roleFilter" className="filter-label">Role Filter:</label>
          <select
            id="roleFilter"
            value={selectedRoleFilter}
            onChange={(e) => setSelectedRoleFilter(e.target.value)}
            className="filter-select-input"
          >
            <option value="ALL">All Roles ({users.length})</option>
            <option value="ADMIN">Administrators</option>
            <option value="FLEET_MANAGER">Fleet Managers</option>
            <option value="MAINTENANCE_STAFF">Maintenance Staff</option>
            <option value="CUSTOMER">Customers</option>
          </select>
        </div>
      </div>

      {/* Users Table */}
      <div className="table-card">
        <div className="table-responsive-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Role</th>
                <th>Status</th>
                <th>Created Date</th>
                <th>Last Active</th>
                <th style={{ textAlign: 'right' }}>Security Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map((u) => (
                <tr key={u.id}>
                  <td>
                    <div className="user-cell-meta">
                      <div className="user-table-avatar">
                        {u.username.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <strong className="user-name-text">{u.username}</strong>
                        <p className="user-email-sub">{u.email}</p>
                      </div>
                    </div>
                  </td>
                  <td>
                    <RoleBadge role={u.role} />
                  </td>
                  <td>
                    <span className="status-pill-active">{u.status}</span>
                  </td>
                  <td>
                    <span className="table-date">{u.createdAt}</span>
                  </td>
                  <td>
                    <span className="table-date">{u.lastLogin}</span>
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div className="table-action-btns">
                      <button
                        type="button"
                        className="btn-table-action"
                        onClick={() => {
                          if (onSelectUser) onSelectUser(u);
                          onNavigate('admin-user-details');
                        }}
                        title="View User Details"
                      >
                        Inspect
                      </button>
                      <button
                        type="button"
                        className="btn-table-action warning"
                        onClick={() => handleOpenReset(u)}
                        title="Reset User Password"
                      >
                        Reset Password
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Administrative Password Reset Modal */}
      <Modal
        isOpen={!!resetModalUser}
        onClose={() => setResetModalUser(null)}
        title="Admin Reset Password"
        subtitle={`Generate a secure temporary password for ${resetModalUser?.username}.`}
      >
        {resetNotice && <Alert type={resetNotice.type} title={resetNotice.title} message={resetNotice.message} />}

        <form onSubmit={handleExecuteReset}>
          <div className="temp-password-box">
            <span className="temp-label">Generated Temporary Password:</span>
            <code className="temp-code">{tempPassword}</code>
            <p className="temp-desc">Provide this temporary password securely to the user.</p>
          </div>

          <div className="checkbox-field-row">
            <input
              type="checkbox"
              id="forcePasswordChange"
              checked={forceChangeToggle}
              onChange={(e) => setForceChangeToggle(e.target.checked)}
            />
            <label htmlFor="forcePasswordChange">
              <strong>Force password change on next login</strong>
              <span>User will be prompted to set a new password upon authentication.</span>
            </label>
          </div>

          <div className="modal-actions-row">
            <Button variant="outline" onClick={() => setResetModalUser(null)}>
              Close
            </Button>
            <Button type="submit" variant="primary">
              Confirm Reset
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default AdminUserListPage;
