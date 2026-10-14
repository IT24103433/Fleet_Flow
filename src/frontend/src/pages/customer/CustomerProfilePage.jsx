import { CameraIcon } from "@phosphor-icons/react/dist/csr/Camera";
import { LockKeyIcon } from "@phosphor-icons/react/dist/csr/LockKey";
import { ShieldCheckIcon } from "@phosphor-icons/react/dist/csr/ShieldCheck";
import { UploadSimpleIcon } from "@phosphor-icons/react/dist/csr/UploadSimple";
import { UserCircleIcon } from "@phosphor-icons/react/dist/csr/UserCircle";
import UploadedImage from "../../components/common/UploadedImage";
import AccountAvatar from '../../components/common/AccountAvatar';
import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import ChangePasswordModal from '../../components/security/ChangePasswordModal';
import { uploadProfilePicture, deleteProfilePicture } from '../../services/authService';
import { getProfileImageUrl } from '../../utils/imageUrlUtils';
const CustomerProfilePage = () => {
  const {
    user,
    roles,
    token,
    updateUser
  } = useAuth();
  const primaryRole = roles?.[0];
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);
  const [isPhotoModalOpen, setIsPhotoModalOpen] = useState(false);
  const profileData = {
    fullName: user?.fullName || user?.username || '—',
    phone: user?.phoneNumber || '—',
    address: user?.address || '—',
    licenseNumber: user?.drivingLicenseNumber || '—'
  };
  const [profileNotice, setProfileNotice] = useState(null);

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
      setPhotoError('Please select an image file first.');
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
      setProfileNotice({
        type: 'success',
        title: 'Profile photo saved',
        message: 'Your profile picture was saved and updated successfully.'
      });
    } else {
      setPhotoError(result.message || 'Failed to upload profile picture.');
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
      setProfileNotice({
        type: 'success',
        title: 'Profile Photo Removed',
        message: 'Your profile picture has been removed.'
      });
    } else {
      setPhotoError(result.message || 'Failed to remove profile picture.');
    }
  };
  return <div className="profile-page-container">
      {profileNotice && <Alert type={profileNotice.type} title={profileNotice.title} message={profileNotice.message} />}

      {/* Profile Header Card */}
      <div className="profile-hero-card">
        <div className="profile-avatar-section">
          <div className="profile-avatar-circle">
            <AccountAvatar account={user} className="account-avatar-large" />
          </div>
          <button type="button" className="change-photo-btn" onClick={handleOpenPhotoModal} title="Change Profile Photo">
            <CameraIcon width="14" height="14" weight="regular" aria-hidden="true" />
            <span>Update Photo</span>
          </button>
        </div>

        <div className="profile-hero-info">
          <div className="profile-role-row">
            <h1 className="profile-user-title">{profileData.fullName}</h1>
            <RoleBadge role={primaryRole} />
          </div>
          <p className="profile-email-line">{user?.email || '—'}</p>
          <div className="profile-meta-tags">
            <span className="meta-tag">Customer Account</span>
            <span className="meta-tag">Registered email</span>
            <span className="meta-tag">Self-Service Access</span>
          </div>
        </div>

        <div className="profile-header-actions">
          <Button variant="outline" onClick={() => setIsPasswordModalOpen(true)}>
            Change Password
          </Button>
        </div>
      </div>

      {/* Two Column Profile Data Grid */}
      <div className="profile-sections-grid">
        {/* Personal Details Panel */}
        <div className="profile-card-panel">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Personal Information</h3>
            <span className="read-only-badge">Read only</span>
          </div>

          <p className="profile-details-note">These are your saved account details. Contact information editing is not available yet.</p>
          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Full Name</span>
              <span className="info-value">{profileData.fullName}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Account Username</span>
              <span className="info-value">{user?.username || '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Email Address</span>
              <span className="info-value">{user?.email || '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Phone Number</span>
              <span className="info-value">{profileData.phone}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Address</span>
              <span className="info-value">{profileData.address}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Driving License Number</span>
              <span className="info-value">{profileData.licenseNumber}</span>
            </div>
          </div>
        </div>

        {/* Security & Access Panel */}
        <div className="profile-card-panel">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Password & account access</h3>
          </div>

          <div className="security-status-list">
            <div className="security-item">
              <div className="sec-icon-box success">
                <ShieldCheckIcon size={20} weight="regular" aria-hidden="true" />
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Authentication Password</strong>
                <p className="sec-desc">Use a strong, unique password for your account.</p>
              </div>
              <Button variant="outline" size="sm" onClick={() => setIsPasswordModalOpen(true)}>
                Change
              </Button>
            </div>

            <div className="security-item">
              <div className="sec-icon-box success">
                <LockKeyIcon size={20} weight="regular" aria-hidden="true" />
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Account Security</strong>
                <p className="sec-desc">Signed in with customer access</p>
              </div>
              <span className="sec-badge-active">Customer</span>
            </div>

            <div className="security-item">
              <div className="sec-icon-box info">
                <LockKeyIcon size={20} weight="regular" aria-hidden="true" />
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Active Session</strong>
                <p className="sec-desc">Secure authenticated session</p>
              </div>
              <span className="sec-badge-active">Online</span>
            </div>
          </div>
        </div>
      </div>

      {/* Change Password Modal */}
      <ChangePasswordModal isOpen={isPasswordModalOpen} onClose={() => setIsPasswordModalOpen(false)} />

      {/* Photo Upload Modal */}
      <Modal isOpen={isPhotoModalOpen} onClose={() => setIsPhotoModalOpen(false)} title="Update Profile Photo" subtitle="Select a profile avatar image (PNG, JPG, WebP up to 5 MB).">
        <div className="photo-modal-body">
          {photoError && <div className="view-4933c088">
              <Alert type="error" title="Photo Error" message={photoError} />
            </div>}

          <div className="photo-preview-box">
            {photoPreview ? <UploadedImage src={photoPreview} alt="Selected preview" className="modal-avatar-preview" /> : <div className="avatar-placeholder-large">
                <UserCircleIcon width="48" height="48" weight="regular" aria-hidden="true" />
              </div>}
          </div>

          <div className="file-input-wrapper">
            <input type="file" id="avatarFile" accept="image/jpeg,image/png,image/webp" onChange={handlePhotoSelect} className="file-input-native" disabled={isSavingPhoto || isDeletingPhoto} />
            <label htmlFor="avatarFile" className="file-input-label">
              <UploadSimpleIcon width="18" height="18" weight="regular" aria-hidden="true" />
              <span>{photoFile ? photoFile.name : 'Choose Image File'}</span>
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
    </div>;
};
export default CustomerProfilePage;
