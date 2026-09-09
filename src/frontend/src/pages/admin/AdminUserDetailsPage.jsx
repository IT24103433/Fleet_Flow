import React, { useState } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { deleteAdminUser } from '../../services/adminUserService';

const AdminUserDetailsPage = ({ selectedUser, onNavigate }) => {
  const { token } = useAuth();
  const user = selectedUser || {
    id: 'u-1',
    username: 'admin_sarah',
    email: 'sarah.admin@fleetflow.io',
    role: 'ADMIN',
    status: 'ACTIVE',
    createdAt: '2026-08-15',
    lastLogin: '2026-09-01',
  };

  const [isResetModalOpen, setIsResetModalOpen] = useState(false);
  const [tempPassword, setTempPassword] = useState('');
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [notice, setNotice] = useState(null);

  // Delete User Modal state
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);

  const handleOpenReset = () => {
    setTempPassword('Temp#' + Math.random().toString(36).substring(2, 8).toUpperCase() + '!');
    setIsResetModalOpen(true);
    setNotice(null);
  };

  const handleExecuteReset = (e) => {
    e.preventDefault();
    setNotice({
      type: 'success',
      title: 'Password Reset Generated',
      message: `Temporary password issued for ${user.username}. Force password change on next login: ${forceChangeToggle ? 'Enabled' : 'Disabled'}.`,
    });
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
            <span className="status-pill-active">{user.status}</span>
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
        onClose={() => setIsResetModalOpen(false)}
        title="Administrative Password Reset"
        subtitle={`Generate a secure temporary password for ${user.username}.`}
      >
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
              <span>User must configure a private password upon authentication.</span>
            </label>
          </div>

          <div className="modal-actions-row">
            <Button variant="outline" onClick={() => setIsResetModalOpen(false)}>
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
    </div>
  );
};

export default AdminUserDetailsPage;
