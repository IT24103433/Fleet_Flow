import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import { ImagesIcon } from "@phosphor-icons/react/dist/csr/Images";
import { UploadSimpleIcon } from "@phosphor-icons/react/dist/csr/UploadSimple";
import UploadedImage from "../../components/common/UploadedImage";
import useConfirmAction from "../../components/common/useConfirmAction";
import React, { useState, useEffect } from 'react';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import Modal from '../../components/common/Modal';
import InputField from '../../components/InputField';
import { useAuth } from '../../context/AuthContext';
import { getVehicleImages, uploadVehicleImage, replaceVehicleImage, deleteVehicleImage } from '../../services/vehicleImageService';
import { getVehicleImageUrl } from '../../utils/imageUrlUtils';
const VehicleImageManagementPage = ({
  selectedVehicle,
  onNavigate
}) => {
  const {
    requestConfirmation,
    confirmationDialog
  } = useConfirmAction();
  const {
    roles,
    token
  } = useAuth();
  const canManagePhotos = (roles || []).some(r => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
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

  // Replace modal state
  const [replacingImage, setReplacingImage] = useState(null);
  const [replaceFile, setReplaceFile] = useState(null);
  const [replacePreviewUrl, setReplacePreviewUrl] = useState(null);
  const [replaceCaption, setReplaceCaption] = useState('');
  const [isReplaceModalOpen, setIsReplaceModalOpen] = useState(false);
  const [isReplacing, setIsReplacing] = useState(false);
  const vehicle = selectedVehicle;
  const vehicleId = vehicle?.id;
  useEffect(() => {
    let isMounted = true;
    if (!vehicleId) return;
    getVehicleImages(vehicleId, token).then(result => {
      if (!isMounted) return;
      setIsLoading(false);
      if (result.success) {
        setImages(result.data);
      } else {
        setNotice({
          type: 'error',
          title: 'Error Loading Photos',
          message: result.message || 'Could not load photos for this vehicle.'
        });
      }
    });
    return () => {
      isMounted = false;
    };
  }, [vehicleId, token]);
  if (!vehicle) {
    return <div className="vehicle-image-mgmt-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Vehicle Photo Asset Workspace</h1>
          </div>
        </div>
        <div className="empty-state-card view-79525567">
          <h3>No Vehicle Selected</h3>
          <p className="view-bd7f6405">
            Please select a vehicle from Fleet Inventory to inspect or manage its photo assets.
          </p>
          <Button variant="primary" onClick={() => onNavigate('manage-fleet')}>
            Go to Fleet Inventory
          </Button>
        </div>
      </div>;
  }
  const primaryImage = images[0] || null;
  const galleryImages = images.slice(1);
  const handleFileSelect = e => {
    if (!canManagePhotos) {
      setNotice({
        type: 'error',
        title: 'Unauthorized Action',
        message: 'Only Fleet Managers and Administrators have permission to upload photos.'
      });
      e.target.value = '';
      return;
    }
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) {
        setNotice({
          type: 'error',
          title: 'File Too Large',
          message: 'Selected photo exceeds the 5 MB limit. Please select a smaller image file.'
        });
        e.target.value = '';
        return;
      }
      if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
        setNotice({
          type: 'error',
          title: 'Unsupported Image Type',
          message: 'Only JPEG, PNG, and WebP image formats are accepted.'
        });
        e.target.value = '';
        return;
      }
      setSelectedFile(file);
      const previewUrl = URL.createObjectURL(file);
      setPendingPreviewUrl(previewUrl);
      const defaultCaption = file.name.replace(/\.[^/.]+$/, '').replace(/[-_]/g, ' ');
      setCaption(defaultCaption || 'Exterior View');
      setIsCaptionModalOpen(true);
    }
    e.target.value = '';
  };
  const handleConfirmUpload = async e => {
    e.preventDefault();
    if (!selectedFile || !vehicle?.id) return;
    setIsUploading(true);
    setNotice(null);
    const result = await uploadVehicleImage(vehicle.id, selectedFile, caption, token);
    setIsUploading(false);
    if (result.success && result.data) {
      setImages(prev => [...prev, result.data]);
      setNotice({
        type: 'success',
        title: 'Photo Uploaded',
        message: 'Vehicle photo uploaded successfully and saved to persistent file storage.'
      });
      handleCloseModal();
    } else {
      setNotice({
        type: 'error',
        title: 'Upload Failed',
        message: result.message || 'Could not upload vehicle photo.'
      });
    }
  };
  const handleOpenReplaceModal = img => {
    if (!canManagePhotos) {
      setNotice({
        type: 'error',
        title: 'Unauthorized Action',
        message: 'Only Fleet Managers and Administrators have permission to replace photos.'
      });
      return;
    }
    setReplacingImage(img);
    setReplaceFile(null);
    setReplacePreviewUrl(null);
    setReplaceCaption(img.caption || img.originalFileName || '');
    setIsReplaceModalOpen(true);
  };
  const handleCloseReplaceModal = () => {
    if (isReplacing) return;
    if (replacePreviewUrl) {
      URL.revokeObjectURL(replacePreviewUrl);
    }
    setIsReplaceModalOpen(false);
    setReplacingImage(null);
    setReplaceFile(null);
    setReplacePreviewUrl(null);
    setReplaceCaption('');
  };
  const handleReplaceFileSelect = e => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) {
        setNotice({
          type: 'error',
          title: 'File Too Large',
          message: 'Replacement photo exceeds the 5 MB limit. Please select a smaller image file.'
        });
        e.target.value = '';
        return;
      }
      if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
        setNotice({
          type: 'error',
          title: 'Unsupported Image Type',
          message: 'Only JPEG, PNG, and WebP image formats are accepted.'
        });
        e.target.value = '';
        return;
      }
      setReplaceFile(file);
      if (replacePreviewUrl) {
        URL.revokeObjectURL(replacePreviewUrl);
      }
      setReplacePreviewUrl(URL.createObjectURL(file));
    }
    e.target.value = '';
  };
  const handleConfirmReplace = async e => {
    e.preventDefault();
    if (!replaceFile || !vehicle?.id || !replacingImage?.id) {
      setNotice({
        type: 'error',
        title: 'Replacement File Required',
        message: 'Please select a replacement image file before confirming.'
      });
      return;
    }
    setIsReplacing(true);
    setNotice(null);
    const result = await replaceVehicleImage(vehicle.id, replacingImage.id, replaceFile, replaceCaption, token);
    setIsReplacing(false);
    if (result.success && result.data) {
      const updated = result.data;
      setImages(prev => prev.map(img => img.id === updated.id ? updated : img));
      setNotice({
        type: 'success',
        title: 'Photo Replaced',
        message: 'Vehicle photo was successfully replaced and updated in storage.'
      });
      handleCloseReplaceModal();
    } else {
      setNotice({
        type: 'error',
        title: 'Replacement Failed',
        message: result.message || 'Could not replace vehicle photo.'
      });
    }
  };
  const handleDeleteImage = async imageId => {
    if (!canManagePhotos) {
      setNotice({
        type: 'error',
        title: 'Unauthorized Action',
        message: 'Only Fleet Managers and Administrators have permission to delete photos.'
      });
      return;
    }
    requestConfirmation({
      title: "Delete this vehicle photo?",
      message: "The photo will be permanently removed. Upload it again if you need to restore it.",
      action: async () => {
        if (!vehicle?.id || !imageId) return;
        setDeletingId(imageId);
        setNotice(null);
        const result = await deleteVehicleImage(vehicle.id, imageId, token);
        setDeletingId(null);
        if (result.success) {
          setImages(prev => prev.filter(img => img.id !== imageId));
          setNotice({
            type: 'success',
            title: 'Photo Deleted',
            message: 'Vehicle photo was permanently deleted from storage.'
          });
        } else {
          setNotice({
            type: 'error',
            title: 'Delete Failed',
            message: result.message || 'Could not delete vehicle photo.'
          });
        }
      },
      label: "Delete photo",
      variant: "danger"
    });
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
  return <div className="vehicle-image-mgmt-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">
            Photo Management: {vehicle.year} {vehicle.make} {vehicle.model}
          </h1>
          <p className="admin-page-subtitle">
            Upload clear photographs and organise the images customers see.
          </p>
        </div>

        <div className="admin-header-actions">
          <div className="file-input-wrapper">
            <input type="file" id="newVehiclePhoto" accept="image/jpeg,image/png,image/webp" onChange={handleFileSelect} className="file-input-native" />
            <label htmlFor="newVehiclePhoto" className="file-input-label">
              <UploadSimpleIcon width="16" height="16" weight="regular" aria-hidden="true" />
              <span>Add Vehicle Photo</span>
            </label>
          </div>
        </div>
      </div>

      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      {/* Main Content Area */}
      {isLoading ? <div className="empty-state-card view-81b1c487">
          <p className="view-423e9651">Loading photos from storage...</p>
        </div> : images.length === 0 ? <div className="empty-state-card view-81b1c487">
          <div className="view-4ae5eb10">
            <ImagesIcon width="48" height="48" className="view-6002529c" weight="regular" aria-hidden="true" />
          </div>
          <h3 className="view-0109de75">
            No photos added yet
          </h3>
          <p className="view-a096a4a0">
            Upload real photos to document this vehicle's appearance and condition.
          </p>
          <div className="file-input-wrapper view-0524ca21">
            <input type="file" id="emptyStateVehiclePhoto" accept="image/jpeg,image/png,image/webp" onChange={handleFileSelect} className="file-input-native" />
            <label htmlFor="emptyStateVehiclePhoto" className="file-input-label view-d348b10f">
              <UploadSimpleIcon width="16" height="16" weight="regular" aria-hidden="true" />
              <span>Add Vehicle Photo</span>
            </label>
          </div>
        </div> : <div className="image-mgmt-grid">
          {/* Primary Asset Card */}
          <div className="primary-asset-card">
            <div className="panel-header-strip">
              <h3 className="panel-heading">Primary Catalog Showcase</h3>
              <span className="primary-tag">Primary Badge</span>
            </div>

            <div className="primary-hero-preview">
              {primaryImage?.relativeUrl ? <UploadedImage src={getVehicleImageUrl(primaryImage.relativeUrl)} alt={primaryImage.caption || `${vehicle.make} ${vehicle.model}`} className="primary-img-view" /> : <div className="primary-img-placeholder">
                  <span>Main Vehicle Photo</span>
                </div>}
            </div>

            <div className="primary-meta-strip view-3490dbc8">
              <div>
                <strong className="primary-title">{primaryImage?.caption || primaryImage?.originalFileName || 'Primary Showcase'}</strong>
                <p className="primary-sub">Displayed on browse fleet cards and top hero view.</p>
              </div>
              <div className="view-a9c77021">
                <Button variant="outline" size="sm" onClick={() => handleOpenReplaceModal(primaryImage)} disabled={!canManagePhotos}>
                  Replace
                </Button>
                <Button variant="danger" size="sm" onClick={() => handleDeleteImage(primaryImage.id)} disabled={!canManagePhotos || deletingId === primaryImage.id} isLoading={deletingId === primaryImage.id}>
                  Delete
                </Button>
              </div>
            </div>
          </div>

          {/* Gallery Assets Grid */}
          <div className="gallery-assets-card">
            <div className="panel-header-strip">
              <h3 className="panel-heading">Additional Gallery Photos ({galleryImages.length})</h3>
            </div>

            {galleryImages.length === 0 ? <div className="no-gallery-items">
                <p>No additional gallery photos added yet.</p>
              </div> : <div className="gallery-items-grid">
                {galleryImages.map(img => <div key={img.id} className="gallery-item-card">
                    <div className="gallery-item-thumb">
                      <UploadedImage src={getVehicleImageUrl(img.relativeUrl)} alt={img.caption || img.originalFileName} className="thumb-img" />
                    </div>

                    <div className="gallery-item-info">
                      <span className="gallery-item-name">{img.caption || img.originalFileName}</span>
                      <div className="gallery-item-actions">
                        <button type="button" className="btn-gallery-action replace" onClick={() => handleOpenReplaceModal(img)} disabled={!canManagePhotos}>
                          Replace
                        </button>
                        <button type="button" className="btn-gallery-action remove" onClick={() => handleDeleteImage(img.id)} disabled={!canManagePhotos || deletingId === img.id}>
                          {deletingId === img.id ? 'Deleting...' : 'Delete'}
                        </button>
                      </div>
                    </div>
                  </div>)}
              </div>}
          </div>
        </div>}

      {/* Caption & Description Entry Modal */}
      <Modal isOpen={isCaptionModalOpen} onClose={handleCloseModal} title="Upload Vehicle Photo" subtitle="Provide an optional description for this photo asset.">
        <form onSubmit={handleConfirmUpload}>
          <div className="view-ef60b435">
            {pendingPreviewUrl && <UploadedImage src={pendingPreviewUrl} alt="Selected preview" className="view-ffc0710b" />}
          </div>

          <div className="view-0636788d">
            <InputField label="Photo Description / Caption (Optional)" id="photoCaption" name="photoCaption" value={caption} onChange={e => setCaption(e.target.value)} placeholder="e.g. Front exterior view, Rear cockpit" />
          </div>

          <div className="view-8db44f75">
            <Button type="button" variant="outline" onClick={handleCloseModal} disabled={isUploading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={isUploading} isLoading={isUploading}>
              Upload Photo
            </Button>
          </div>
        </form>
      </Modal>

      {/* Replace Photo Modal */}
      <Modal isOpen={isReplaceModalOpen} onClose={handleCloseReplaceModal} title="Replace Vehicle Photo" subtitle="Select a new image file to replace this asset in storage.">
        <form onSubmit={handleConfirmReplace}>
          <div className="view-630ecae1">
            <div className="view-fb5ec070">
              <span className="view-90a10d1e">
                Current Photo
              </span>
              {replacingImage?.relativeUrl && <UploadedImage src={getVehicleImageUrl(replacingImage.relativeUrl)} alt="Current" className="view-48a15216" />}
            </div>
            <div className="view-fb5ec070">
              <span className="view-90a10d1e">
                Replacement Photo
              </span>
              {replacePreviewUrl ? <UploadedImage src={replacePreviewUrl} alt="New preview" className="view-3d316dd8" /> : <div className="view-bcbf45c0">
                  <span className="view-d8212c8d">Select file below</span>
                </div>}
            </div>
          </div>

          <div className="view-4933c088">
            <label htmlFor="replaceFileInput" className="view-83e308c1">
              Choose Replacement Image (JPEG, PNG, WebP up to 5 MB):
            </label>
            <input type="file" id="replaceFileInput" accept="image/jpeg,image/png,image/webp" onChange={handleReplaceFileSelect} className="view-5c740006" />
          </div>

          <div className="view-4933c088">
            <InputField label="Photo Description / Caption" id="replaceCaption" name="replaceCaption" value={replaceCaption} onChange={e => setReplaceCaption(e.target.value)} placeholder="e.g. Front 3/4 Exterior View" />
          </div>

          <div className="view-8db44f75">
            <Button type="button" variant="outline" onClick={handleCloseReplaceModal} disabled={isReplacing}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={isReplacing || !replaceFile} isLoading={isReplacing}>
              Confirm Replacement
            </Button>
          </div>
        </form>
      </Modal>
    {confirmationDialog}</div>;
};
export default VehicleImageManagementPage;
