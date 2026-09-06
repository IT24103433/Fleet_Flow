import React, { useState, useEffect, useCallback } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import { getVehicles, getCategories } from '../../services/vehicleService';

const STATUS_OPTIONS = [
  { value: 'ALL', label: 'All Statuses' },
  { value: 'Available', label: 'Available' },
  { value: 'InUse', label: 'In Use' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Retired', label: 'Retired' },
];

const ManageFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const [vehicles, setVehicles] = useState([]);
  const [categories, setCategories] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedStatusFilter, setSelectedStatusFilter] = useState('ALL');
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState('ALL');

  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);

  // Fetch Categories on Mount
  useEffect(() => {
    let isMounted = true;
    getCategories().then((result) => {
      if (isMounted && result.success && Array.isArray(result.data)) {
        setCategories(result.data);
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  // Fetch Vehicles from backend API
  const fetchVehicles = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);

    const params = {};
    if (selectedStatusFilter && selectedStatusFilter !== 'ALL') {
      params.status = selectedStatusFilter;
    }
    if (selectedCategoryFilter && selectedCategoryFilter !== 'ALL') {
      params.category = selectedCategoryFilter;
    }
    if (searchTerm.trim()) {
      params.searchTerm = searchTerm.trim();
    }

    const result = await getVehicles(params);

    if (result.success) {
      setVehicles(result.data);
    } else {
      setErrorMessage(result.message || 'Failed to retrieve vehicle inventory from FleetService.');
      setVehicles([]);
    }
    setIsLoading(false);
  }, [selectedStatusFilter, selectedCategoryFilter, searchTerm]);

  // Trigger search with debounce
  useEffect(() => {
    const handler = setTimeout(() => {
      fetchVehicles();
    }, 250);

    return () => clearTimeout(handler);
  }, [fetchVehicles]);

  const handleInspectVehicle = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-details');
  };

  const handleManagePhotos = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-images');
  };

  const handleResetFilters = () => {
    setSearchTerm('');
    setSelectedStatusFilter('ALL');
    setSelectedCategoryFilter('ALL');
  };

  return (
    <div className="manage-fleet-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Fleet Inventory & Operations Management</h1>
          <p className="admin-page-subtitle">
            Track operational vehicle fleet statuses, inspect unit specifications, and ingest new inventory units.
          </p>
        </div>

        <Button variant="primary" onClick={() => onNavigate('add-vehicle')}>
          + Ingest New Vehicle
        </Button>
      </div>

      {/* Filter and Search Bar */}
      <div className="users-filter-bar">
        <div className="search-box-wrapper">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon" aria-hidden="true">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            placeholder="Search by VIN, license plate, make, or model..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="filter-search-input"
            aria-label="Filter fleet vehicles"
          />
        </div>

        <div className="role-filter-group">
          <label htmlFor="statusFilter" className="filter-label">Status:</label>
          <select
            id="statusFilter"
            value={selectedStatusFilter}
            onChange={(e) => setSelectedStatusFilter(e.target.value)}
            className="filter-select-input"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        <div className="role-filter-group">
          <label htmlFor="categoryFilter" className="filter-label">Category:</label>
          <select
            id="categoryFilter"
            value={selectedCategoryFilter}
            onChange={(e) => setSelectedCategoryFilter(e.target.value)}
            className="filter-select-input"
          >
            <option value="ALL">All Categories</option>
            {categories.map((cat) => (
              <option key={cat.id || cat.name} value={cat.name}>
                {cat.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Error Alert */}
      {errorMessage && (
        <div style={{ marginBottom: 'var(--space-6)' }}>
          <Alert
            type="error"
            title="Fleet Inventory Service Error"
            message={errorMessage}
          />
          <div style={{ marginTop: 'var(--space-3)', display: 'flex', justifyContent: 'flex-end' }}>
            <Button variant="outline" size="sm" onClick={fetchVehicles}>
              ↻ Retry Loading Fleet
            </Button>
          </div>
        </div>
      )}

      {/* Loading State */}
      {isLoading && (
        <div style={{ textAlign: 'center', padding: 'var(--space-12) 0', color: 'var(--color-text-secondary)' }}>
          <div className="spinner" style={{ width: '32px', height: '32px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
          <p>Querying fleet inventory records from FleetService...</p>
        </div>
      )}

      {/* Fleet Inventory Table / Empty State */}
      {!isLoading && !errorMessage && (
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
                {vehicles.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ textAlign: 'center', padding: 'var(--space-8)' }}>
                      <div style={{ color: 'var(--color-text-muted)', marginBottom: '8px' }}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="40" height="40" aria-hidden="true" style={{ margin: '0 auto' }}>
                          <circle cx="11" cy="11" r="8" />
                          <line x1="21" y1="21" x2="16.65" y2="16.65" />
                        </svg>
                      </div>
                      <strong style={{ display: 'block', fontSize: '15px', color: 'var(--color-text-primary)', marginBottom: '4px' }}>
                        No Fleet Units Found
                      </strong>
                      <p style={{ color: 'var(--color-text-secondary)', fontSize: '13px', marginBottom: '12px' }}>
                        No registered vehicles match your current search and filter settings.
                      </p>
                      <Button variant="outline" size="sm" onClick={handleResetFilters}>
                        Reset Filters
                      </Button>
                    </td>
                  </tr>
                ) : (
                  vehicles.map((v) => {
                    const categoryName = v.categoryName || v.category || 'Standard';
                    const fuelType = v.fuelType || v.fuel || 'Hybrid';
                    const transmission = v.transmission || 'Automatic';
                    const licensePlate = v.licensePlate || v.plate || 'N/A';
                    const hubLocation = v.hubLocation || v.hub || 'Metro Hub';
                    const mileage = typeof v.mileage === 'number' ? `${v.mileage.toLocaleString()} mi` : (v.mileage || '0 mi');

                    return (
                      <tr key={v.id}>
                        <td>
                          <div className="user-cell-meta">
                            <div className="user-table-avatar" style={{ backgroundColor: '#0F172A' }}>
                              🚗
                            </div>
                            <div>
                              <strong className="user-name-text">{v.year} {v.make} {v.model}</strong>
                              <p className="user-email-sub">{fuelType} • {transmission}</p>
                            </div>
                          </div>
                        </td>
                        <td>
                          <span className="font-mono" style={{ fontSize: '11px', display: 'block' }}>{v.vin}</span>
                          <span className="plate-pill">{licensePlate}</span>
                        </td>
                        <td>
                          <span className="table-date">{categoryName}</span>
                        </td>
                        <td>
                          <StatusBadge status={v.status} />
                        </td>
                        <td>
                          <span className="table-date">{hubLocation}</span>
                        </td>
                        <td>
                          <span className="table-date font-mono">{mileage}</span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <div className="table-action-btns">
                            <button
                              type="button"
                              className="btn-table-action"
                              onClick={() => handleInspectVehicle(v)}
                              title="Inspect Vehicle Specifications"
                            >
                              Details
                            </button>
                            <button
                              type="button"
                              className="btn-table-action"
                              onClick={() => handleManagePhotos(v)}
                              title="Manage Vehicle Images"
                            >
                              Photos
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};

export default ManageFleetPage;
