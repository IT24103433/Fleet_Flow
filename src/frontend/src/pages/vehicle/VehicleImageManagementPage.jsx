import React, { useState } from 'react';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import { INITIAL_VEHICLES } from '../../data/vehicleData';

const VehicleImageManagementPage = ({ selectedVehicle, onNavigate }) => {
  const vehicle = selectedVehicle || INITIAL_VEHICLES[0];

  const [images, setImages] = useState(
    vehicle.images || [
      { id: 'img-1', title: 'Front Three-Quarter Angle', isPrimary: true, url: null },
      { id: 'img-2', title: 'Driver Cockpit & Display', isPrimary: false, url: null },
      { id: 'img-3', title: 'Rear Fascia & Aerodynamics', isPrimary: false, url: null },
      { id: 'img-4', title: 'Executive Leather Seating', isPrimary: false, url: null },
    ]
  );

  const [uploadProgress, setUploadProgress] = useState(false);
  const [notice, setNotice] = useState(null);

  const primaryImage = images.find((img) => img.isPrimary) || images[0];
  const galleryImages = images.filter((img) => img.id !== primaryImage?.id);

  const handleSetPrimary = (imgId) => {
    setImages((prev) =>
      prev.map((img) => ({
        ...img,
        isPrimary: img.id === imgId,
      }))
    );
    setNotice({
      type: 'info',
      title: 'Primary Asset Updated',
      message: 'Primary display photo updated in local session.',
    });
  };

  const handleRemoveImage = (imgId) => {
    if (images.length <= 1) {
      setNotice({
        type: 'error',
        title: 'Minimum Asset Required',
        message: 'A vehicle must maintain at least one visual asset record.',
      });
      return;
    }
    setImages((prev) => prev.filter((img) => img.id !== imgId));
    setNotice({
      type: 'info',
      title: 'Asset Removed',
      message: 'Image removed from gallery list in local session.',
    });
  };

  const handleAddFile = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      const previewUrl = URL.createObjectURL(file);
      setUploadProgress(true);

      setTimeout(() => {
        setUploadProgress(false);
        const newImg = {
          id: 'img-' + Date.now(),
          title: file.name.replace(/\.[^/.]+$/, ''),
          isPrimary: false,
          url: previewUrl,
        };
        setImages((prev) => [...prev, newImg]);
        setNotice({
          type: 'success',
          title: 'Photo Upload Preview Ready',
          message: `Asset "${file.name}" added to client preview gallery. (Object storage connection active in Sprint 2).`,
        });
      }, 800);
    }
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
            Image Asset Management: {vehicle.year} {vehicle.make} {vehicle.model}
          </h1>
          <p className="admin-page-subtitle">
            Configure primary showcase photograph and secondary gallery angles for customer exploration.
          </p>
        </div>

        <div className="admin-header-actions">
          <div className="file-input-wrapper">
            <input
              type="file"
              id="newVehiclePhoto"
              accept="image/*"
              onChange={handleAddFile}
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

      {uploadProgress && (
        <div className="upload-progress-card">
          <span className="upload-spin-label">Processing and generating responsive client previews...</span>
          <div className="upload-progress-bar">
            <div className="progress-fill" />
          </div>
        </div>
      )}

      {/* Two Column Workspace: Primary on Left, Additional on Right */}
      <div className="image-mgmt-grid">
        {/* Primary Asset Card */}
        <div className="primary-asset-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Primary Catalog Showcase</h3>
            <span className="primary-tag">Primary Badge</span>
          </div>

          <div className="primary-hero-preview">
            {primaryImage?.url ? (
              <img src={primaryImage.url} alt={primaryImage.title} className="primary-img-view" />
            ) : (
              <div className="primary-img-placeholder">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="48" height="48">
                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                  <circle cx="8.5" cy="8.5" r="1.5" />
                  <polyline points="21 15 16 10 5 21" />
                </svg>
                <span>{primaryImage?.title || 'Main Vehicle Photo'}</span>
              </div>
            )}
          </div>

          <div className="primary-meta-strip">
            <div>
              <strong className="primary-title">{primaryImage?.title}</strong>
              <p className="primary-sub">Displayed on browse fleet cards and top hero view.</p>
            </div>
          </div>
        </div>

        {/* Gallery Assets Grid */}
        <div className="gallery-assets-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Secondary Gallery Angles ({galleryImages.length})</h3>
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
                    {img.url ? (
                      <img src={img.url} alt={img.title} className="thumb-img" />
                    ) : (
                      <span>{img.title}</span>
                    )}
                  </div>

                  <div className="gallery-item-info">
                    <span className="gallery-item-name">{img.title}</span>
                    <div className="gallery-item-actions">
                      <button
                        type="button"
                        className="btn-gallery-action set-primary"
                        onClick={() => handleSetPrimary(img.id)}
                      >
                        Make Primary
                      </button>
                      <button
                        type="button"
                        className="btn-gallery-action remove"
                        onClick={() => handleRemoveImage(img.id)}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default VehicleImageManagementPage;
