import React, { useState, useEffect } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getAdminUsers, deleteAdminUser, updateUserStatus, resetUserPassword } from '../../services/adminUserService';

const AdminUserListPage = ({ onNavigate, onSelectUser }) => {
  const { token, user: currentAuthUser } = useAuth();
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [fetchError, setFetchError] = useState(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRoleFilter, setSelectedRoleFilter] = useState('ALL');

  // Reset Password Modal state
  const [resetModalUser, setResetModalUser] = useState(null);
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [isResetting, setIsResetting] = useState(false);
  const [resetNotice, setResetNotice] = useState(null);
  const [resetSuccessData, setResetSuccessData] = useState(null);
  const [copiedNotice, setCopiedNotice] = useState(false);

  // Delete User Modal state
  const [deleteModalUser, setDeleteModalUser] = useState(null);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);
  const [actionSuccessNotice, setActionSuccessNotice] = useState(null);

  // Account Status (Enable/Disable) Modal state
  const [statusModalUser, setStatusModalUser] = useState(null);
  const [statusModalTargetActive, setStatusModalTargetActive] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusError, setStatusError] = useState(null);

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

  const handleOpenReset = (user, e) => {
    if (e) e.stopPropagation();
    setResetModalUser(user);
    setForceChangeToggle(true);
    setResetNotice(null);
    setResetSuccessData(null);
    setCopiedNotice(false);
    setIsResetting(false);
  };

  const handleCloseReset = () => {
    if (isResetting) return;
    setResetModalUser(null);
    setResetNotice(null);
    setResetSuccessData(null);
    setCopiedNotice(false);
  };

  const handleExecuteReset = async (e) => {
    e.preventDefault();
    if (!resetModalUser) return;
    setIsResetting(true);
    setResetNotice(null);
    setCopiedNotice(false);

    const res = await resetUserPassword(resetModalUser.id, {
      forcePasswordChange: forceChangeToggle,
    }, token);

    setIsResetting(false);
    if (res.success) {
      setResetSuccessData(res.data);
      setResetNotice({
        type: 'success',
        title: 'Temporary Credential Generated',
        message: res.data.message || `Password reset successfully for @${resetModalUser.username}.`,
      });
    } else {
      setResetNotice({
        type: 'error',
        title: 'Password Reset Failed',
        message: res.message || 'An error occurred while resetting the user password.',
      });
    }
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

  const handleOpenStatusModal = (user, targetActive, e) => {
    if (e) e.stopPropagation();
    setStatusModalUser(user);
    setStatusModalTargetActive(targetActive);
    setStatusError(null);
  };

  const handleCloseStatusModal = () => {
    if (isUpdatingStatus) return;
    setStatusModalUser(null);
    setStatusError(null);
  };

  const handleExecuteStatusToggle = async (e) => {
    e.preventDefault();
    if (!statusModalUser) return;

    setIsUpdatingStatus(true);
    setStatusError(null);

    const res = await updateUserStatus(statusModalUser.id, statusModalTargetActive, token);
    setIsUpdatingStatus(false);

    if (res.success) {
      setUsers((prev) =>
        prev.map((u) =>
          u.id === statusModalUser.id
            ? { ...u, isActive: statusModalTargetActive, status: statusModalTargetActive ? 'ACTIVE' : 'DISABLED' }
            : u
        )
      );
      setActionSuccessNotice(
        `User account "${statusModalUser.username}" was successfully ${
          statusModalTargetActive ? 're-enabled' : 'disabled'
        }.`
      );
      setStatusModalUser(null);
    } else {
      setStatusError(res.message || `Failed to ${statusModalTargetActive ? 'enable' : 'disable'} user account.`);
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
                        <span className={(u.isActive ?? (u.status === 'ACTIVE')) ? 'status-pill-active' : 'status-pill-disabled'}>
                          {(u.isActive ?? (u.status === 'ACTIVE')) ? 'ACTIVE' : 'DISABLED'}
                        </span>
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
                            {(u.isActive ?? (u.status === 'ACTIVE')) ? (
                              <button
                                type="button"
                                className="btn-table-action warning"
                                onClick={(e) => handleOpenStatusModal(u, false, e)}
                                disabled={currentAuthUser && (currentAuthUser.id === u.id || currentAuthUser.userId === u.id || currentAuthUser.username === u.username)}
                                title={currentAuthUser && (currentAuthUser.id === u.id || currentAuthUser.userId === u.id || currentAuthUser.username === u.username) ? "You cannot disable your own account" : "Disable User Account"}
                              >
                                Disable
                              </button>
                            ) : (
                              <button
                                type="button"
                                className="btn-table-action success"
                                onClick={(e) => handleOpenStatusModal(u, true, e)}
                                title="Re-enable User Account"
                              >
                                Enable
                              </button>
                            )}
                            <button
                              type="button"
                              className="btn-table-action warning"
                              onClick={(e) => handleOpenReset(u, e)}
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
        onClose={handleCloseReset}
        title="Admin Reset Password"
        subtitle={`Generate a secure temporary credential for @${resetModalUser?.username}`}
      >
        {resetNotice && <Alert type={resetNotice.type} title={resetNotice.title} message={resetNotice.message} />}

        {!resetSuccessData ? (
          <form onSubmit={handleExecuteReset}>
            <div style={{ marginBottom: '1.25rem' }}>
              <p style={{ color: 'var(--color-text-secondary, #64748b)', fontSize: '0.875rem', lineHeight: 1.5, marginBottom: '0.75rem' }}>
                Resetting password for <strong>{resetModalUser?.fullName || resetModalUser?.username}</strong> (<code>{resetModalUser?.email}</code>).
              </p>
              <p style={{ color: 'var(--color-text-secondary, #64748b)', fontSize: '0.875rem', lineHeight: 1.5 }}>
                A cryptographically secure temporary password will be generated. The existing password will be immediately invalidated and replaced without exposing or disclosing any prior credentials.
              </p>
            </div>

            <div className="checkbox-field-row" style={{ marginBottom: '1.5rem' }}>
              <input
                type="checkbox"
                id="forcePasswordChange"
                checked={forceChangeToggle}
                onChange={(e) => setForceChangeToggle(e.target.checked)}
                disabled={isResetting}
              />
              <label htmlFor="forcePasswordChange">
                <strong>Force password change on next login</strong>
                <span>User will be required to configure a new personal password immediately upon next authentication.</span>
              </label>
            </div>

            <div className="modal-actions-row">
              <Button variant="outline" type="button" onClick={handleCloseReset} disabled={isResetting}>
                Cancel
              </Button>
              <Button type="submit" variant="primary" isLoading={isResetting} disabled={isResetting}>
                Generate & Reset Password
              </Button>
            </div>
          </form>
        ) : (
          <div>
            <div className="temp-password-box" style={{ margin: '1rem 0' }}>
              <span className="temp-label">Generated Temporary Password:</span>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '0.5rem' }}>
                <code className="temp-code" style={{ flex: 1, wordBreak: 'break-all', fontSize: '1.1rem', letterSpacing: '0.05em' }}>
                  {resetSuccessData.temporaryPassword}
                </code>
                <Button
                  variant="outline"
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(resetSuccessData.temporaryPassword);
                    setCopiedNotice(true);
                    setTimeout(() => setCopiedNotice(false), 2500);
                  }}
                >
                  {copiedNotice ? '✓ Copied' : 'Copy'}
                </Button>
              </div>
              <p className="temp-desc" style={{ marginTop: '0.75rem', fontSize: '0.825rem' }}>
                Provide this temporary password securely to the user.
                {resetSuccessData.mustChangePassword ? ' They must configure a new password upon login.' : ''}
                <br />
                <strong style={{ color: 'var(--color-danger, #ef4444)' }}>
                  Warning: This credential will never be displayed again.
                </strong>
              </p>
            </div>

            <div className="modal-actions-row" style={{ justifyContent: 'flex-end' }}>
              <Button variant="primary" type="button" onClick={handleCloseReset}>
                Done
              </Button>
            </div>
          </div>
        )}
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

      {/* Account Status (Enable/Disable) Confirmation Modal */}
      <Modal
        isOpen={!!statusModalUser}
        onClose={handleCloseStatusModal}
        title={statusModalTargetActive ? "Re-enable User Account" : "Disable User Account"}
        subtitle={`Control system access for ${statusModalUser?.username} without deleting history.`}
      >
        {statusError && (
          <Alert type="error" title="Status Change Error" message={statusError} />
        )}

        <div style={{ marginBottom: '1.25rem', fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', lineHeight: '1.5' }}>
          {statusModalTargetActive ? (
            <p>
              Are you sure you want to <strong>re-enable</strong> the account for <strong>{statusModalUser?.username}</strong>?
              They will regain the ability to log in and access protected FleetFlow services.
            </p>
          ) : (
            <p>
              Are you sure you want to <strong>disable</strong> the account for <strong>{statusModalUser?.username}</strong>?
              They will be <strong>blocked from authenticating</strong> or accessing protected services. All records, roles, and historical data remain preserved.
            </p>
          )}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseStatusModal} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button
            variant={statusModalTargetActive ? "primary" : "danger"}
            onClick={handleExecuteStatusToggle}
            disabled={isUpdatingStatus}
            isLoading={isUpdatingStatus}
          >
            {statusModalTargetActive ? 'Confirm Enable' : 'Confirm Disable'}
          </Button>
        </div>
      </Modal>
    </div>
  );
};


export default AdminUserListPage;
