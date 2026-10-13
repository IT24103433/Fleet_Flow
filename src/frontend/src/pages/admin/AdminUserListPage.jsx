import { PlusIcon } from '@phosphor-icons/react/dist/csr/Plus';
import { MagnifyingGlassIcon } from "@phosphor-icons/react/dist/csr/MagnifyingGlass";
import AccountAvatar from '../../components/common/AccountAvatar';
import React, { useState, useEffect } from 'react';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getAdminUsers, deleteAdminUser, updateUserStatus, resetUserPassword, forceUserPasswordChange } from '../../services/adminUserService';
const AdminUserListPage = ({
  onNavigate,
  onSelectUser
}) => {
  const {
    token,
    user: currentAuthUser
  } = useAuth();
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

  // Force Password Change Modal state
  const [forceModalUser, setForceModalUser] = useState(null);
  const [isSubmittingForce, setIsSubmittingForce] = useState(false);
  const [forceError, setForceError] = useState(null);

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
  const filteredUsers = users.filter(u => {
    const term = searchTerm.toLowerCase();
    const matchesSearch = u.username && u.username.toLowerCase().includes(term) || u.email && u.email.toLowerCase().includes(term) || u.fullName && u.fullName.toLowerCase().includes(term);
    const userRole = u.role || u.roles && u.roles[0] || '';
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
  const handleExecuteReset = async e => {
    e.preventDefault();
    if (!resetModalUser) return;
    setIsResetting(true);
    setResetNotice(null);
    setCopiedNotice(false);
    const res = await resetUserPassword(resetModalUser.id, {
      forcePasswordChange: forceChangeToggle
    }, token);
    setIsResetting(false);
    if (res.success) {
      setResetSuccessData(res.data);
      setResetNotice({
        type: 'success',
        title: 'Temporary Credential Generated',
        message: res.data.message || `Password reset successfully for @${resetModalUser.username}.`
      });
    } else {
      setResetNotice({
        type: 'error',
        title: 'Password Reset Failed',
        message: res.message || 'An error occurred while resetting the user password.'
      });
    }
  };
  const handleOpenForceModal = (user, e) => {
    if (e) e.stopPropagation();
    setForceModalUser(user);
    setForceError(null);
  };
  const handleCloseForceModal = () => {
    if (isSubmittingForce) return;
    setForceModalUser(null);
    setForceError(null);
  };
  const handleExecuteForceChange = async targetValue => {
    if (!forceModalUser) return;
    setIsSubmittingForce(true);
    setForceError(null);
    const res = await forceUserPasswordChange(forceModalUser.id, targetValue, token);
    setIsSubmittingForce(false);
    if (res.success) {
      setUsers(prev => prev.map(u => u.id === forceModalUser.id ? {
        ...u,
        mustChangePassword: targetValue
      } : u));
      setActionSuccessNotice({
        type: 'success',
        title: targetValue ? 'Password Change Required' : 'Requirement Cleared',
        message: targetValue ? `User @${forceModalUser.username} will be required to change their password on next login.` : `Password change requirement cleared for @${forceModalUser.username}.`
      });
      setForceModalUser(null);
    } else {
      setForceError(res.message || 'Failed to update password change requirement.');
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
  const handleExecuteDelete = async e => {
    e.preventDefault();
    if (deleteConfirmText !== 'DELETE') return;
    if (!deleteModalUser) return;
    setIsDeleting(true);
    setDeleteError(null);
    const res = await deleteAdminUser(deleteModalUser.id, token);
    setIsDeleting(false);
    if (res.success) {
      setUsers(prev => prev.filter(u => u.id !== deleteModalUser.id));
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
  const handleExecuteStatusToggle = async e => {
    e.preventDefault();
    if (!statusModalUser) return;
    setIsUpdatingStatus(true);
    setStatusError(null);
    const res = await updateUserStatus(statusModalUser.id, statusModalTargetActive, token);
    setIsUpdatingStatus(false);
    if (res.success) {
      setUsers(prev => prev.map(u => u.id === statusModalUser.id ? {
        ...u,
        isActive: statusModalTargetActive,
        status: statusModalTargetActive ? 'ACTIVE' : 'DISABLED'
      } : u));
      setActionSuccessNotice(`User account "${statusModalUser.username}" was successfully ${statusModalTargetActive ? 're-enabled' : 'disabled'}.`);
      setStatusModalUser(null);
    } else {
      setStatusError(res.message || `Failed to ${statusModalTargetActive ? 'enable' : 'disable'} user account.`);
    }
  };
  return <div className="admin-users-container">
      {/* Page Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">User directory</h1>
          <p className="admin-page-subtitle">
            Find accounts, review roles, and manage access.
          </p>
        </div>
        <Button variant="primary" onClick={() => onNavigate('admin-create-user')}>
          <PlusIcon size={18} weight="regular" aria-hidden="true" /> Create an account
        </Button>
      </div>

      {actionSuccessNotice && <Alert type="success" title="Account Deleted" message={actionSuccessNotice} />}

      {fetchError && <Alert type="error" title="Directory Retrieval Error" message={fetchError} />}

      {/* Filter and Search Bar */}
      <div className="users-filter-bar">
        <div className="search-box-wrapper">
          <MagnifyingGlassIcon className="search-icon" weight="regular" aria-hidden="true" />
          <input type="text" placeholder="Search accounts" aria-label="Search accounts" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} className="filter-search-input" />
        </div>

        <div className="role-filter-group">
          <label htmlFor="roleFilter" className="filter-label">Role Filter:</label>
          <select id="roleFilter" value={selectedRoleFilter} onChange={e => setSelectedRoleFilter(e.target.value)} className="filter-select-input">
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
          <table className="admin-table responsive-record-table" role="table">
            <thead>
              <tr role="row">
                <th role="columnheader">User</th>
                <th role="columnheader">Role</th>
                <th role="columnheader">Status</th>
                <th role="columnheader">Created Date</th>
                <th role="columnheader">Last Active</th>
                <th className="view-37dd0c2a" role="columnheader">Security Actions</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? <tr role="row">
                  <td colSpan={6} className="view-ca39bee7" role="cell">
                    <p className="view-eb74435c">Loading user directory…</p>
                  </td>
                </tr> : filteredUsers.length === 0 ? <tr role="row">
                  <td colSpan={6} className="view-ca39bee7" role="cell">
                    <p className="view-eb74435c">
                      {users.length === 0 ? 'No user accounts found in the database.' : 'No accounts match the current search or role filter.'}
                    </p>
                  </td>
                </tr> : filteredUsers.map(u => {
              const role = u.role || u.roles && u.roles[0];
              const createdStr = u.createdAt ? new Date(u.createdAt).toISOString().split('T')[0] : '—';
              return <tr key={u.id} role="row">
                      <td data-label="Account" role="cell">
                        <div className="user-cell-meta">
                          <AccountAvatar account={u} />
                          <div>
                            <strong className="user-name-text">{u.fullName || u.username}</strong>
                            <p className="user-email-sub">{u.username !== u.fullName ? `@${u.username} • ` : ''}{u.email}</p>
                          </div>
                        </div>
                      </td>
                      <td data-label="Role" role="cell">
                        <RoleBadge role={role} />
                      </td>
                      <td data-label="Status" role="cell">
                        <div className="view-2124459e">
                          <span className={u.isActive ?? u.status === 'ACTIVE' ? 'status-pill-active' : 'status-pill-disabled'}>
                            {u.isActive ?? u.status === 'ACTIVE' ? 'ACTIVE' : 'DISABLED'}
                          </span>
                          {u.mustChangePassword && <span title="User is required to configure a new password upon next login" className="view-7725a43a">
                              Password Change Required
                            </span>}
                        </div>
                      </td>
                      <td data-label="Created" role="cell">
                        <span className="table-date">{createdStr}</span>
                      </td>
                      <td data-label="Last active" role="cell">
                        <span className="table-date">—</span>
                      </td>
                      <td className="view-37dd0c2a" data-label="Actions" role="cell">
                          <div className="table-action-btns">
                            <button type="button" className="btn-table-action" onClick={() => {
                      if (onSelectUser) onSelectUser(u);
                      onNavigate('admin-user-details');
                    }} title="View User Details">
                              Inspect
                            </button>
                            <button type="button" className="btn-table-action" onClick={() => {
                      if (onSelectUser) onSelectUser(u);
                      onNavigate('admin-edit-user');
                    }} title="Edit User Details & Role">
                              Edit
                            </button>
                            {u.isActive ?? u.status === 'ACTIVE' ? <button type="button" className="btn-table-action warning" onClick={e => handleOpenStatusModal(u, false, e)} disabled={currentAuthUser && (currentAuthUser.id === u.id || currentAuthUser.userId === u.id || currentAuthUser.username === u.username)} title={currentAuthUser && (currentAuthUser.id === u.id || currentAuthUser.userId === u.id || currentAuthUser.username === u.username) ? "You cannot disable your own account" : "Disable User Account"}>
                                Disable
                              </button> : <button type="button" className="btn-table-action success" onClick={e => handleOpenStatusModal(u, true, e)} title="Re-enable User Account">
                                Enable
                              </button>}
                            <button type="button" className={`btn-table-action ${u.mustChangePassword ? 'info' : ''}`} onClick={e => handleOpenForceModal(u, e)} title={u.mustChangePassword ? "Clear Password Change Requirement" : "Require Password Change on Next Login"}>
                              {u.mustChangePassword ? "Change Req'd" : "Require Change"}
                            </button>
                            <button type="button" className="btn-table-action warning" onClick={e => handleOpenReset(u, e)} title="Reset User Password">
                              Reset Password
                            </button>
                            <button type="button" className="btn-table-action danger" onClick={e => handleOpenDelete(u, e)} title="Permanently Delete User Account">
                              Delete
                            </button>
                          </div>
                      </td>
                    </tr>;
            })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Administrative Password Reset Modal */}
      <Modal isOpen={!!resetModalUser} onClose={handleCloseReset} title="Admin Reset Password" subtitle={`Generate a secure temporary credential for @${resetModalUser?.username}`}>
        {resetNotice && <Alert type={resetNotice.type} title={resetNotice.title} message={resetNotice.message} />}

        {!resetSuccessData ? <form onSubmit={handleExecuteReset}>
            <div className="view-014b93e7">
              <p className="view-e1ff96cb">
                Resetting password for <strong>{resetModalUser?.fullName || resetModalUser?.username}</strong> (<code>{resetModalUser?.email}</code>).
              </p>
              <p className="view-464cf24b">
                Resetting the password replaces the current password with a temporary one. Share it securely with the account holder.
              </p>
            </div>

            <div className="checkbox-field-row view-002dc26c">
              <input type="checkbox" id="forcePasswordChange" checked={forceChangeToggle} onChange={e => setForceChangeToggle(e.target.checked)} disabled={isResetting} />
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
      <Modal isOpen={!!deleteModalUser} onClose={handleCloseDelete} title="Delete User Account" subtitle={`Permanent account deletion for @${deleteModalUser?.username}`}>
        {deleteError && <Alert type="error" title="Deletion Failed" message={deleteError} />}

        <form onSubmit={handleExecuteDelete}>
          <div className="view-014b93e7">
            <p className="view-3dddf789">
              Warning: This action is permanent and cannot be undone.
            </p>
            <p className="view-307c54b6">
              User account <strong>{deleteModalUser?.username}</strong> ({deleteModalUser?.email}) will be permanently deleted from the database.
            </p>
            <label htmlFor="listDeleteConfirmInput" className="view-e49e0bdb">
              Type <strong>DELETE</strong> to confirm.
            </label>
            <input type="text" id="listDeleteConfirmInput" value={deleteConfirmText} onChange={e => setDeleteConfirmText(e.target.value)} placeholder="Type DELETE" required disabled={isDeleting} autoComplete="off" className="view-1c52fdfc" />
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
      <Modal isOpen={!!statusModalUser} onClose={handleCloseStatusModal} title={statusModalTargetActive ? "Re-enable User Account" : "Disable User Account"} subtitle={`Control system access for ${statusModalUser?.username} without deleting history.`}>
        {statusError && <Alert type="error" title="Status Change Error" message={statusError} />}

        <div className="view-b1d83fd0">
          {statusModalTargetActive ? <p>
              Are you sure you want to <strong>re-enable</strong> the account for <strong>{statusModalUser?.username}</strong>?
              They will regain the ability to log in and access protected FleetFlow services.
            </p> : <p>
              Are you sure you want to <strong>disable</strong> the account for <strong>{statusModalUser?.username}</strong>?
              They will be <strong>blocked from authenticating</strong> or accessing protected services. All records, roles, and historical data remain preserved.
            </p>}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseStatusModal} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button variant={statusModalTargetActive ? "primary" : "danger"} onClick={handleExecuteStatusToggle} disabled={isUpdatingStatus} isLoading={isUpdatingStatus}>
            {statusModalTargetActive ? 'Confirm Enable' : 'Confirm Disable'}
          </Button>
        </div>
      </Modal>

      {/* Force Password Change Modal */}
      <Modal isOpen={!!forceModalUser} onClose={handleCloseForceModal} title={forceModalUser?.mustChangePassword ? "Clear Password Change Requirement" : "Require Password Change on Next Login"} subtitle={`Administrative credential policy enforcement for @${forceModalUser?.username}`}>
        {forceError && <Alert type="error" title="Configuration Error" message={forceError} />}

        <div className="view-b1d83fd0">
          {forceModalUser?.mustChangePassword ? <div>
              <p className="view-413d04d4">
                User <strong>@{forceModalUser?.username}</strong> ({forceModalUser?.email}) is currently <strong>flagged to change their password</strong>.
              </p>
              <p>
                Would you like to <strong>clear this requirement</strong>? The user will be able to log in normally with their current credentials without being redirected to the password change flow.
              </p>
            </div> : <div>
              <p className="view-413d04d4">
                Flagging <strong>@{forceModalUser?.username}</strong> ({forceModalUser?.email}) will <strong>force them to set a new password</strong> immediately upon next login.
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
          <Button variant={forceModalUser?.mustChangePassword ? "outline" : "primary"} onClick={() => handleExecuteForceChange(!forceModalUser?.mustChangePassword)} disabled={isSubmittingForce} isLoading={isSubmittingForce}>
            {forceModalUser?.mustChangePassword ? "Confirm Clear Requirement" : "Enforce Password Change"}
          </Button>
        </div>
      </Modal>
    </div>;
};
export default AdminUserListPage;
