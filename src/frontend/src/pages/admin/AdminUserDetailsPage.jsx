import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import AccountAvatar from '../../components/common/AccountAvatar';
import { accountName } from '../../utils/accountPresentation';
import React, { useState } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { deleteAdminUser, updateUserStatus, resetUserPassword, forceUserPasswordChange } from '../../services/adminUserService';
import useOnboardingConfig from '../../components/onboarding/useOnboardingConfig';
const AdminUserDetailsPage = ({
  selectedUser,
  onNavigate
}) => {
  const {
    token,
    user: currentAuthUser
  } = useAuth();
  const [user, setUser] = useState(selectedUser || null);
  const onboardingConfig = useOnboardingConfig();
  const [isResetModalOpen, setIsResetModalOpen] = useState(false);
  const [forceChangeToggle, setForceChangeToggle] = useState(true);
  const [isResetting, setIsResetting] = useState(false);
  const [resetNotice, setResetNotice] = useState(null);
  const [resetSuccessData, setResetSuccessData] = useState(null);
  const [copiedNotice, setCopiedNotice] = useState(false);
  const [notice, setNotice] = useState(null);

  // Force Password Change Modal state
  const [isForceModalOpen, setIsForceModalOpen] = useState(false);
  const [isSubmittingForce, setIsSubmittingForce] = useState(false);
  const [forceError, setForceError] = useState(null);

  // Delete User Modal state
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);

  // Account Status (Enable/Disable) Modal state
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusError, setStatusError] = useState(null);
  if (!user) return <div className="record-empty"><h1>Choose an account to continue</h1><Button onClick={() => onNavigate('admin-users')}>Return to user directory</Button></div>;
  const isUserActive = user.isActive ?? user.status === 'ACTIVE';
  const handleExecuteStatusToggle = async () => {
    if (!user?.id) return;
    setIsUpdatingStatus(true);
    setStatusError(null);
    const targetActive = !isUserActive;
    const res = await updateUserStatus(user.id, targetActive, token);
    setIsUpdatingStatus(false);
    if (res.success) {
      setUser(prev => ({
        ...prev,
        isActive: targetActive,
        status: targetActive ? 'ACTIVE' : 'DISABLED'
      }));
      setNotice({
        type: 'success',
        title: targetActive ? 'Account Re-enabled' : 'Account Disabled',
        message: `Account for ${user.username} has been successfully ${targetActive ? 're-enabled' : 'disabled'}.`
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
  const handleExecuteReset = async e => {
    e.preventDefault();
    if (!user?.id) return;
    setIsResetting(true);
    setResetNotice(null);
    setCopiedNotice(false);
    const res = await resetUserPassword(user.id, {
      forcePasswordChange: forceChangeToggle
    }, token);
    setIsResetting(false);
    if (res.success) {
      setResetSuccessData(res.data);
      setResetNotice({
        type: 'success',
        title: 'Temporary Credential Generated',
        message: res.data.message || `Password reset successfully for @${user.username}.`
      });
    } else {
      setResetNotice({
        type: 'error',
        title: 'Password Reset Failed',
        message: res.message || 'An error occurred while resetting the user password.'
      });
    }
  };
  const handleOpenForceModal = () => {
    setIsForceModalOpen(true);
    setForceError(null);
  };
  const handleCloseForceModal = () => {
    if (isSubmittingForce) return;
    setIsForceModalOpen(false);
    setForceError(null);
  };
  const handleExecuteForceChange = async targetValue => {
    if (!user?.id) return;
    setIsSubmittingForce(true);
    setForceError(null);
    const res = await forceUserPasswordChange(user.id, targetValue, token);
    setIsSubmittingForce(false);
    if (res.success) {
      setUser(prev => ({
        ...prev,
        mustChangePassword: targetValue
      }));
      setNotice({
        type: 'success',
        title: targetValue ? 'Password Change Enforced' : 'Requirement Cleared',
        message: targetValue ? `User @${user.username} will be required to change their password on next login.` : `Password change requirement cleared for @${user.username}.`
      });
      setIsForceModalOpen(false);
    } else {
      setForceError(res.message || 'Failed to update password change requirement.');
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
  const handleExecuteDelete = async e => {
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
  return <div className="admin-user-details-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
            <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to User Directory
          </button>
          <h1 className="admin-page-title">Account details: {user.username}</h1>
          <p className="admin-page-subtitle">
            Review this account’s details, role, and access controls.
          </p>
        </div>

        <div className="admin-header-actions">
          <Button variant="primary" onClick={() => onNavigate('admin-edit-user', {
          user
        })}>
            Edit User
          </Button>
          {isUserActive ? <Button variant="outline" onClick={() => {
          setIsStatusModalOpen(true);
          setStatusError(null);
        }} disabled={currentAuthUser && (currentAuthUser.id === user.id || currentAuthUser.userId === user.id || currentAuthUser.username === user.username)} title={currentAuthUser && (currentAuthUser.id === user.id || currentAuthUser.userId === user.id || currentAuthUser.username === user.username) ? "You cannot disable your own account" : "Disable User Account"} className="view-a5d6917c">
              Disable Account
            </Button> : <Button variant="outline" onClick={() => {
          setIsStatusModalOpen(true);
          setStatusError(null);
        }} className="view-6e44f0b0">
              Enable Account
            </Button>}
          {!onboardingConfig.enabled && <Button variant="danger" onClick={handleOpenDelete}>
            Delete User
          </Button>}
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
            <AccountAvatar account={user} className="account-avatar-large" />
            <div>
              <h2 className="details-username">{accountName(user)}</h2>
              <p className="details-email">{user.email}</p>
              <div className="view-70ae72af">
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
            {user.role === 'CUSTOMER' && <div className="info-row">
                <span className="info-label">Driving License</span>
                <span className="info-value">{user.drivingLicenseNumber || '—'}</span>
              </div>}
            <div className="info-row">
              <span className="info-label">Account Identifier</span>
              <span className="info-value font-mono">{user.id}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Created on</span>
              <span className="info-value">{user.createdAt ? user.createdAt.includes('T') ? user.createdAt.split('T')[0] : user.createdAt : '—'}</span>
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
            <div className="claim-item">
              <span className="claim-key">Password Change Status</span>
              <span className="claim-value">
                {user.mustChangePassword ? <span className="view-66dacdb1">
                    Required on Next Login
                  </span> : <span className="view-ba866d16">
                    Normal (Not Required)
                  </span>}
              </span>
            </div>
          </div>

          <div className="admin-actions-box">
            <h4>Security Administration</h4>
            <div className="view-63ab39d7">
              <Button variant="primary" size="sm" onClick={() => onNavigate('admin-edit-user')}>
                Edit User Account
              </Button>
              <Button variant={user.mustChangePassword ? "outline" : "warning"} size="sm" onClick={handleOpenForceModal}>
                {user.mustChangePassword ? "Clear Change Req" : "Force Password Change"}
              </Button>
              {!onboardingConfig.enabled && <Button variant="danger" size="sm" onClick={handleOpenDelete}>
                Delete User Account
              </Button>}
              <Button variant="outline" size="sm" onClick={handleOpenReset}>
                Issue Temporary Password
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Password Reset Modal */}
      <Modal isOpen={isResetModalOpen} onClose={handleCloseReset} title="Admin Reset Password" subtitle={`Generate a secure temporary credential for @${user.username}`}>
        {resetNotice && <Alert type={resetNotice.type} title={resetNotice.title} message={resetNotice.message} />}

        {!resetSuccessData ? <form onSubmit={handleExecuteReset}>
            <div className="view-014b93e7">
              <p className="view-e1ff96cb">
                Resetting password for <strong>{user.fullName || user.username}</strong> (<code>{user.email}</code>).
              </p>
              <p className="view-464cf24b">
                Resetting the password replaces the current password with a temporary one. Share it securely with the account holder.
              </p>
            </div>

            <div className="checkbox-field-row view-002dc26c">
              <input type="checkbox" id="forcePasswordChangeDetails" checked={forceChangeToggle} onChange={e => setForceChangeToggle(e.target.checked)} disabled={isResetting} />
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
          </form> : <div>
            <div className="temp-password-box view-a9799837">
              <span className="temp-label">Generated Temporary Password:</span>
              <div className="view-b8cbced6">
                <code className="temp-code view-7e6fb287">
                  {resetSuccessData.temporaryPassword}
                </code>
                <Button variant="outline" type="button" onClick={() => {
              navigator.clipboard.writeText(resetSuccessData.temporaryPassword);
              setCopiedNotice(true);
              setTimeout(() => setCopiedNotice(false), 2500);
            }}>
                  {copiedNotice ? 'Copied' : 'Copy'}
                </Button>
              </div>
              <p className="temp-desc view-87fe899d">
                Provide this temporary password securely to the user.
                {resetSuccessData.mustChangePassword ? ' They must configure a new password upon login.' : ''}
                <br />
                <strong className="view-003f95a0">
                  Warning: This credential will never be displayed again.
                </strong>
              </p>
            </div>

            <div className="modal-actions-row view-d5fbe829">
              <Button variant="primary" type="button" onClick={handleCloseReset}>
                Done
              </Button>
            </div>
          </div>}
      </Modal>

      {/* Permanent Delete User Confirmation Modal */}
      <Modal isOpen={isDeleteModalOpen} onClose={handleCloseDelete} title="Delete User Account" subtitle={`Permanent account deletion for @${user.username}`}>
        {deleteError && <Alert type="error" title="Deletion Failed" message={deleteError} />}

        <form onSubmit={handleExecuteDelete}>
          <div className="view-014b93e7">
            <p className="view-3dddf789">
              Warning: This action is permanent and cannot be undone.
            </p>
            <p className="view-307c54b6">
              User account <strong>{user.username}</strong> ({user.email}) will be permanently deleted from the database.
            </p>
            <label htmlFor="detailsDeleteConfirmInput" className="view-e49e0bdb">
              Type <strong>DELETE</strong> to confirm.
            </label>
            <input type="text" id="detailsDeleteConfirmInput" value={deleteConfirmText} onChange={e => setDeleteConfirmText(e.target.value)} placeholder="Type DELETE" required disabled={isDeleting} autoComplete="off" className="view-1c52fdfc" />
          </div>

          <div className="modal-actions-row">
            <Button variant="outline" onClick={handleCloseDelete} disabled={isDeleting}>
              Cancel
            </Button>
            <Button type="submit" variant="danger" disabled={deleteConfirmText !== 'DELETE' || isDeleting} isLoading={isDeleting}>
              Permanently Delete User
            </Button>
          </div>
        </form>
      </Modal>

      {/* Account Status (Enable/Disable) Confirmation Modal */}
      <Modal isOpen={isStatusModalOpen} onClose={() => {
      if (!isUpdatingStatus) setIsStatusModalOpen(false);
    }} title={isUserActive ? "Disable User Account" : "Re-enable User Account"} subtitle={`Control system access for ${user.username} without deleting history.`}>
        {statusError && <Alert type="error" title="Status Change Error" message={statusError} />}

        <div className="view-b1d83fd0">
          {isUserActive ? <p>
              Are you sure you want to <strong>disable</strong> the account for <strong>{user.username}</strong>?
              They will be <strong>blocked from authenticating</strong> or accessing protected services. All records, roles, and historical data remain preserved.
            </p> : <p>
              Are you sure you want to <strong>re-enable</strong> the account for <strong>{user.username}</strong>?
              They will regain the ability to log in and access protected FleetFlow services.
            </p>}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={() => setIsStatusModalOpen(false)} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button variant={isUserActive ? "danger" : "primary"} onClick={handleExecuteStatusToggle} disabled={isUpdatingStatus} isLoading={isUpdatingStatus}>
            {isUpdatingStatus ? 'Updating...' : isUserActive ? 'Confirm Disable' : 'Confirm Enable'}
          </Button>
        </div>
      </Modal>

      {/* Force Password Change Modal */}
      <Modal isOpen={isForceModalOpen} onClose={handleCloseForceModal} title={user.mustChangePassword ? "Clear Password Change Requirement" : "Require Password Change on Next Login"} subtitle={`Administrative credential policy enforcement for @${user.username}`}>
        {forceError && <Alert type="error" title="Configuration Error" message={forceError} />}

        <div className="view-b1d83fd0">
          {user.mustChangePassword ? <div>
              <p className="view-413d04d4">
                User <strong>@{user.username}</strong> ({user.email}) is currently <strong>flagged to change their password</strong> upon authentication.
              </p>
              <p>
                Would you like to <strong>clear this requirement</strong>? The user will be able to log in normally with their current credentials without being redirected to the password change flow.
              </p>
            </div> : <div>
              <p className="view-413d04d4">
                Flagging <strong>@{user.username}</strong> ({user.email}) will <strong>force them to configure a new password</strong> immediately upon their next login.
              </p>
              <p className="view-413d04d4">
                Their current password remains valid for initial sign-in, but access to all protected application features (fleet reservations, workspace consoles, user profiles) will remain strictly blocked until the new password is saved.
              </p>
            </div>}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseForceModal} disabled={isSubmittingForce}>
            Cancel
          </Button>
          <Button variant={user.mustChangePassword ? "outline" : "primary"} onClick={() => handleExecuteForceChange(!user.mustChangePassword)} disabled={isSubmittingForce} isLoading={isSubmittingForce}>
            {user.mustChangePassword ? "Confirm Clear Requirement" : "Enforce Password Change"}
          </Button>
        </div>
      </Modal>
    </div>;
};
export default AdminUserDetailsPage;
