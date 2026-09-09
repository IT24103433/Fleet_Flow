import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import Button from '../../components/common/Button';
import InputField from '../../components/InputField';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import ChangePasswordModal from '../../components/security/ChangePasswordModal';
import { uploadProfilePicture, deleteProfilePicture } from '../../services/authService';
import { getProfileImageUrl } from '../../utils/imageUrlUtils';

const StaffProfilePage = () => {
  const { user, roles, token, updateUser } = useAuth();
  const primaryRole = roles?.[0] || 'FLEET_MANAGER';

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);
  const [isPhotoModalOpen, setIsPhotoModalOpen] = useState(false);

  const [staffInfo, setStaffInfo] = useState({
    staffId: user?.id ? String(user.id).slice(0, 8).toUpperCase() : '—',
    department: primaryRole === 'MAINTENANCE_STAFF' ? 'Technical Services & Fleet Maintenance' : (primaryRole === 'ADMIN' ? 'Executive Administration' : 'Operations & Logistics'),
    workPhone: user?.phoneNumber || '—',
    officeLocation: user?.address || '—',
    assignedShift: 'Standard Operational Shift',
  });

  const [editFormData, setEditFormData] = useState({ ...staffInfo });
  const [notice, setNotice] = useState(null);

  // Persistent photo upload state
  const [photoFile, setPhotoFile] = useState(null);
  const [photoPreview, setPhotoPreview] = useState(null);
  const [isSavingPhoto, setIsSavingPhoto] = useState(false);
  const [isDeletingPhoto, setIsDeletingPhoto] = useState(false);
  const [photoError, setPhotoError] = useState(null);

  const handleEditOpen = () => {
    setEditFormData({ ...staffInfo });
    setIsEditModalOpen(true);
  };

  const handleEditSave = (e) => {
    e.preventDefault();
    setStaffInfo({ ...editFormData });
    setIsEditModalOpen(false);
    setNotice({
      type: 'success',
      title: 'Staff Details Updated',
      message: 'Operational contact information updated in local session.',
    });
  };

  const handleOpenPhotoModal = () => {
    setPhotoFile(null);
    setPhotoPreview(user?.profileImageUrl ? getProfileImageUrl(user.profileImageUrl) : null);
    setPhotoError(null);
    setIsPhotoModalOpen(true);
  };

  const handlePhotoSelect = (e) => {
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
      updateUser({ profileImageUrl: result.data.profileImageUrl });
      setIsPhotoModalOpen(false);
      setNotice({
        type: 'success',
        title: 'Staff Photo Updated',
        message: 'Your official identification photo was uploaded successfully.',
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
      updateUser({ profileImageUrl: null });
      setPhotoPreview(null);
      setPhotoFile(null);
      setIsPhotoModalOpen(false);
      setNotice({
        type: 'success',
        title: 'Staff Photo Removed',
        message: 'Your identification photo was removed.',
      });
    } else {
      setPhotoError(result.message || 'Failed to remove identification photo.');
    }
  };

  return (
    <div className="staff-profile-container">
      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      {/* Staff Hero Banner */}
      <div className="staff-profile-hero">
        <div className="staff-avatar-box">
          <div className="staff-avatar-large">
            {user?.profileImageUrl ? (
              <img src={getProfileImageUrl(user.profileImageUrl)} alt="Staff profile" className="avatar-img" />
            ) : (
              <span>{user?.username?.charAt(0)?.toUpperCase() || 'S'}</span>
            )}
          </div>
          <button
            type="button"
            className="change-photo-btn staff-theme"
            onClick={handleOpenPhotoModal}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
              <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
              <circle cx="12" cy="13" r="4" />
            </svg>
            <span>Update Photo</span>
          </button>
        </div>

        <div className="staff-meta-details">
          <div className="staff-title-row">
            <h1 className="staff-name-heading">{user?.username}</h1>
            <RoleBadge role={primaryRole} />
          </div>
          <p className="staff-dept-line">{staffInfo.department}</p>
          <div className="staff-badge-strip">
            <span className="staff-tag">ID: {staffInfo.staffId}</span>
            <span className="staff-tag active">Security Cleared</span>
            <span className="staff-tag">Verified Session</span>
          </div>
        </div>

        <div className="staff-hero-actions">
          <Button variant="primary" onClick={handleEditOpen}>
            Edit Contact Details
          </Button>
          <Button variant="outline" onClick={() => setIsPasswordModalOpen(true)}>
            Change Password
          </Button>
        </div>
      </div>

      {/* Grid of Profile Sections */}
      <div className="staff-sections-grid">
        {/* System & Identity Details (Read Only) */}
        <div className="staff-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">System & Identity Credentials</h3>
            <span className="read-only-badge">Administrative Managed</span>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Staff Identifier</span>
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
              <span className="info-label">Assigned Department</span>
              <span className="info-value">{staffInfo.department}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Account Status</span>
              <span className="info-value status-active">Active & Cleared</span>
            </div>
          </div>
        </div>

        {/* Contact & Station Location (Editable) */}
        <div className="staff-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Contact & Station Info</h3>
            <button type="button" className="panel-edit-link" onClick={handleEditOpen}>
              Edit Info
            </button>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Work Direct Line</span>
              <span className="info-value">{staffInfo.workPhone}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Assigned Shift</span>
              <span className="info-value">{staffInfo.assignedShift}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Station Hub</span>
              <span className="info-value">{staffInfo.officeLocation}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Session Status</span>
              <span className="info-value" style={{ color: '#10B981' }}>Active</span>
            </div>
          </div>
        </div>
      </div>

      {/* Edit Staff Contact Modal */}
      <Modal
        isOpen={isEditModalOpen}
        onClose={() => setIsEditModalOpen(false)}
        title="Edit Staff Contact Information"
        subtitle="Update work phone and station assignment."
      >
        <form onSubmit={handleEditSave} noValidate>
          <InputField
            label="Work Phone / Direct Extension"
            id="workPhone"
            name="workPhone"
            value={editFormData.workPhone}
            onChange={(e) => setEditFormData({ ...editFormData, workPhone: e.target.value })}
            required
          />

          <InputField
            label="Station Hub Location"
            id="officeLocation"
            name="officeLocation"
            value={editFormData.officeLocation}
            onChange={(e) => setEditFormData({ ...editFormData, officeLocation: e.target.value })}
            required
          />

          <InputField
            label="Assigned Shift Schedule"
            id="assignedShift"
            name="assignedShift"
            value={editFormData.assignedShift}
            onChange={(e) => setEditFormData({ ...editFormData, assignedShift: e.target.value })}
          />

          <div className="modal-actions-row">
            <Button variant="outline" onClick={() => setIsEditModalOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" variant="primary">
              Save Contact Info
            </Button>
          </div>
        </form>
      </Modal>

      {/* Change Password Modal */}
      <ChangePasswordModal
        isOpen={isPasswordModalOpen}
        onClose={() => setIsPasswordModalOpen(false)}
      />

      {/* Staff Photo Modal */}
      <Modal
        isOpen={isPhotoModalOpen}
        onClose={() => setIsPhotoModalOpen(false)}
        title="Update Staff Identification Photo"
        subtitle="Select an official employee photo file (PNG, JPG, WebP up to 5 MB)."
      >
        <div className="photo-modal-body">
          {photoError && (
            <div style={{ marginBottom: '16px' }}>
              <Alert type="error" title="Photo Error" message={photoError} />
            </div>
          )}

          <div className="photo-preview-box">
            {photoPreview ? (
              <img src={photoPreview} alt="Selected staff preview" className="modal-avatar-preview" />
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
              id="staffPhotoFile"
              accept="image/jpeg,image/png,image/webp"
              onChange={handlePhotoSelect}
              className="file-input-native"
              disabled={isSavingPhoto || isDeletingPhoto}
            />
            <label htmlFor="staffPhotoFile" className="file-input-label">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="17 8 12 3 7 8" />
                <line x1="12" y1="3" x2="12" y2="15" />
              </svg>
              <span>{photoFile ? photoFile.name : 'Choose Photo File'}</span>
            </label>
          </div>

          <div className="modal-actions-row" style={{ display: 'flex', gap: '8px', marginTop: '16px' }}>
            <Button
              variant="outline"
              onClick={() => setIsPhotoModalOpen(false)}
              disabled={isSavingPhoto || isDeletingPhoto}
            >
              Cancel
            </Button>
            {user?.profileImageUrl && (
              <Button
                variant="danger"
                onClick={handleDeletePhoto}
                disabled={isSavingPhoto || isDeletingPhoto}
              >
                {isDeletingPhoto ? 'Removing...' : 'Remove Photo'}
              </Button>
            )}
            <Button
              variant="primary"
              onClick={handleSavePhoto}
              disabled={!photoFile || isSavingPhoto || isDeletingPhoto}
            >
              {isSavingPhoto ? 'Uploading...' : 'Save Photo'}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};

export default StaffProfilePage;
