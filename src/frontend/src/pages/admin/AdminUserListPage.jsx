import React, { useState, useEffect } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getAdminUsers, deleteAdminUser } from '../../services/adminUserService';

const AdminUserListPage = ({ onNavigate, onSelectUser }) => {
  const { token } = useAuth();
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [fetchError, setFetchError] = useState(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRoleFilter, setSelectedRoleFilter] = useState('ALL');

  // Reset Password Modal state
  const [resetModalUser, setResetModalUser] = useState(null);
  const [tempPassword, setTempPassword] = useState('');
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [resetNotice, setResetNotice] = useState(null);

  // Delete User Modal state
  const [deleteModalUser, setDeleteModalUser] = useState(null);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);
  const [actionSuccessNotice, setActionSuccessNotice] = useState(null);

  useEffect(() => {
    let isMounted = true;
    const fetchUsers = async () => {
      const res = await getAdminUsers(token);
      if (isMounted) {
        setIsLoading(false);
        if (res.success) {
          setUsers(res.data);
        } else {
          setFetchError(res.message);
        }
      }
    };

    fetchUsers();

    return () => {
      isMounted = false;
    };
  }, [token]);

  const filteredUsers = users.filter((u) => {
    const term = searchTerm.toLowerCase();
    const matchesSearch =
      (u.username && u.username.toLowerCase().includes(term)) ||
      (u.email && u.email.toLowerCase().includes(term)) ||
      (u.fullName && u.fullName.toLowerCase().includes(term));
    const userRole = u.role || (u.roles && u.roles[0]) || '';
    const matchesRole = selectedRoleFilter === 'ALL' || userRole.toUpperCase() === selectedRoleFilter.toUpperCase();
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
      type: 'info',
      title: 'Password Reset (Sprint 2 Roadmap)',
      message: `Simulated temporary password generated for ${resetModalUser.username}. Live administrative password reset API will connect in Sprint 2.`,
    });
  };

  const handleOpenDelete = (user, e) => {
    e.stopPropagation();
    setDeleteModalUser(user);
    setDeleteConfirmText('');
    setDeleteError(null);
  };

  const handleCloseDelete = () => {
    if (isDeleting) return;
    setDeleteModalUser(null);
    setDeleteConfirmText('');
    setDeleteError(null);
  };

  const handleExecuteDelete = async (e) => {
    e.preventDefault();
    if (deleteConfirmText !== 'DELETE') return;
    if (!deleteModalUser) return;

    setIsDeleting(true);
    setDeleteError(null);

    const res = await deleteAdminUser(deleteModalUser.id, token);
    setIsDeleting(false);

    if (res.success) {
      setUsers((prev) => prev.filter((u) => u.id !== deleteModalUser.id));
      setActionSuccessNotice(`User account "${deleteModalUser.username}" was permanently deleted.`);
      setDeleteModalUser(null);
      setDeleteConfirmText('');
    } else {
      setDeleteError(res.message || 'Failed to delete user account.');
    }
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

      {actionSuccessNotice && (
        <Alert
          type="success"
          title="Account Deleted"
          message={actionSuccessNotice}
        />
      )}

      {fetchError && (
        <Alert
          type="error"
          title="Directory Retrieval Error"
          message={fetchError}
        />
      )}

      {/* Filter and Search Bar */}
      <div className="users-filter-bar">
        <div className="search-box-wrapper">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            placeholder="Search by username, email, or name..."
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
              {isLoading ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '2.5rem' }}>
                    <p style={{ color: 'var(--text-secondary, #94a3b8)' }}>Loading user directory from IdentityService...</p>
                  </td>
                </tr>
              ) : filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '2.5rem' }}>
                    <p style={{ color: 'var(--text-secondary, #94a3b8)' }}>
                      {users.length === 0
                        ? 'No user accounts found in the database.'
                        : 'No accounts match the current search or role filter.'}
                    </p>
                  </td>
                </tr>
              ) : (
                filteredUsers.map((u) => {
                  const role = u.role || (u.roles && u.roles[0]) || 'CUSTOMER';
                  const createdStr = u.createdAt
                    ? new Date(u.createdAt).toISOString().split('T')[0]
                    : '—';

                  return (
                    <tr key={u.id}>
                      <td>
                        <div className="user-cell-meta">
                          <div className="user-table-avatar">
                            {(u.fullName || u.username || 'U').charAt(0).toUpperCase()}
                          </div>
                          <div>
                            <strong className="user-name-text">{u.fullName || u.username}</strong>
                            <p className="user-email-sub">{u.username !== u.fullName ? `@${u.username} • ` : ''}{u.email}</p>
                          </div>
                        </div>
                      </td>
                      <td>
                        <RoleBadge role={role} />
                      </td>
                      <td>
                        <span className="status-pill-active">{u.status || 'ACTIVE'}</span>
                      </td>
                      <td>
                        <span className="table-date">{createdStr}</span>
                      </td>
                      <td>
                        <span className="table-date">—</span>
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
                              className="btn-table-action"
                              onClick={() => {
                                if (onSelectUser) onSelectUser(u);
                                onNavigate('admin-edit-user');
                              }}
                              title="Edit User Details & Role"
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              className="btn-table-action warning"
                              onClick={() => handleOpenReset(u)}
                              title="Reset User Password"
                            >
                              Reset Password
                            </button>
                            <button
                              type="button"
                              className="btn-table-action danger"
                              onClick={(e) => handleOpenDelete(u, e)}
                              title="Permanently Delete User Account"
                            >
                              Delete
                            </button>
                          </div>
                      </td>
                    </tr>
                  );
                })
              )}
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

      {/* Permanent Delete User Confirmation Modal */}
      <Modal
        isOpen={!!deleteModalUser}
        onClose={handleCloseDelete}
        title="Delete User Account"
        subtitle={`Permanent account deletion for @${deleteModalUser?.username}`}
      >
        {deleteError && (
          <Alert
            type="error"
            title="Deletion Failed"
            message={deleteError}
          />
        )}

        <form onSubmit={handleExecuteDelete}>
          <div style={{ marginBottom: '1.25rem' }}>
            <p style={{ color: 'var(--color-danger, #DC2626)', fontWeight: 600, fontSize: '0.95rem', marginBottom: '0.5rem' }}>
              Warning: This action is permanent and cannot be undone.
            </p>
            <p style={{ color: 'var(--color-text-secondary, #64748b)', fontSize: '0.875rem', lineHeight: 1.5, marginBottom: '1rem' }}>
              User account <strong>{deleteModalUser?.username}</strong> ({deleteModalUser?.email}) will be permanently deleted from the database.
            </p>
            <label
              htmlFor="listDeleteConfirmInput"
              style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: 'var(--color-text-primary)', marginBottom: '0.375rem' }}
            >
              Type <strong>DELETE</strong> to confirm.
            </label>
            <input
              type="text"
              id="listDeleteConfirmInput"
              value={deleteConfirmText}
              onChange={(e) => setDeleteConfirmText(e.target.value)}
              placeholder="Type DELETE"
              required
              disabled={isDeleting}
              autoComplete="off"
              style={{
                width: '100%',
                padding: '0.5rem 0.75rem',
                border: '1px solid var(--color-border, #cbd5e1)',
                borderRadius: 'var(--radius-sm, 4px)',
                fontFamily: 'monospace',
                fontSize: '0.95rem'
              }}
            />
          </div>

          <div className="modal-actions-row">
            <Button variant="outline" onClick={handleCloseDelete} disabled={isDeleting}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="danger"
              disabled={deleteConfirmText !== 'DELETE' || isDeleting}
              isLoading={isDeleting}
            >
              Permanently Delete User
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};


export default AdminUserListPage;
