import React, { useState, useEffect, useMemo } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getVehicleById, updateVehicleStatus, retireVehicle, reactivateVehicle } from '../../services/vehicleService';
import { getVehicleImages } from '../../services/vehicleImageService';
import { getVehicleImageUrl } from '../../utils/imageUrlUtils';
import { formatPriceNumber } from '../../utils/currencyUtils';
import {
  getAvailableStatusTransitionsForRoles,
  canUserChangeStatus,
  validateStatusChange,
  canUserRetireVehicle,
  validateRetirement,
} from '../../validation/vehicleStatusValidation';

const StaffVehicleDetailsPage = ({ selectedVehicle, onNavigate, onSelectVehicle }) => {
  const { roles, token } = useAuth();
  const canEditVehicle = (roles || []).some((r) =>
    ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase())
  );
  const canManageStatus = canUserChangeStatus(roles);
  const canRetireVehicle = canUserRetireVehicle(roles);

  const [vehicle, setVehicle] = useState(selectedVehicle);
  const [images, setImages] = useState([]);
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const [isLightboxOpen, setIsLightboxOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  // Status Change Modal States
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);

  // Retirement Modal States
  const [isRetireModalOpen, setIsRetireModalOpen] = useState(false);
  const [retireReason, setRetireReason] = useState('');
  const [isRetiring, setIsRetiring] = useState(false);
  const [retireError, setRetireError] = useState(null);

  const allowedStatusOptions = useMemo(() => {
    return getAvailableStatusTransitionsForRoles(roles);
  }, [roles]);

  const selectedStatusDesc = useMemo(() => {
    return allowedStatusOptions.find((o) => o.value === targetStatus)?.description || '';
  }, [allowedStatusOptions, targetStatus]);

  const handleOpenStatusModal = () => {
    if (!vehicle) return;
    const nextDefault = allowedStatusOptions.find((s) => s.value !== vehicle.status)?.value || allowedStatusOptions[0]?.value || '';
    setTargetStatus(nextDefault);
    setStatusModalError(null);
    setIsStatusModalOpen(true);
  };

  const handleCloseStatusModal = () => {
    if (isUpdatingStatus) return;
    setIsStatusModalOpen(false);
    setTargetStatus('');
    setStatusModalError(null);
  };

  const handleConfirmStatusChange = async () => {
    if (!vehicle) return;

    const validation = validateStatusChange(targetStatus, vehicle.status, roles);
    if (!validation.isValid) {
      setStatusModalError(validation.error);
      return;
    }

    setIsUpdatingStatus(true);
    setStatusModalError(null);

    const result = await updateVehicleStatus(vehicle.id, targetStatus, token);

    setIsUpdatingStatus(false);

    if (result.success) {
      const updated = result.data;
      setVehicle(updated);
      if (onSelectVehicle) {
        onSelectVehicle(updated);
      }
      setStatusSuccessMessage(`Vehicle operational status successfully updated to "${targetStatus}".`);
      setIsStatusModalOpen(false);
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setStatusModalError(result.message || 'Failed to update vehicle status.');
    }
  };

  const handleOpenRetireModal = () => {
    setRetireReason('');
    setRetireError(null);
    setIsRetireModalOpen(true);
  };

  const handleCloseRetireModal = () => {
    if (isRetiring) return;
    setIsRetireModalOpen(false);
    setRetireReason('');
    setRetireError(null);
  };

  const handleConfirmRetire = async () => {
    if (!vehicle) return;
    const validation = validateRetirement(vehicle, roles);
    if (!validation.isValid) {
      setRetireError(validation.error);
      return;
    }

    setIsRetiring(true);
    setRetireError(null);

    const result = await retireVehicle(vehicle.id, retireReason, token);
    setIsRetiring(false);

    if (result.success) {
      const updated = result.data;
      setVehicle(updated);
      if (onSelectVehicle) {
        onSelectVehicle(updated);
      }
      setStatusSuccessMessage(
        `Vehicle ${vehicle.licensePlate || vehicle.vin} successfully retired and decommissioned from active fleet views. All specifications and historical logs are preserved.`
      );
      setIsRetireModalOpen(false);
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setRetireError(result.message || 'Failed to retire vehicle.');
    }
  };

  const handleReactivate = async () => {
    if (!vehicle || !canRetireVehicle) return;
    const confirmed = window.confirm(
      `Reactivate vehicle ${vehicle.licensePlate || vehicle.vin} (${vehicle.year} ${vehicle.make} ${vehicle.model}) back to Available status in active fleet inventory?`
    );
    if (!confirmed) return;

    const result = await reactivateVehicle(vehicle.id, token);
    if (result.success) {
      const updated = result.data;
      setVehicle(updated);
      if (onSelectVehicle) {
        onSelectVehicle(updated);
      }
      setStatusSuccessMessage(
        `Vehicle ${vehicle.licensePlate || vehicle.vin} reactivated and restored to active fleet operations.`
      );
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setErrorMessage(result.message || 'Failed to reactivate vehicle.');
    }
  };

  useEffect(() => {
    let isMounted = true;
    if (selectedVehicle?.id) {
      Promise.all([
        getVehicleById(selectedVehicle.id),
        getVehicleImages(selectedVehicle.id, token),
      ]).then(([vehRes, imgRes]) => {
        if (!isMounted) return;
        setIsLoading(false);
        if (vehRes.success && vehRes.data) {
          setVehicle(vehRes.data);
          if (onSelectVehicle) {
            onSelectVehicle(vehRes.data);
          }
        } else if (vehRes.status === 404) {
          setErrorMessage('Vehicle record was not found in the fleet catalog.');
        } else if (!selectedVehicle) {
          setErrorMessage(vehRes.message || 'Failed to retrieve vehicle details.');
        }

        if (imgRes.success && Array.isArray(imgRes.data)) {
          setImages(imgRes.data);
          setSelectedImageIndex(0);
        } else if (vehRes.data?.images && Array.isArray(vehRes.data.images)) {
          setImages(vehRes.data.images);
          setSelectedImageIndex(0);
        }
      });
    }
    return () => {
      isMounted = false;
    };
  }, [selectedVehicle, onSelectVehicle, token]);

  const handleGoToEdit = () => {
    if (onSelectVehicle && vehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('edit-vehicle');
  };

  const handleGoToPhotos = () => {
    if (onSelectVehicle && vehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-images');
  };

  if (isLoading) {
    return (
      <div className="admin-user-details-container" style={{ textAlign: 'center', padding: 'var(--space-12) 0' }}>
        <div className="spinner" style={{ width: '32px', height: '32px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
        <p>Loading vehicle specification details from FleetService...</p>
      </div>
    );
  }

  if (errorMessage && !vehicle) {
    return (
      <div className="admin-user-details-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              ← Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Vehicle Inspection</h1>
          </div>
        </div>
        <Alert type="error" title="Fleet Record Error" message={errorMessage} />
      </div>
    );
  }

  if (!vehicle) {
    return (
      <div className="admin-user-details-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              ← Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Vehicle Inspection</h1>
          </div>
        </div>
        <div className="create-user-card" style={{ padding: 'var(--space-8)', textAlign: 'center' }}>
          <h3>No Vehicle Selected</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '16px' }}>
            Please select a vehicle from the fleet inventory table to inspect specifications.
          </p>
          <Button variant="primary" onClick={() => onNavigate('manage-fleet')}>
            Return to Fleet Inventory
          </Button>
        </div>
      </div>
    );
  }

  const categoryName = vehicle.categoryName || vehicle.category?.name || '—';
  const fuelType = vehicle.fuelType || vehicle.fuel || '—';
  const transmission = vehicle.transmission || '—';
  const seating = vehicle.seatingCapacity || vehicle.seating || '—';
  const hubLocation = vehicle.hubLocation || vehicle.hub || '—';
  const licensePlate = vehicle.licensePlate || vehicle.plate || '—';
  const mileage = typeof vehicle.mileage === 'number' ? `${vehicle.mileage.toLocaleString()} mi` : (vehicle.mileage ? `${vehicle.mileage} mi` : '—');
  const createdStr = vehicle.createdAt
    ? (vehicle.createdAt.includes('T') ? vehicle.createdAt.split('T')[0] : vehicle.createdAt)
    : '—';
  const updatedStr = vehicle.updatedAt
    ? (vehicle.updatedAt.includes('T') ? vehicle.updatedAt.split('T')[0] : vehicle.updatedAt)
    : '—';

  return (
    <div className="admin-user-details-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            ← Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">
            {vehicle.year} {vehicle.make} {vehicle.model}
          </h1>
          <p className="admin-page-subtitle">
            Operational vehicle inspection, powertrain specifications, and inventory registry data.
          </p>
        </div>

        <div className="admin-header-actions">
          {canManageStatus && (
            <Button variant="secondary" onClick={handleOpenStatusModal}>
              Change Status
            </Button>
          )}
          {canRetireVehicle && (
            vehicle.status === 'Retired' ? (
              <Button variant="secondary" onClick={handleReactivate}>
                Reactivate Vehicle
              </Button>
            ) : (
              <Button
                variant="danger"
                onClick={handleOpenRetireModal}
                disabled={vehicle.status === 'InUse'}
                title={vehicle.status === 'InUse' ? 'Cannot retire a vehicle in active customer use' : 'Retire or Decommission Vehicle'}
              >
                Retire Vehicle
              </Button>
            )
          )}
          {canEditVehicle && (
            <>
              <Button variant="primary" onClick={handleGoToEdit}>
                Edit Vehicle
              </Button>
              <Button variant="outline" onClick={handleGoToPhotos}>
                Photos ({images.length})
              </Button>
            </>
          )}
          <Button variant="outline" onClick={() => onNavigate('manage-fleet')}>
            Back to Inventory
          </Button>
        </div>
      </div>

      {/* Decommissioned Warning Banner */}
      {vehicle.status === 'Retired' && (
        <div className="decommissioned-alert-banner" style={{ marginBottom: '16px' }}>
          <div className="decommissioned-alert-icon">⚠️</div>
          <div>
            <strong className="decommissioned-alert-title">Decommissioned Unit — Retired from Active Fleet</strong>
            <p className="decommissioned-alert-desc">
              This vehicle is no longer in commercial service and is excluded from normal active-fleet queries and customer rental dispatch.
              Complete specification details, asset registration, mileage logs, and historical photo records remain permanently preserved in the fleet archive.
            </p>
          </div>
        </div>
      )}

      {statusSuccessMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert type="success" title="Status Updated" message={statusSuccessMessage} />
        </div>
      )}

      {/* Vehicle Media Showcase & Photo Gallery */}
      <div className="details-panel-card" style={{ marginBottom: 'var(--space-6)' }}>
        <div className="panel-header-strip">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <h3 className="panel-heading">Vehicle Photo Gallery</h3>
            <span className="photo-count-pill">{images.length} {images.length === 1 ? 'Photo' : 'Photos'}</span>
          </div>
          {canEditVehicle && (
            <Button variant="outline" size="sm" onClick={handleGoToPhotos}>
              Manage Photos & Uploads
            </Button>
          )}
        </div>

        {images.length === 0 ? (
          <div className="empty-gallery-box">
            <div style={{ fontSize: '36px', marginBottom: '8px' }}>📷</div>
            <h4 style={{ margin: '0 0 4px', color: 'var(--color-text-primary)' }}>No vehicle photos added yet</h4>
            <p style={{ color: 'var(--color-text-secondary)', fontSize: '13px', margin: '0 0 16px' }}>
              Upload authentic vehicle photos to document unit condition and showcase the vehicle to customers.
            </p>
            {canEditVehicle && (
              <Button variant="primary" size="sm" onClick={handleGoToPhotos}>
                + Upload Vehicle Photos
              </Button>
            )}
          </div>
        ) : (
          <div className="vehicle-gallery-layout">
            <div className="gallery-primary-container" onClick={() => setIsLightboxOpen(true)} title="Click to view full-size photo">
              <img
                src={getVehicleImageUrl(images[selectedImageIndex]?.relativeUrl)}
                alt={images[selectedImageIndex]?.caption || `${vehicle.make} ${vehicle.model}`}
                className="gallery-primary-img"
              />
              <div className="gallery-img-caption-strip">
                <span>{images[selectedImageIndex]?.caption || images[selectedImageIndex]?.originalFileName || 'Vehicle Showcase'}</span>
                <span className="gallery-counter-pill">{selectedImageIndex + 1} of {images.length} • Click to Zoom</span>
              </div>
            </div>

            {images.length > 1 && (
              <div className="gallery-thumbnails-strip">
                {images.map((img, idx) => (
                  <button
                    type="button"
                    key={img.id}
                    className={`gallery-thumb-btn ${idx === selectedImageIndex ? 'active' : ''}`}
                    onClick={() => setSelectedImageIndex(idx)}
                    title={img.caption || `View photo ${idx + 1}`}
                  >
                    <img
                      src={getVehicleImageUrl(img.relativeUrl)}
                      alt={img.caption || `Thumbnail ${idx + 1}`}
                      className="gallery-thumb-img"
                    />
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      <div className="user-details-grid">
        {/* Vehicle Identity & Specification Card */}
        <div className="details-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Vehicle Specifications</h3>
            <StatusBadge status={vehicle.status} />
          </div>

          <div className="details-avatar-row">
            <div className="details-avatar-circle" style={{ backgroundColor: '#0F172A', color: '#38BDF8', fontSize: '24px' }}>
              🚗
            </div>
            <div>
              <h2 className="details-username">{vehicle.year} {vehicle.make} {vehicle.model}</h2>
              <p className="details-email">{categoryName} • {fuelType}</p>
              <div style={{ marginTop: '6px' }}>
                <span className="plate-pill">{licensePlate}</span>
              </div>
            </div>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Vehicle Identification (VIN)</span>
              <span className="info-value font-mono" style={{ fontSize: '12px' }}>{vehicle.vin}</span>
            </div>
            <div className="info-row">
              <span className="info-label">License Plate</span>
              <span className="info-value font-mono">{licensePlate}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Manufacturer / Make</span>
              <span className="info-value">{vehicle.make}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Model</span>
              <span className="info-value">{vehicle.model}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Model Year</span>
              <span className="info-value">{vehicle.year}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Vehicle Category</span>
              <span className="info-value">{categoryName}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Daily Rental Rate</span>
              <span className="info-value" style={{ fontWeight: 600, color: 'var(--color-primary)' }}>
                {vehicle?.dailyRate != null && !isNaN(Number(vehicle.dailyRate))
                  ? `LKR ${formatPriceNumber(vehicle.dailyRate)} / day`
                  : '—'}
              </span>
            </div>
          </div>
        </div>

        {/* Technical & Operational Attributes Card */}
        <div className="details-panel-card">
          <div className="panel-header-strip">
            <h3 className="panel-heading">Technical & Station Assignment</h3>
            <span className="read-only-badge">Operations</span>
          </div>

          <div className="info-fields-list">
            <div className="info-row">
              <span className="info-label">Assigned Station Hub</span>
              <span className="info-value">{hubLocation}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Current Odometer</span>
              <span className="info-value font-mono">{mileage}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Powertrain / Fuel</span>
              <span className="info-value">{fuelType}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Transmission</span>
              <span className="info-value">{transmission}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Seating Capacity</span>
              <span className="info-value">{seating}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Unit Asset ID</span>
              <span className="info-value font-mono" style={{ fontSize: '11px' }}>{vehicle.id}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Ingested Date</span>
              <span className="info-value">{createdStr}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Last Modified</span>
              <span className="info-value">{updatedStr}</span>
            </div>
          </div>

          {canManageStatus && (
            <div className="admin-actions-box" style={{ marginTop: 'var(--space-6)' }}>
              <h4>Fleet Operations & Status Controls</h4>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '8px' }}>
                <Button variant="secondary" size="sm" onClick={handleOpenStatusModal}>
                  Update Operational Status
                </Button>
                {canRetireVehicle && (
                  vehicle.status === 'Retired' ? (
                    <Button variant="secondary" size="sm" onClick={handleReactivate}>
                      Reactivate to Fleet
                    </Button>
                  ) : (
                    <Button
                      variant="danger"
                      size="sm"
                      onClick={handleOpenRetireModal}
                      disabled={vehicle.status === 'InUse'}
                      title={vehicle.status === 'InUse' ? 'Cannot retire a vehicle in active customer use' : 'Retire or Decommission Vehicle'}
                    >
                      Decommission / Retire
                    </Button>
                  )
                )}
                {canEditVehicle && (
                  <>
                    <Button variant="primary" size="sm" onClick={handleGoToEdit}>
                      Modify Vehicle Specifications
                    </Button>
                    <Button variant="outline" size="sm" onClick={handleGoToPhotos}>
                      Photo Management
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Vehicle Operational Status Confirmation Modal */}
      <Modal
        isOpen={isStatusModalOpen}
        onClose={handleCloseStatusModal}
        title="Update Operational Status"
        subtitle={`Modify operational availability for ${vehicle?.year} ${vehicle?.make} ${vehicle?.model}`}
      >
        {statusModalError && (
          <div style={{ marginBottom: '16px' }}>
            <Alert type="error" title="Status Update Error" message={statusModalError} />
          </div>
        )}

        <div style={{ marginBottom: '1.25rem', fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', lineHeight: '1.5' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '12px 14px',
              backgroundColor: 'var(--color-surface, #f8fafc)',
              borderRadius: '8px',
              border: '1px solid var(--color-border, #e2e8f0)',
              marginBottom: '16px',
            }}
          >
            <div>
              <strong style={{ display: 'block', color: 'var(--color-text-primary, #0f172a)', fontSize: '14px' }}>
                {vehicle?.year} {vehicle?.make} {vehicle?.model}
              </strong>
              <span style={{ fontSize: '11px', color: 'var(--color-text-muted, #94a3b8)' }}>
                Plate: {vehicle?.licensePlate} • VIN: {vehicle?.vin}
              </span>
            </div>
            <div>
              <StatusBadge status={vehicle?.status} />
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <label
              htmlFor="detailTargetStatusSelect"
              style={{
                display: 'block',
                fontWeight: 600,
                color: 'var(--color-text-primary, #0f172a)',
                marginBottom: '6px',
              }}
            >
              Select New Operational Status:
            </label>
            <select
              id="detailTargetStatusSelect"
              value={targetStatus}
              onChange={(e) => {
                setTargetStatus(e.target.value);
                setStatusModalError(null);
              }}
              disabled={isUpdatingStatus}
              className="filter-select-input"
              style={{ width: '100%', padding: '10px 12px', fontSize: '14px', borderRadius: '6px' }}
            >
              {allowedStatusOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label} {opt.value === vehicle?.status ? '(Current)' : ''}
                </option>
              ))}
            </select>
            {selectedStatusDesc && (
              <p style={{ marginTop: '6px', fontSize: '12px', color: 'var(--color-text-muted, #94a3b8)' }}>
                {selectedStatusDesc}
              </p>
            )}
          </div>

          {targetStatus === 'Retired' && (
            <div
              style={{
                padding: '10px 12px',
                backgroundColor: '#FEF2F2',
                border: '1px solid #FECACA',
                borderRadius: '6px',
                color: '#991B1B',
                fontSize: '12px',
                marginBottom: '16px',
                lineHeight: '1.4',
              }}
            >
              <strong>Warning:</strong> Retiring a vehicle will permanently decommission this unit and remove it from commercial rental availability.
            </div>
          )}

          {targetStatus === 'Maintenance' && vehicle?.status !== 'Maintenance' && (
            <div
              style={{
                padding: '10px 12px',
                backgroundColor: '#FFFBEB',
                border: '1px solid #FDE68A',
                borderRadius: '6px',
                color: '#92400E',
                fontSize: '12px',
                marginBottom: '16px',
                lineHeight: '1.4',
              }}
            >
              <strong>Maintenance Notice:</strong> This unit will be marked out of service for inspection and repair. Customer bookings cannot be dispatched for this vehicle until restored to Available.
            </div>
          )}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseStatusModal} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button
            variant={targetStatus === 'Retired' ? 'danger' : 'primary'}
            onClick={handleConfirmStatusChange}
            disabled={isUpdatingStatus || targetStatus === vehicle?.status}
            isLoading={isUpdatingStatus}
          >
            Confirm Status Change
          </Button>
        </div>
      </Modal>

      {/* Vehicle Decommission / Retirement Confirmation Modal */}
      <Modal
        isOpen={isRetireModalOpen}
        onClose={handleCloseRetireModal}
        title="Retire & Decommission Vehicle"
        subtitle={`Decommission ${vehicle?.year} ${vehicle?.make} ${vehicle?.model} from active commercial service`}
      >
        {retireError && (
          <div style={{ marginBottom: '16px' }}>
            <Alert type="error" title="Decommission Error" message={retireError} />
          </div>
        )}

        <div style={{ marginBottom: '1.25rem', fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', lineHeight: '1.5' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '12px 14px',
              backgroundColor: 'var(--color-surface, #f8fafc)',
              borderRadius: '8px',
              border: '1px solid var(--color-border, #e2e8f0)',
              marginBottom: '16px',
            }}
          >
            <div>
              <strong style={{ display: 'block', color: 'var(--color-text-primary, #0f172a)', fontSize: '14px' }}>
                {vehicle?.year} {vehicle?.make} {vehicle?.model}
              </strong>
              <span style={{ fontSize: '11px', color: 'var(--color-text-muted, #94a3b8)' }}>
                Plate: {vehicle?.licensePlate} • VIN: {vehicle?.vin}
              </span>
            </div>
            <div>
              <StatusBadge status={vehicle?.status} />
            </div>
          </div>

          <div
            style={{
              padding: '12px 14px',
              backgroundColor: '#FEF2F2',
              border: '1px solid #FECACA',
              borderRadius: '8px',
              color: '#991B1B',
              fontSize: '13px',
              marginBottom: '16px',
              lineHeight: '1.45',
            }}
          >
            <div style={{ display: 'flex', gap: '8px' }}>
              <span style={{ fontSize: '16px' }}>⚠️</span>
              <div>
                <strong style={{ display: 'block', marginBottom: '4px' }}>Active Fleet Exclusion & History Preservation</strong>
                <p style={{ margin: 0 }}>
                  Retiring this vehicle will decommission it from service and exclude it from active-fleet views and customer rental bookings.
                  <strong> All specifications, vehicle identification, photos, and maintenance logs will remain permanently preserved in the database.</strong>
                </p>
              </div>
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <label
              htmlFor="staffRetireReasonInput"
              style={{
                display: 'block',
                fontWeight: 600,
                color: 'var(--color-text-primary, #0f172a)',
                marginBottom: '6px',
              }}
            >
              Retirement / Decommission Reason (Optional):
            </label>
            <input
              id="staffRetireReasonInput"
              type="text"
              placeholder="e.g., End of operational lease, total loss, sold to third-party"
              value={retireReason}
              onChange={(e) => setRetireReason(e.target.value)}
              disabled={isRetiring}
              className="filter-search-input"
              style={{ width: '100%', padding: '10px 12px', fontSize: '14px', borderRadius: '6px' }}
            />
          </div>
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseRetireModal} disabled={isRetiring}>
            Cancel
          </Button>
          <Button
            variant="danger"
            onClick={handleConfirmRetire}
            disabled={isRetiring}
            isLoading={isRetiring}
          >
            Confirm Decommission & Retire
          </Button>
        </div>
      </Modal>

      {/* Photo Lightbox Preview Modal */}
      <Modal
        isOpen={isLightboxOpen && images.length > 0}
        onClose={() => setIsLightboxOpen(false)}
        title={images[selectedImageIndex]?.caption || `${vehicle?.make} ${vehicle?.model} Photo`}
        subtitle={`Viewing photo ${selectedImageIndex + 1} of ${images.length}`}
      >
        <div style={{ textAlign: 'center' }}>
          {images[selectedImageIndex]?.relativeUrl && (
            <img
              src={getVehicleImageUrl(images[selectedImageIndex]?.relativeUrl)}
              alt={images[selectedImageIndex]?.caption || 'Full-size vehicle view'}
              style={{ maxWidth: '100%', maxHeight: '65vh', objectFit: 'contain', borderRadius: '8px' }}
            />
          )}
          <div style={{ marginTop: '12px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Button
              variant="outline"
              size="sm"
              disabled={selectedImageIndex === 0}
              onClick={() => setSelectedImageIndex((prev) => Math.max(0, prev - 1))}
            >
              ← Previous Photo
            </Button>
            <span style={{ fontSize: '13px', color: 'var(--color-text-secondary)' }}>
              {selectedImageIndex + 1} of {images.length}
            </span>
            <Button
              variant="outline"
              size="sm"
              disabled={selectedImageIndex >= images.length - 1}
              onClick={() => setSelectedImageIndex((prev) => Math.min(images.length - 1, prev + 1))}
            >
              Next Photo →
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};

export default StaffVehicleDetailsPage;
