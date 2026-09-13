import React, { useState } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { deleteAdminUser, updateUserStatus, resetUserPassword } from '../../services/adminUserService';

const AdminUserDetailsPage = ({ selectedUser, onNavigate }) => {
  const { token, user: currentAuthUser } = useAuth();
  const [user, setUser] = useState(
    selectedUser || {
      id: 'u-1',
      username: 'admin_sarah',
      email: 'sarah.admin@fleetflow.io',
      role: 'ADMIN',
      status: 'ACTIVE',
      isActive: true,
      createdAt: '2026-08-15',
      lastLogin: '2026-09-01',
    }
  );

  const [isResetModalOpen, setIsResetModalOpen] = useState(false);
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [isResetting, setIsResetting] = useState(false);
  const [resetNotice, setResetNotice] = useState(null);
  const [resetSuccessData, setResetSuccessData] = useState(null);
  const [copiedNotice, setCopiedNotice] = useState(false);
  const [notice, setNotice] = useState(null);

  // Delete User Modal state
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);

  // Account Status (Enable/Disable) Modal state
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusError, setStatusError] = useState(null);

  const isUserActive = user.isActive ?? (user.status === 'ACTIVE');

  const handleExecuteStatusToggle = async () => {
    if (!user?.id) return;

    setIsUpdatingStatus(true);
    setStatusError(null);

    const targetActive = !isUserActive;
    const res = await updateUserStatus(user.id, targetActive, token);
    setIsUpdatingStatus(false);

    if (res.success) {
      setUser((prev) => ({
        ...prev,
        isActive: targetActive,
        status: targetActive ? 'ACTIVE' : 'DISABLED',
      }));
      setNotice({
        type: 'success',
        title: targetActive ? 'Account Re-enabled' : 'Account Disabled',
        message: `Account for ${user.username} has been successfully ${targetActive ? 're-enabled' : 'disabled'}.`,
      });
      setIsStatusModalOpen(false);
    } else {
      setStatusError(res.message || `Failed to ${targetActive ? 'enable' : 'disable'} user account.`);
    }
  };

  const handleOpenReset = () => {
    setIsResetModalOpen(true);
    setForceChangeToggle(true);
    setResetNotice(null);
    setResetSuccessData(null);
    setCopiedNotice(false);
    setIsResetting(false);
  };

  const handleCloseReset = () => {
    if (isResetting) return;
    setIsResetModalOpen(false);
    setResetNotice(null);
    setResetSuccessData(null);
    setCopiedNotice(false);
  };

  const handleExecuteReset = async (e) => {
    e.preventDefault();
    if (!user?.id) return;
    setIsResetting(true);
    setResetNotice(null);
    setCopiedNotice(false);

    const res = await resetUserPassword(user.id, {
      forcePasswordChange: forceChangeToggle,
    }, token);

    setIsResetting(false);
    if (res.success) {
      setResetSuccessData(res.data);
      setResetNotice({
        type: 'success',
        title: 'Temporary Credential Generated',
        message: res.data.message || `Password reset successfully for @${user.username}.`,
      });
    } else {
      setResetNotice({
        type: 'error',
        title: 'Password Reset Failed',
        message: res.message || 'An error occurred while resetting the user password.',
      });
    }
  };

  const handleOpenDelete = () => {
    setIsDeleteModalOpen(true);
    setDeleteConfirmText('');
    setDeleteError(null);
  };

  const handleCloseDelete = () => {
    if (isDeleting) return;
    setIsDeleteModalOpen(false);
    setDeleteConfirmText('');
    setDeleteError(null);
  };

  const handleExecuteDelete = async (e) => {
    e.preventDefault();
    if (deleteConfirmText !== 'DELETE') return;
    if (!user?.id) return;

    setIsDeleting(true);
    setDeleteError(null);

    const res = await deleteAdminUser(user.id, token);
    setIsDeleting(false);

    if (res.success) {
      setIsDeleteModalOpen(false);
      onNavigate('admin-users');
    } else {
      setDeleteError(res.message || 'Failed to delete user account.');
    }
  };


  return (
    <div className="admin-user-details-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
            ← Back to User Directory
          </button>
          <h1 className="admin-page-title">User Account Inspection: {user.username}</h1>
          <p className="admin-page-subtitle">
            Inspect security claims, assigned roles, and administrative options.
          </p>
        </div>

        <div className="admin-header-actions">
          <Button variant="primary" onClick={() => onNavigate('admin-edit-user')}>
            Edit User
          </Button>
          {isUserActive ? (
            <Button
              variant="outline"
              style={{ borderColor: 'rgba(245, 158, 11, 0.6)', color: '#B45309' }}
              onClick={() => { setIsStatusModalOpen(true); setStatusError(null); }}
              disabled={currentAuthUser && (currentAuthUser.id === user.id || currentAuthUser.userId === user.id || currentAuthUser.username === user.username)}
              title={currentAuthUser && (currentAuthUser.id === user.id || currentAuthUser.userId === user.id || currentAuthUser.username === user.username) ? "You cannot disable your own account" : "Disable User Account"}
            >
              Disable Account
            </Button>
          ) : (
            <Button
              variant="outline"
              style={{ borderColor: 'rgba(34, 197, 94, 0.6)', color: '#15803D' }}
              onClick={() => { setIsStatusModalOpen(true); setStatusError(null); }}
            >
              Enable Account
            </Button>
          )}
          <Button variant="danger" onClick={handleOpenDelete}>
            Delete User
          </Button>
          <Button variant="outline" onClick={handleOpenReset}>
            Reset Password
          </Button>
          <Button variant="outline" onClick={() => onNavigate('admin-users')}>
            Return to List
          </Button>
        </div>
      </div>


      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      <div className="user-details-grid">
        {/* User Identity Card */}
        <div className="details-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Account Overview</h3>
            <span className={isUserActive ? 'status-pill-active' : 'status-pill-disabled'}>
              {isUserActive ? 'ACTIVE' : 'DISABLED'}
            </span>
          </div>

          <div className="details-avatar-row">
            <div className="details-avatar-circle">
              {user.username.charAt(0).toUpperCase()}
            </div>
            <div>
              <h2 className="details-username">{user.username}</h2>
              <p className="details-email">{user.email}</p>
              <div style={{ marginTop: '6px' }}>
                <RoleBadge role={user.role} />
              </div>
            </div>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Full Name</span>
              <span className="info-value">{user.fullName || '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Phone Number</span>
              <span className="info-value">{user.phoneNumber || '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Address</span>
              <span className="info-value">{user.address || '—'}</span>
            </div>
            {user.role === 'CUSTOMER' && (
              <div className="info-row">
                <span className="info-label">Driving License</span>
                <span className="info-value">{user.drivingLicenseNumber || '—'}</span>
              </div>
            )}
            <div className="info-row">
              <span className="info-label">Account Identifier</span>
              <span className="info-value font-mono">{user.id}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Provisioned Date</span>
              <span className="info-value">{user.createdAt ? (user.createdAt.includes('T') ? user.createdAt.split('T')[0] : user.createdAt) : '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Last Active Session</span>
              <span className="info-value">{user.lastLogin || '—'}</span>
            </div>
          </div>
        </div>

        {/* Security Claims & Authorization Card */}
        <div className="details-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Security & Role Authorization</h3>
            <span className="read-only-badge">Access Claims</span>
          </div>

          <div className="claims-list">
            <div className="claim-item">
              <span className="claim-key">Assigned Role</span>
              <span className="claim-value">
                <RoleBadge role={user.role} />
              </span>
            </div>
            <div className="claim-item">
              <span className="claim-key">User Identifier</span>
              <span className="claim-value font-mono">{user.id}</span>
            </div>
            <div className="claim-item">
              <span className="claim-key">Username</span>
              <span className="claim-value">{user.username}</span>
            </div>
            <div className="claim-item">
              <span className="claim-key">Email Address</span>
              <span className="claim-value">{user.email}</span>
            </div>
          </div>

          <div className="admin-actions-box">
            <h4>Security Administration</h4>
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '8px' }}>
              <Button variant="primary" size="sm" onClick={() => onNavigate('admin-edit-user')}>
                Edit User Account
              </Button>
              <Button variant="danger" size="sm" onClick={handleOpenDelete}>
                Delete User Account
              </Button>
              <Button variant="outline" size="sm" onClick={handleOpenReset}>
                Issue Temporary Password
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Password Reset Modal */}
      <Modal
        isOpen={isResetModalOpen}
        onClose={handleCloseReset}
        title="Admin Reset Password"
        subtitle={`Generate a secure temporary credential for @${user.username}`}
      >
        {resetNotice && <Alert type={resetNotice.type} title={resetNotice.title} message={resetNotice.message} />}

        {!resetSuccessData ? (
          <form onSubmit={handleExecuteReset}>
            <div style={{ marginBottom: '1.25rem' }}>
              <p style={{ color: 'var(--color-text-secondary, #64748b)', fontSize: '0.875rem', lineHeight: 1.5, marginBottom: '0.75rem' }}>
                Resetting password for <strong>{user.fullName || user.username}</strong> (<code>{user.email}</code>).
              </p>
              <p style={{ color: 'var(--color-text-secondary, #64748b)', fontSize: '0.875rem', lineHeight: 1.5 }}>
                A cryptographically secure temporary password will be generated. The existing password will be immediately invalidated and replaced without exposing or disclosing any prior credentials.
              </p>
            </div>

            <div className="checkbox-field-row" style={{ marginBottom: '1.5rem' }}>
              <input
                type="checkbox"
                id="forcePasswordChangeDetails"
                checked={forceChangeToggle}
                onChange={(e) => setForceChangeToggle(e.target.checked)}
                disabled={isResetting}
              />
              <label htmlFor="forcePasswordChangeDetails">
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
        isOpen={isDeleteModalOpen}
        onClose={handleCloseDelete}
        title="Delete User Account"
        subtitle={`Permanent account deletion for @${user.username}`}
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
              User account <strong>{user.username}</strong> ({user.email}) will be permanently deleted from the database.
            </p>
            <label
              htmlFor="detailsDeleteConfirmInput"
              style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: 'var(--color-text-primary)', marginBottom: '0.375rem' }}
            >
              Type <strong>DELETE</strong> to confirm.
            </label>
            <input
              type="text"
              id="detailsDeleteConfirmInput"
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
        isOpen={isStatusModalOpen}
        onClose={() => { if (!isUpdatingStatus) setIsStatusModalOpen(false); }}
        title={isUserActive ? "Disable User Account" : "Re-enable User Account"}
        subtitle={`Control system access for ${user.username} without deleting history.`}
      >
        {statusError && (
          <Alert type="error" title="Status Change Error" message={statusError} />
        )}

        <div style={{ marginBottom: '1.25rem', fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', lineHeight: '1.5' }}>
          {isUserActive ? (
            <p>
              Are you sure you want to <strong>disable</strong> the account for <strong>{user.username}</strong>?
              They will be <strong>blocked from authenticating</strong> or accessing protected services. All records, roles, and historical data remain preserved.
            </p>
          ) : (
            <p>
              Are you sure you want to <strong>re-enable</strong> the account for <strong>{user.username}</strong>?
              They will regain the ability to log in and access protected FleetFlow services.
            </p>
          )}
        </div>

        <div className="modal-actions-row">
          <Button
            variant="outline"
            onClick={() => setIsStatusModalOpen(false)}
            disabled={isUpdatingStatus}
          >
            Cancel
          </Button>
          <Button
            variant={isUserActive ? "danger" : "primary"}
            onClick={handleExecuteStatusToggle}
            disabled={isUpdatingStatus}
            isLoading={isUpdatingStatus}
          >
            {isUpdatingStatus
              ? 'Updating...'
              : isUserActive
              ? 'Confirm Disable'
              : 'Confirm Enable'}
          </Button>
        </div>
      </Modal>
    </div>
  );
};

export default AdminUserDetailsPage;
