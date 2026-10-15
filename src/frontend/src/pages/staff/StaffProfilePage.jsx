import useOnboardingConfig from '../../components/onboarding/useOnboardingConfig';
import AccountPanel from '../../components/onboarding/AccountPanel';
import { CameraIcon } from "@phosphor-icons/react/dist/csr/Camera";
import { UploadSimpleIcon } from "@phosphor-icons/react/dist/csr/UploadSimple";
import { UserCircleIcon } from "@phosphor-icons/react/dist/csr/UserCircle";
import UploadedImage from "../../components/common/UploadedImage";
import AccountAvatar from '../../components/common/AccountAvatar';
import { accountName } from '../../utils/accountPresentation';
import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import ChangePasswordModal from '../../components/security/ChangePasswordModal';
import { uploadProfilePicture, deleteProfilePicture } from '../../services/authService';
import { getProfileImageUrl } from '../../utils/imageUrlUtils';
const StaffProfilePage = () => {
  const {
    user,
    roles,
    token,
    updateUser
  } = useAuth();
  const onboardingConfig = useOnboardingConfig();
  const primaryRole = roles?.[0];
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);
  const [isPhotoModalOpen, setIsPhotoModalOpen] = useState(false);
  const staffInfo = {
    staffId: user?.id ? String(user.id).slice(0, 8).toUpperCase() : '—',
    department: primaryRole === 'MAINTENANCE_STAFF' ? 'Maintenance workspace' : primaryRole === 'ADMIN' ? 'Administration workspace' : 'Fleet management workspace',
    workPhone: user?.phoneNumber || '—',
    officeLocation: user?.address || '—'
  };
  const [notice, setNotice] = useState(null);

  // Persistent photo upload state
  const [photoFile, setPhotoFile] = useState(null);
  const [photoPreview, setPhotoPreview] = useState(null);
  const [isSavingPhoto, setIsSavingPhoto] = useState(false);
  const [isDeletingPhoto, setIsDeletingPhoto] = useState(false);
  const [photoError, setPhotoError] = useState(null);
  const handleOpenPhotoModal = () => {
    setPhotoFile(null);
    setPhotoPreview(user?.profileImageUrl ? getProfileImageUrl(user.profileImageUrl) : null);
    setPhotoError(null);
    setIsPhotoModalOpen(true);
  };
  const handlePhotoSelect = e => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) {
        setPhotoError('Selected image exceeds the 5 MB limit.');
        return;
      }
      setPhotoFile(file);
      setPhotoPreview(URL.createObjectURL(file));
      setPhotoError(null);
    }
  };
  const handleSavePhoto = async () => {
    if (!photoFile) {
      setPhotoError('Please select a photo file first.');
      return;
    }
    setIsSavingPhoto(true);
    setPhotoError(null);
    const result = await uploadProfilePicture(photoFile, token);
    setIsSavingPhoto(false);
    if (result.success && result.data?.profileImageUrl) {
      updateUser({
        profileImageUrl: result.data.profileImageUrl
      });
      setIsPhotoModalOpen(false);
      setNotice({
        type: 'success',
        title: 'Staff Photo Updated',
        message: 'Your profile photo was saved successfully.'
      });
    } else {
      setPhotoError(result.message || 'Failed to upload identification photo.');
    }
  };
  const handleDeletePhoto = async () => {
    setIsDeletingPhoto(true);
    setPhotoError(null);
    const result = await deleteProfilePicture(token);
    setIsDeletingPhoto(false);
    if (result.success) {
      updateUser({
        profileImageUrl: null
      });
      setPhotoPreview(null);
      setPhotoFile(null);
      setIsPhotoModalOpen(false);
      setNotice({
        type: 'success',
        title: 'Staff Photo Removed',
        message: 'Your identification photo was removed.'
      });
    } else {
      setPhotoError(result.message || 'Failed to remove identification photo.');
    }
  };
  return <div className="staff-profile-container">
      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      {/* Staff Hero Banner */}
      <div className="staff-profile-hero">
        <div className="staff-avatar-box">
          <div className="staff-avatar-large">
            <AccountAvatar account={user} className="account-avatar-large" />
          </div>
          <button type="button" className="change-photo-btn staff-theme" onClick={handleOpenPhotoModal}>
            <CameraIcon width="14" height="14" weight="regular" aria-hidden="true" />
            <span>Update Photo</span>
          </button>
        </div>

        <div className="staff-meta-details">
          <div className="staff-title-row">
            <h1 className="staff-name-heading">{accountName(user)}</h1>
            <RoleBadge role={primaryRole} />
          </div>
          <p className="staff-dept-line">{staffInfo.department}</p>
          <div className="staff-badge-strip">
            <span className="staff-tag">ID: {staffInfo.staffId}</span>
            <span className="staff-tag active">Signed in</span>
          </div>
        </div>

        <div className="staff-hero-actions">
          <Button variant="outline" disabled={onboardingConfig.loading || Boolean(onboardingConfig.error)} onClick={() => onboardingConfig.enabled ? window.location.assign(`${window.location.hash.split('?')[0]}?section=security`) : setIsPasswordModalOpen(true)}>{onboardingConfig.loading ? 'Opening security…' : onboardingConfig.error ? 'Security unavailable' : onboardingConfig.enabled ? 'Manage security' : 'Change Password'}</Button>
        </div>
      </div>

      {/* Grid of Profile Sections */}
      {!onboardingConfig.loading && !onboardingConfig.error && !onboardingConfig.enabled && <div className="staff-sections-grid">
        {/* System & Identity Details (Read Only) */}
        <div className="staff-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Account details</h3>
            <span className="read-only-badge">Account summary</span>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Account reference</span>
              <span className="info-value font-mono">{staffInfo.staffId}</span>
            </div>
            <div className="info-row">
              <span className="info-label">System Username</span>
              <span className="info-value">{user?.username}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Operational Role</span>
              <span className="info-value">
                <RoleBadge role={primaryRole} />
              </span>
            </div>
            <div className="info-row">
              <span className="info-label">Workspace</span>
              <span className="info-value">{staffInfo.department}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Session status</span>
              <span className="info-value status-active">Signed in</span>
            </div>
          </div>
        </div>

        {/* Saved contact details */}
        <div className="staff-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Contact information</h3>
            <span className="read-only-badge">Account summary</span>
          </div>

          <p className="profile-details-note">These are your saved account details. Use the account settings below when editing is enabled for this environment.</p>
          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Work Direct Line</span>
              <span className="info-value">{staffInfo.workPhone}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Address</span>
              <span className="info-value">{staffInfo.officeLocation}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Session Status</span>
              <span className="info-value view-93655c9a">Active</span>
            </div>
          </div>
        </div>
      </div>}

      {/* Change Password Modal */}
      <ChangePasswordModal isOpen={isPasswordModalOpen} onClose={() => setIsPasswordModalOpen(false)} />

      {/* Staff Photo Modal */}
      <Modal isOpen={isPhotoModalOpen} onClose={() => setIsPhotoModalOpen(false)} title="Update profile photo" subtitle="Choose a PNG, JPG, or WebP image up to 5 MB.">
        <div className="photo-modal-body">
          {photoError && <div className="view-4933c088">
              <Alert type="error" title="Photo Error" message={photoError} />
            </div>}

          <div className="photo-preview-box">
            {photoPreview ? <UploadedImage src={photoPreview} alt="Selected staff preview" className="modal-avatar-preview" /> : <div className="avatar-placeholder-large">
                <UserCircleIcon width="48" height="48" weight="regular" aria-hidden="true" />
              </div>}
          </div>

          <div className="file-input-wrapper">
            <input type="file" id="staffPhotoFile" accept="image/jpeg,image/png,image/webp" onChange={handlePhotoSelect} className="file-input-native" disabled={isSavingPhoto || isDeletingPhoto} />
            <label htmlFor="staffPhotoFile" className="file-input-label">
              <UploadSimpleIcon width="18" height="18" weight="regular" aria-hidden="true" />
              <span>{photoFile ? photoFile.name : 'Choose Photo File'}</span>
            </label>
          </div>

          <div className="modal-actions-row view-5a61c2c9">
            <Button variant="outline" onClick={() => setIsPhotoModalOpen(false)} disabled={isSavingPhoto || isDeletingPhoto}>
              Cancel
            </Button>
            {user?.profileImageUrl && <Button variant="danger" onClick={handleDeletePhoto} disabled={isSavingPhoto || isDeletingPhoto}>
                {isDeletingPhoto ? 'Removing...' : 'Remove Photo'}
              </Button>}
            <Button variant="primary" onClick={handleSavePhoto} disabled={!photoFile || isSavingPhoto || isDeletingPhoto}>
              {isSavingPhoto ? 'Uploading...' : 'Save Photo'}
            </Button>
          </div>
        </div>
      </Modal>
      <AccountPanel customer={false} />
    </div>;
};
export default StaffProfilePage;
