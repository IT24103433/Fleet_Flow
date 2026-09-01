import React, { useState } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { INITIAL_VEHICLES } from '../../data/vehicleData';

const STATUS_OPTIONS = ['ALL', 'AVAILABLE', 'IN_USE', 'MAINTENANCE', 'RETIRED'];

const ManageFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const [vehicles, setVehicles] = useState(INITIAL_VEHICLES);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedStatusFilter, setSelectedStatusFilter] = useState('ALL');

  // Status Change Modal State
  const [statusModalVehicle, setStatusModalVehicle] = useState(null);
  const [newStatus, setNewStatus] = useState('AVAILABLE');
  const [statusNotice, setStatusNotice] = useState(null);

  const filteredVehicles = vehicles.filter((v) => {
    const matchesSearch =
      v.model.toLowerCase().includes(searchTerm.toLowerCase()) ||
      v.make.toLowerCase().includes(searchTerm.toLowerCase()) ||
      v.vin.toLowerCase().includes(searchTerm.toLowerCase()) ||
      v.plate.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesStatus =
      selectedStatusFilter === 'ALL' || v.status === selectedStatusFilter;

    return matchesSearch && matchesStatus;
  });

  const handleOpenStatusModal = (vehicle) => {
    setStatusModalVehicle(vehicle);
    setNewStatus(vehicle.status);
    setStatusNotice(null);
  };

  const handleSaveStatusChange = (e) => {
    e.preventDefault();
    setVehicles((prev) =>
      prev.map((v) => (v.id === statusModalVehicle.id ? { ...v, status: newStatus } : v))
    );
    setStatusNotice({
      type: 'success',
      title: 'Status Updated Locally',
      message: `${statusModalVehicle.make} ${statusModalVehicle.model} status changed to ${newStatus}. (FleetService persistence active in Sprint 2).`,
    });
    setTimeout(() => {
      setStatusModalVehicle(null);
    }, 900);
  };

  const handleManagePhotos = (vehicle) => {
    if (onSelectVehicle) onSelectVehicle(vehicle);
    onNavigate('vehicle-images');
  };

  return (
    <div className="manage-fleet-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Fleet Inventory & Operations Management</h1>
          <p className="admin-page-subtitle">
            Track operational vehicle fleet statuses, manage photo media, and ingest new inventory units.
          </p>
        </div>

        <Button variant="primary" onClick={() => onNavigate('add-vehicle')}>
          + Ingest New Vehicle
        </Button>
      </div>

      {statusNotice && (
        <Alert
          type={statusNotice.type}
          title={statusNotice.title}
          message={statusNotice.message}
        />
      )}

      {/* Filter and Search Bar */}
      <div className="users-filter-bar">
        <div className="search-box-wrapper">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            placeholder="Search by VIN, license plate, make, or model..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="filter-search-input"
          />
        </div>

        <div className="role-filter-group">
          <label htmlFor="statusFilter" className="filter-label">Status Filter:</label>
          <select
            id="statusFilter"
            value={selectedStatusFilter}
            onChange={(e) => setSelectedStatusFilter(e.target.value)}
            className="filter-select-input"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt} value={opt}>
                {opt === 'ALL' ? 'All Statuses' : opt}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Fleet Inventory Table */}
      <div className="table-card">
        <div className="table-responsive-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Vehicle Details</th>
                <th>VIN & Plate</th>
                <th>Category</th>
                <th>Status</th>
                <th>Hub Location</th>
                <th>Odometer</th>
                <th style={{ textAlign: 'right' }}>Operational Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredVehicles.map((v) => (
                <tr key={v.id}>
                  <td>
                    <div className="user-cell-meta">
                      <div className="user-table-avatar" style={{ backgroundColor: '#0F172A' }}>
                        🚗
                      </div>
                      <div>
                        <strong className="user-name-text">{v.year} {v.make} {v.model}</strong>
                        <p className="user-email-sub">{v.fuel} • {v.transmission}</p>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className="font-mono" style={{ fontSize: '11px', display: 'block' }}>{v.vin}</span>
                    <span className="plate-pill">{v.plate}</span>
                  </td>
                  <td>
                    <span className="table-date">{v.category}</span>
                  </td>
                  <td>
                    <StatusBadge status={v.status} />
                  </td>
                  <td>
                    <span className="table-date">{v.hub}</span>
                  </td>
                  <td>
                    <span className="table-date font-mono">{v.mileage}</span>
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div className="table-action-btns">
                      <button
                        type="button"
                        className="btn-table-action"
                        onClick={() => handleManagePhotos(v)}
                        title="Manage Vehicle Images"
                      >
                        Photos ({v.images?.length || 0})
                      </button>
                      <button
                        type="button"
                        className="btn-table-action warning"
                        onClick={() => handleOpenStatusModal(v)}
                        title="Change Vehicle Operational Status"
                      >
                        Set Status
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Operational Status Update Modal */}
      <Modal
        isOpen={!!statusModalVehicle}
        onClose={() => setStatusModalVehicle(null)}
        title="Update Vehicle Operational Status"
        subtitle={`Update dispatch eligibility for ${statusModalVehicle?.make} ${statusModalVehicle?.model}.`}
      >
        <form onSubmit={handleSaveStatusChange}>
          <div className="form-group">
            <label htmlFor="vehicleStatus" className="form-label">
              Select Operational State <span className="required-indicator">*</span>
            </label>
            <div className="role-options-grid">
              {['AVAILABLE', 'IN_USE', 'MAINTENANCE', 'RETIRED'].map((statusKey) => (
                <label
                  key={statusKey}
                  className={`role-option-card ${newStatus === statusKey ? 'selected' : ''}`}
                >
                  <input
                    type="radio"
                    name="vehicleStatus"
                    value={statusKey}
                    checked={newStatus === statusKey}
                    onChange={(e) => setNewStatus(e.target.value)}
                    className="role-radio-native"
                  />
                  <div className="role-card-inner">
                    <StatusBadge status={statusKey} />
                  </div>
                </label>
              ))}
            </div>
          </div>

          <div className="modal-actions-row">
            <Button variant="outline" onClick={() => setStatusModalVehicle(null)}>
              Cancel
            </Button>
            <Button type="submit" variant="primary">
              Save Status
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default ManageFleetPage;
