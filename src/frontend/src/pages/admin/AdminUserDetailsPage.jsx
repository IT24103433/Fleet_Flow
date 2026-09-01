import React, { useState } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';

const AdminUserDetailsPage = ({ selectedUser, onNavigate }) => {
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
          <Button variant="outline" onClick={handleOpenReset}>
            Reset Password
          </Button>
          <Button variant="primary" onClick={() => onNavigate('admin-users')}>
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
              <span className="info-label">Account Identifier</span>
              <span className="info-value font-mono">{user.id}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Provisioned Date</span>
              <span className="info-value">{user.createdAt}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Last Active Session</span>
              <span className="info-value">{user.lastLogin}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Identity Provider</span>
              <span className="info-value">ASP.NET Identity Core</span>
            </div>
          </div>
        </div>

        {/* Security Claims & Authorization Card */}
        <div className="details-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">JWT Security Claims</h3>
            <span className="read-only-badge">Token Payload Spec</span>
          </div>

          <div className="claims-list">
            <div className="claim-item">
              <span className="claim-key">http://schemas.microsoft.com/ws/2008/06/identity/claims/role</span>
              <span className="claim-value">
                <RoleBadge role={user.role} />
              </span>
            </div>
            <div className="claim-item">
              <span className="claim-key">http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier</span>
              <span className="claim-value font-mono">{user.id}</span>
            </div>
            <div className="claim-item">
              <span className="claim-key">http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name</span>
              <span className="claim-value">{user.username}</span>
            </div>
            <div className="claim-item">
              <span className="claim-key">http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress</span>
              <span className="claim-value">{user.email}</span>
            </div>
          </div>

          <div className="admin-actions-box">
            <h4>Security Administration</h4>
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '8px' }}>
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
    </div>
  );
};

export default AdminUserDetailsPage;
