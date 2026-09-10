import React, { useState, useEffect } from 'react';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import Modal from '../../components/common/Modal';
import InputField from '../../components/InputField';
import { useAuth } from '../../context/AuthContext';
import { getVehicleImages, uploadVehicleImage, deleteVehicleImage } from '../../services/vehicleImageService';
import { getVehicleImageUrl } from '../../utils/imageUrlUtils';

const VehicleImageManagementPage = ({ selectedVehicle, onNavigate }) => {
  const { token } = useAuth();
  const [images, setImages] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [notice, setNotice] = useState(null);

  // Upload modal state
  const [selectedFile, setSelectedFile] = useState(null);
  const [pendingPreviewUrl, setPendingPreviewUrl] = useState(null);
  const [caption, setCaption] = useState('');
  const [isCaptionModalOpen, setIsCaptionModalOpen] = useState(false);

  const vehicle = selectedVehicle;
  const vehicleId = vehicle?.id;

  useEffect(() => {
    let isMounted = true;
    if (!vehicleId) return;

    getVehicleImages(vehicleId, token).then((result) => {
      if (!isMounted) return;
      setIsLoading(false);
      if (result.success) {
        setImages(result.data);
      } else {
        setNotice({
          type: 'error',
          title: 'Error Loading Photos',
          message: result.message || 'Could not load photos for this vehicle.',
        });
      }
    });

    return () => {
      isMounted = false;
    };
  }, [vehicleId, token]);

  if (!vehicle) {
    return (
      <div className="vehicle-image-mgmt-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              ← Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Vehicle Photo Asset Workspace</h1>
          </div>
        </div>
        <div className="empty-state-card" style={{ padding: 'var(--space-8)' }}>
          <h3>No Vehicle Selected</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '16px' }}>
            Please select a vehicle from Fleet Inventory to inspect or manage its photo assets.
          </p>
          <Button variant="primary" onClick={() => onNavigate('manage-fleet')}>
            Go to Fleet Inventory
          </Button>
        </div>
      </div>
    );
  }

  const primaryImage = images[0] || null;
  const galleryImages = images.slice(1);

  const handleFileSelect = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      setSelectedFile(file);
      const previewUrl = URL.createObjectURL(file);
      setPendingPreviewUrl(previewUrl);
      const defaultCaption = file.name.replace(/\.[^/.]+$/, '').replace(/[-_]/g, ' ');
      setCaption(defaultCaption || 'Exterior View');
      setIsCaptionModalOpen(true);
    }
    e.target.value = '';
  };

  const handleConfirmUpload = async (e) => {
    e.preventDefault();
    if (!selectedFile || !vehicle?.id) return;

    setIsUploading(true);
    setNotice(null);

    const result = await uploadVehicleImage(vehicle.id, selectedFile, caption, token);
    setIsUploading(false);

    if (result.success && result.data) {
      setImages((prev) => [...prev, result.data]);
      setNotice({
        type: 'success',
        title: 'Photo Uploaded',
        message: 'Vehicle photo uploaded successfully and saved to storage.',
      });
      handleCloseModal();
    } else {
      setNotice({
        type: 'error',
        title: 'Upload Failed',
        message: result.message || 'Could not upload vehicle photo.',
      });
    }
  };

  const handleDeleteImage = async (imageId) => {
    if (!vehicle?.id || !imageId) return;
    setDeletingId(imageId);
    setNotice(null);

    const result = await deleteVehicleImage(vehicle.id, imageId, token);
    setDeletingId(null);

    if (result.success) {
      setImages((prev) => prev.filter((img) => img.id !== imageId));
      setNotice({
        type: 'success',
        title: 'Photo Deleted',
        message: 'Vehicle photo was permanently deleted from storage.',
      });
    } else {
      setNotice({
        type: 'error',
        title: 'Delete Failed',
        message: result.message || 'Could not delete vehicle photo.',
      });
    }
  };

  const handleCloseModal = () => {
    if (pendingPreviewUrl) {
      URL.revokeObjectURL(pendingPreviewUrl);
    }
    setIsCaptionModalOpen(false);
    setSelectedFile(null);
    setPendingPreviewUrl(null);
    setCaption('');
  };

  return (
    <div className="vehicle-image-mgmt-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            ← Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">
            Photo Management: {vehicle.year} {vehicle.make} {vehicle.model}
          </h1>
          <p className="admin-page-subtitle">
            Upload and organize high-resolution vehicle photos stored securely in backend persistent storage.
          </p>
        </div>

        <div className="admin-header-actions">
          <div className="file-input-wrapper">
            <input
              type="file"
              id="newVehiclePhoto"
              accept="image/jpeg,image/png,image/webp"
              onChange={handleFileSelect}
              className="file-input-native"
            />
            <label htmlFor="newVehiclePhoto" className="file-input-label">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="16" />
                <line x1="8" y1="12" x2="16" y2="12" />
              </svg>
              <span>+ Add Vehicle Photo</span>
            </label>
          </div>
        </div>
      </div>

      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      {/* Main Content Area */}
      {isLoading ? (
        <div className="empty-state-card" style={{ padding: 'var(--space-10) var(--space-6)', textAlign: 'center' }}>
          <p style={{ color: 'var(--color-text-secondary)', fontSize: '14px' }}>Loading photos from storage...</p>
        </div>
      ) : images.length === 0 ? (
        <div className="empty-state-card" style={{ padding: 'var(--space-10) var(--space-6)', textAlign: 'center' }}>
          <div style={{ color: 'var(--color-text-muted)', marginBottom: '16px' }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="48" height="48" style={{ margin: '0 auto' }}>
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <circle cx="8.5" cy="8.5" r="1.5" />
              <polyline points="21 15 16 10 5 21" />
            </svg>
          </div>
          <h3 style={{ fontSize: '18px', fontWeight: 600, color: 'var(--color-text-primary)', marginBottom: '8px' }}>
            No photos added yet
          </h3>
          <p style={{ color: 'var(--color-text-secondary)', fontSize: '14px', maxWidth: '460px', margin: '0 auto 20px' }}>
            Upload real photos to document this vehicle's appearance and condition.
          </p>
          <div className="file-input-wrapper" style={{ display: 'inline-block' }}>
            <input
              type="file"
              id="emptyStateVehiclePhoto"
              accept="image/jpeg,image/png,image/webp"
              onChange={handleFileSelect}
              className="file-input-native"
            />
            <label htmlFor="emptyStateVehiclePhoto" className="file-input-label" style={{ display: 'inline-flex' }}>
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="16" />
                <line x1="8" y1="12" x2="16" y2="12" />
              </svg>
              <span>+ Add Vehicle Photo</span>
            </label>
          </div>
        </div>
      ) : (
        <div className="image-mgmt-grid">
          {/* Primary Asset Card */}
          <div className="primary-asset-card">
            <div className="panel-header-strip">
              <h3 className="panel-heading">Primary Catalog Showcase</h3>
              <span className="primary-tag">Primary Badge</span>
            </div>

            <div className="primary-hero-preview">
              {primaryImage?.relativeUrl ? (
                <img
                  src={getVehicleImageUrl(primaryImage.relativeUrl)}
                  alt={primaryImage.caption || `${vehicle.make} ${vehicle.model}`}
                  className="primary-img-view"
                />
              ) : (
                <div className="primary-img-placeholder">
                  <span>Main Vehicle Photo</span>
                </div>
              )}
            </div>

            <div className="primary-meta-strip" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <strong className="primary-title">{primaryImage?.caption || primaryImage?.originalFileName || 'Primary Showcase'}</strong>
                <p className="primary-sub">Displayed on browse fleet cards and top hero view.</p>
              </div>
              <Button
                variant="danger"
                size="sm"
                onClick={() => handleDeleteImage(primaryImage.id)}
                disabled={deletingId === primaryImage.id}
                isLoading={deletingId === primaryImage.id}
              >
                Delete
              </Button>
            </div>
          </div>

          {/* Gallery Assets Grid */}
          <div className="gallery-assets-card">
            <div className="panel-header-strip">
              <h3 className="panel-heading">Additional Gallery Photos ({galleryImages.length})</h3>
            </div>

            {galleryImages.length === 0 ? (
              <div className="no-gallery-items">
                <p>No additional gallery photos added yet.</p>
              </div>
            ) : (
              <div className="gallery-items-grid">
                {galleryImages.map((img) => (
                  <div key={img.id} className="gallery-item-card">
                    <div className="gallery-item-thumb">
                      <img
                        src={getVehicleImageUrl(img.relativeUrl)}
                        alt={img.caption || img.originalFileName}
                        className="thumb-img"
                      />
                    </div>

                    <div className="gallery-item-info">
                      <span className="gallery-item-name">{img.caption || img.originalFileName}</span>
                      <div className="gallery-item-actions">
                        <button
                          type="button"
                          className="btn-gallery-action remove"
                          onClick={() => handleDeleteImage(img.id)}
                          disabled={deletingId === img.id}
                        >
                          {deletingId === img.id ? 'Deleting...' : 'Delete'}
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Caption & Description Entry Modal */}
      <Modal
        isOpen={isCaptionModalOpen}
        onClose={handleCloseModal}
        title="Upload Vehicle Photo"
        subtitle="Provide an optional description for this photo asset."
      >
        <form onSubmit={handleConfirmUpload}>
          <div style={{ marginBottom: 'var(--space-4)', textAlign: 'center' }}>
            {pendingPreviewUrl && (
              <img
                src={pendingPreviewUrl}
                alt="Selected preview"
                style={{
                  maxWidth: '100%',
                  maxHeight: '220px',
                  objectFit: 'contain',
                  borderRadius: 'var(--radius-sm)',
                  border: '1px solid var(--color-border)',
                }}
              />
            )}
          </div>

          <div style={{ marginBottom: 'var(--space-4)' }}>
            <InputField
              label="Photo Description / Caption (Optional)"
              id="photoCaption"
              name="photoCaption"
              value={caption}
              onChange={(e) => setCaption(e.target.value)}
              placeholder="e.g. Front exterior view, Rear cockpit"
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-3)' }}>
            <Button type="button" variant="outline" onClick={handleCloseModal} disabled={isUploading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={isUploading} isLoading={isUploading}>
              Upload Photo
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default VehicleImageManagementPage;
