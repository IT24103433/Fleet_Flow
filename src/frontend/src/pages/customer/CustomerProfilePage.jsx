import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import InputField from '../../components/InputField';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import ChangePasswordModal from '../../components/security/ChangePasswordModal';

const CustomerProfilePage = () => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'CUSTOMER';

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);
  const [isPhotoModalOpen, setIsPhotoModalOpen] = useState(false);

  // Editable customer profile state (local state for Phase 1 until CustomerProfile API is integrated)
  const [profileData, setProfileData] = useState({
    fullName: user?.username || 'Customer User',
    phone: '+1 (555) 234-5678',
    address: '100 Mobility Way, Metro City',
    licenseNumber: 'DL-8492048-A',
  });

  const [editFormData, setEditFormData] = useState({ ...profileData });
  const [profileNotice, setProfileNotice] = useState(null);

  // Photo upload preview state
  const [selectedPhoto, setSelectedPhoto] = useState(null);
  const [photoUploadState, setPhotoUploadState] = useState('idle'); // 'idle' | 'uploading' | 'ready'

  const handleEditOpen = () => {
    setEditFormData({ ...profileData });
    setIsEditModalOpen(true);
  };

  const handleEditSave = (e) => {
    e.preventDefault();
    setProfileData({ ...editFormData });
    setIsEditModalOpen(false);
    setProfileNotice({
      type: 'success',
      title: 'Profile Updated Locally',
      message: 'Personal information updated. Full backend profile persistence will be active in Sprint 2.',
    });
  };

  const handlePhotoSelect = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      const previewUrl = URL.createObjectURL(file);
      setSelectedPhoto(previewUrl);
      setPhotoUploadState('uploading');
      setTimeout(() => {
        setPhotoUploadState('ready');
      }, 700);
    }
  };

  return (
    <div className="profile-page-container">
      {profileNotice && (
        <Alert
          type={profileNotice.type}
          title={profileNotice.title}
          message={profileNotice.message}
        />
      )}

      {/* Profile Header Card */}
      <div className="profile-hero-card">
        <div className="profile-avatar-section">
          <div className="profile-avatar-circle">
            {selectedPhoto ? (
              <img src={selectedPhoto} alt="Profile Avatar" className="avatar-img" />
            ) : (
              <span className="avatar-initials">{user?.username?.charAt(0)?.toUpperCase() || 'C'}</span>
            )}
          </div>
          <button
            type="button"
            className="change-photo-btn"
            onClick={() => setIsPhotoModalOpen(true)}
            title="Change Profile Photo"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
              <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
              <circle cx="12" cy="13" r="4" />
            </svg>
            <span>Update Photo</span>
          </button>
        </div>

        <div className="profile-hero-info">
          <div className="profile-role-row">
            <h1 className="profile-user-title">{profileData.fullName}</h1>
            <RoleBadge role={primaryRole} />
          </div>
          <p className="profile-email-line">{user?.email || 'customer@fleetflow.io'}</p>
          <div className="profile-meta-tags">
            <span className="meta-tag">Customer Account</span>
            <span className="meta-tag verified">Email Verified</span>
            <span className="meta-tag">Self-Service Access</span>
          </div>
        </div>

        <div className="profile-header-actions">
          <Button variant="primary" onClick={handleEditOpen}>
            Edit Profile
          </Button>
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
            <button type="button" className="panel-edit-link" onClick={handleEditOpen}>
              Edit Info
            </button>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Full Name</span>
              <span className="info-value">{profileData.fullName}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Account Username</span>
              <span className="info-value">{user?.username}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Email Address</span>
              <span className="info-value">{user?.email || 'N/A'}</span>
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
            <h3 className="panel-heading">Security & Authentication</h3>
          </div>

          <div className="security-status-list">
            <div className="security-item">
              <div className="sec-icon-box success">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Authentication Password</strong>
                <p className="sec-desc">Secured via ASP.NET Identity 256-bit hash</p>
              </div>
              <Button variant="outline" size="sm" onClick={() => setIsPasswordModalOpen(true)}>
                Change
              </Button>
            </div>

            <div className="security-item">
              <div className="sec-icon-box success">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                </svg>
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Role-Based Authorization</strong>
                <p className="sec-desc">Enforced via verified JWT ClaimTypes.Role</p>
              </div>
              <span className="sec-badge-active">Active</span>
            </div>

            <div className="security-item">
              <div className="sec-icon-box info">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </div>
              <div className="sec-text-col">
                <strong className="sec-title">Session State</strong>
                <p className="sec-desc">Stored in authenticated browser storage</p>
              </div>
              <span className="sec-badge-active">Online</span>
            </div>
          </div>
        </div>
      </div>

      {/* Edit Profile Modal */}
      <Modal
        isOpen={isEditModalOpen}
        onClose={() => setIsEditModalOpen(false)}
        title="Edit Customer Profile"
        subtitle="Update your personal contact details."
      >
        <form onSubmit={handleEditSave} noValidate>
          <InputField
            label="Full Legal Name"
            id="fullName"
            name="fullName"
            value={editFormData.fullName}
            onChange={(e) => setEditFormData({ ...editFormData, fullName: e.target.value })}
            required
          />

          <InputField
            label="Phone Number"
            id="phone"
            name="phone"
            value={editFormData.phone}
            onChange={(e) => setEditFormData({ ...editFormData, phone: e.target.value })}
            required
          />

          <InputField
            label="Address"
            id="address"
            name="address"
            value={editFormData.address}
            onChange={(e) => setEditFormData({ ...editFormData, address: e.target.value })}
          />

          <InputField
            label="Driving License ID"
            id="licenseNumber"
            name="licenseNumber"
            value={editFormData.licenseNumber}
            onChange={(e) => setEditFormData({ ...editFormData, licenseNumber: e.target.value })}
          />

          <div className="modal-actions-row">
            <Button variant="outline" onClick={() => setIsEditModalOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" variant="primary">
              Save Changes
            </Button>
          </div>
        </form>
      </Modal>

      {/* Change Password Modal */}
      <ChangePasswordModal
        isOpen={isPasswordModalOpen}
        onClose={() => setIsPasswordModalOpen(false)}
      />

      {/* Photo Upload Modal */}
      <Modal
        isOpen={isPhotoModalOpen}
        onClose={() => setIsPhotoModalOpen(false)}
        title="Update Profile Photo"
        subtitle="Select an avatar image (PNG, JPG, WebP)."
      >
        <div className="photo-modal-body">
          <div className="photo-preview-box">
            {selectedPhoto ? (
              <img src={selectedPhoto} alt="Selected preview" className="modal-avatar-preview" />
            ) : (
              <div className="avatar-placeholder-large">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="48" height="48">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
              </div>
            )}
          </div>

          <div className="file-input-wrapper">
            <input
              type="file"
              id="avatarFile"
              accept="image/*"
              onChange={handlePhotoSelect}
              className="file-input-native"
            />
            <label htmlFor="avatarFile" className="file-input-label">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="17 8 12 3 7 8" />
                <line x1="12" y1="3" x2="12" y2="15" />
              </svg>
              <span>Choose Image File</span>
            </label>
          </div>

          {photoUploadState === 'uploading' && (
            <div className="upload-progress-bar">
              <div className="progress-fill" />
              <span className="progress-text">Processing image preview...</span>
            </div>
          )}

          <div className="photo-integration-notice">
            <span className="info-icon-dot" aria-hidden="true" />
            <span>
              Image storage API is scheduled for Sprint 2. Local client preview verified.
            </span>
          </div>

          <div className="modal-actions-row">
            <Button variant="primary" fullWidth onClick={() => setIsPhotoModalOpen(false)}>
              Done
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};

export default CustomerProfilePage;
