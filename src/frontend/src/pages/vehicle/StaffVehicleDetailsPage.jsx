import React, { useState, useEffect, useMemo } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getVehicleById, updateVehicleStatus } from '../../services/vehicleService';
import { formatPriceNumber } from '../../utils/currencyUtils';
import {
  getAvailableStatusTransitionsForRoles,
  canUserChangeStatus,
  validateStatusChange,
} from '../../validation/vehicleStatusValidation';

const StaffVehicleDetailsPage = ({ selectedVehicle, onNavigate, onSelectVehicle }) => {
  const { roles, token } = useAuth();
  const canEditVehicle = (roles || []).some((r) =>
    ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase())
  );
  const canManageStatus = canUserChangeStatus(roles);

  const [vehicle, setVehicle] = useState(selectedVehicle);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  // Status Change Modal States
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);

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

  useEffect(() => {
    let isMounted = true;
    if (selectedVehicle?.id) {
      getVehicleById(selectedVehicle.id).then((result) => {
        if (!isMounted) return;
        setIsLoading(false);
        if (result.success && result.data) {
          setVehicle(result.data);
          if (onSelectVehicle) {
            onSelectVehicle(result.data);
          }
        } else if (result.status === 404) {
          setErrorMessage('Vehicle record was not found in the fleet catalog.');
        } else if (!selectedVehicle) {
          setErrorMessage(result.message || 'Failed to retrieve vehicle details.');
        }
      });
    }
    return () => {
      isMounted = false;
    };
  }, [selectedVehicle, onSelectVehicle]);

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
          {canEditVehicle && (
            <>
              <Button variant="primary" onClick={handleGoToEdit}>
                Edit Vehicle
              </Button>
              <Button variant="outline" onClick={handleGoToPhotos}>
                Photos
              </Button>
            </>
          )}
          <Button variant="outline" onClick={() => onNavigate('manage-fleet')}>
            Back to Inventory
          </Button>
        </div>
      </div>

      {statusSuccessMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert type="success" title="Status Updated" message={statusSuccessMessage} />
        </div>
      )}

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
    </div>
  );
};

export default StaffVehicleDetailsPage;
