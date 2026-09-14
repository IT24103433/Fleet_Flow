import React, { useState, useEffect, useCallback, useMemo } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getVehicles, getCategories, updateVehicleStatus } from '../../services/vehicleService';
import {
  getAvailableStatusTransitionsForRoles,
  canUserChangeStatus,
  validateStatusChange,
} from '../../validation/vehicleStatusValidation';

const STATUS_OPTIONS = [
  { value: 'ALL', label: 'All Statuses' },
  { value: 'Available', label: 'Available' },
  { value: 'InUse', label: 'In Use' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Retired', label: 'Retired' },
];

const FUEL_OPTIONS = [
  { value: 'ALL', label: 'All Powertrains' },
  { value: 'Gasoline', label: 'Gasoline' },
  { value: 'Diesel', label: 'Diesel' },
  { value: 'Electric', label: 'Electric' },
  { value: 'Hybrid', label: 'Hybrid' },
];

const TRANSMISSION_OPTIONS = [
  { value: 'ALL', label: 'All Transmissions' },
  { value: 'Automatic', label: 'Automatic' },
  { value: 'Manual', label: 'Manual' },
];

const HUB_OPTIONS = [
  { value: 'ALL', label: 'All Hubs' },
  { value: 'Metro Hub', label: 'Metro Hub' },
  { value: 'Logistics Depot', label: 'Logistics Depot' },
  { value: 'Airport Terminal 2', label: 'Airport Terminal 2' },
  { value: 'North Logistics Hub', label: 'North Logistics Hub' },
  { value: 'South Operations Depot', label: 'South Operations Depot' },
];

const SORT_OPTIONS = [
  { value: 'createdAt:desc', label: 'Newest Added' },
  { value: 'createdAt:asc', label: 'Oldest Added' },
  { value: 'make:asc', label: 'Make & Model (A–Z)' },
  { value: 'make:desc', label: 'Make & Model (Z–A)' },
  { value: 'dailyRate:asc', label: 'Daily Rate (Low to High)' },
  { value: 'dailyRate:desc', label: 'Daily Rate (High to Low)' },
  { value: 'year:desc', label: 'Model Year (Newest)' },
  { value: 'mileage:asc', label: 'Odometer (Lowest First)' },
];

const PAGE_SIZE_OPTIONS = [5, 10, 20, 50];

const ManageFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const { roles, token } = useAuth();
  const canAddVehicle = (roles || []).some((r) => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
  const canManageStatus = canUserChangeStatus(roles);

  // Status Change Modal States
  const [statusModalVehicle, setStatusModalVehicle] = useState(null);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);

  const [vehicles, setVehicles] = useState([]);
  const [categories, setCategories] = useState([]);

  // Search & Filter States
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedStatusFilter, setSelectedStatusFilter] = useState('ALL');
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState('ALL');
  const [selectedFuelFilter, setSelectedFuelFilter] = useState('ALL');
  const [selectedTransmissionFilter, setSelectedTransmissionFilter] = useState('ALL');
  const [selectedHubFilter, setSelectedHubFilter] = useState('ALL');

  // Sorting State
  const [selectedSort, setSelectedSort] = useState('createdAt:desc');

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pagination, setPagination] = useState({
    totalCount: 0,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  });

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

    const [sortBy, sortOrder] = selectedSort.split(':');

    const params = {
      page: currentPage,
      pageSize: pageSize,
      sortBy: sortBy || 'createdAt',
      sortOrder: sortOrder || 'desc',
    };

    if (selectedStatusFilter && selectedStatusFilter !== 'ALL') {
      params.status = selectedStatusFilter;
    }
    if (selectedCategoryFilter && selectedCategoryFilter !== 'ALL') {
      params.category = selectedCategoryFilter;
    }
    if (selectedFuelFilter && selectedFuelFilter !== 'ALL') {
      params.fuel = selectedFuelFilter;
    }
    if (selectedTransmissionFilter && selectedTransmissionFilter !== 'ALL') {
      params.transmission = selectedTransmissionFilter;
    }
    if (selectedHubFilter && selectedHubFilter !== 'ALL') {
      params.hub = selectedHubFilter;
    }
    if (searchTerm.trim()) {
      params.searchTerm = searchTerm.trim();
    }

    const result = await getVehicles(params);

    if (result.success) {
      setVehicles(result.data);
      if (result.pagination) {
        setPagination(result.pagination);
      } else {
        setPagination({
          totalCount: result.data.length,
          page: currentPage,
          pageSize: pageSize,
          totalPages: Math.ceil(result.data.length / pageSize) || 1,
          hasNextPage: false,
          hasPreviousPage: currentPage > 1,
        });
      }
    } else {
      setErrorMessage(result.message || 'Failed to retrieve vehicle inventory from FleetService.');
      setVehicles([]);
      setPagination((prev) => ({ ...prev, totalCount: 0, totalPages: 1 }));
    }
    setIsLoading(false);
  }, [
    currentPage,
    pageSize,
    selectedSort,
    selectedStatusFilter,
    selectedCategoryFilter,
    selectedFuelFilter,
    selectedTransmissionFilter,
    selectedHubFilter,
    searchTerm,
  ]);

  // Trigger search with debounce
  useEffect(() => {
    const handler = setTimeout(() => {
      fetchVehicles();
    }, 250);

    return () => clearTimeout(handler);
  }, [fetchVehicles]);

  // Reset to page 1 whenever search, filter, or sort changes
  const handleSearchChange = (e) => {
    setSearchTerm(e.target.value);
    setCurrentPage(1);
  };

  const handleStatusChange = (e) => {
    setSelectedStatusFilter(e.target.value);
    setCurrentPage(1);
  };

  const handleCategoryChange = (e) => {
    setSelectedCategoryFilter(e.target.value);
    setCurrentPage(1);
  };

  const handleFuelChange = (e) => {
    setSelectedFuelFilter(e.target.value);
    setCurrentPage(1);
  };

  const handleTransmissionChange = (e) => {
    setSelectedTransmissionFilter(e.target.value);
    setCurrentPage(1);
  };

  const handleHubChange = (e) => {
    setSelectedHubFilter(e.target.value);
    setCurrentPage(1);
  };

  const handleSortChange = (e) => {
    setSelectedSort(e.target.value);
    setCurrentPage(1);
  };

  const handlePageSizeChange = (e) => {
    const newSize = Number(e.target.value);
    setPageSize(newSize);
    setCurrentPage(1);
  };

  const handleInspectVehicle = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('staff-vehicle-details');
  };

  const handleEditVehicle = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('edit-vehicle');
  };

  const handleManagePhotos = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-images');
  };

  const allowedStatusOptions = useMemo(() => {
    return getAvailableStatusTransitionsForRoles(roles);
  }, [roles]);

  const selectedStatusDesc = useMemo(() => {
    return allowedStatusOptions.find((o) => o.value === targetStatus)?.description || '';
  }, [allowedStatusOptions, targetStatus]);

  const handleOpenStatusModal = (vehicle) => {
    setStatusModalVehicle(vehicle);
    const nextDefault = allowedStatusOptions.find((s) => s.value !== vehicle.status)?.value || allowedStatusOptions[0]?.value || '';
    setTargetStatus(nextDefault);
    setStatusModalError(null);
  };

  const handleCloseStatusModal = () => {
    if (isUpdatingStatus) return;
    setStatusModalVehicle(null);
    setTargetStatus('');
    setStatusModalError(null);
  };

  const handleConfirmStatusChange = async () => {
    if (!statusModalVehicle) return;

    const validation = validateStatusChange(targetStatus, statusModalVehicle.status, roles);
    if (!validation.isValid) {
      setStatusModalError(validation.error);
      return;
    }

    setIsUpdatingStatus(true);
    setStatusModalError(null);

    const result = await updateVehicleStatus(statusModalVehicle.id, targetStatus, token);

    setIsUpdatingStatus(false);

    if (result.success) {
      const updated = result.data;
      setVehicles((prev) =>
        prev.map((v) => (v.id === updated.id ? { ...v, status: updated.status, updatedAt: updated.updatedAt } : v))
      );
      setStatusSuccessMessage(`Vehicle ${statusModalVehicle.licensePlate || statusModalVehicle.vin} operational status successfully updated to "${targetStatus}".`);
      setStatusModalVehicle(null);
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setStatusModalError(result.message || 'Failed to update vehicle status.');
    }
  };

  const handleResetFilters = () => {
    setSearchTerm('');
    setSelectedStatusFilter('ALL');
    setSelectedCategoryFilter('ALL');
    setSelectedFuelFilter('ALL');
    setSelectedTransmissionFilter('ALL');
    setSelectedHubFilter('ALL');
    setSelectedSort('createdAt:desc');
    setCurrentPage(1);
  };

  // Active filters list for chips
  const activeFilters = useMemo(() => {
    const list = [];
    if (searchTerm.trim()) {
      list.push({ key: 'search', label: `Search: "${searchTerm.trim()}"`, onRemove: () => { setSearchTerm(''); setCurrentPage(1); } });
    }
    if (selectedStatusFilter !== 'ALL') {
      const match = STATUS_OPTIONS.find((s) => s.value === selectedStatusFilter);
      list.push({ key: 'status', label: `Status: ${match?.label || selectedStatusFilter}`, onRemove: () => { setSelectedStatusFilter('ALL'); setCurrentPage(1); } });
    }
    if (selectedCategoryFilter !== 'ALL') {
      list.push({ key: 'category', label: `Category: ${selectedCategoryFilter}`, onRemove: () => { setSelectedCategoryFilter('ALL'); setCurrentPage(1); } });
    }
    if (selectedFuelFilter !== 'ALL') {
      list.push({ key: 'fuel', label: `Fuel: ${selectedFuelFilter}`, onRemove: () => { setSelectedFuelFilter('ALL'); setCurrentPage(1); } });
    }
    if (selectedTransmissionFilter !== 'ALL') {
      list.push({ key: 'transmission', label: `Trans: ${selectedTransmissionFilter}`, onRemove: () => { setSelectedTransmissionFilter('ALL'); setCurrentPage(1); } });
    }
    if (selectedHubFilter !== 'ALL') {
      list.push({ key: 'hub', label: `Hub: ${selectedHubFilter}`, onRemove: () => { setSelectedHubFilter('ALL'); setCurrentPage(1); } });
    }
    return list;
  }, [searchTerm, selectedStatusFilter, selectedCategoryFilter, selectedFuelFilter, selectedTransmissionFilter, selectedHubFilter]);

  // Pagination navigation helpers
  const totalCount = pagination.totalCount || 0;
  const totalPages = pagination.totalPages || 1;
  const startItem = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(currentPage * pageSize, totalCount);

  // Generate visible page numbers
  const pageNumbers = useMemo(() => {
    const pages = [];
    const maxVisible = 5;
    let start = Math.max(1, currentPage - 2);
    let end = Math.min(totalPages, start + maxVisible - 1);

    if (end - start < maxVisible - 1) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }, [currentPage, totalPages]);

  return (
    <div className="manage-fleet-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Fleet Inventory & Operations Management</h1>
          <p className="admin-page-subtitle">
            Track operational vehicle fleet statuses, inspect unit specifications, search by VIN/plate, and manage inventory units.
          </p>
        </div>

        {canAddVehicle && (
          <Button variant="primary" onClick={() => onNavigate('add-vehicle')}>
            + Ingest New Vehicle
          </Button>
        )}
      </div>

      {statusSuccessMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert type="success" title="Status Updated" message={statusSuccessMessage} />
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="users-filter-bar" style={{ gap: '10px' }}>
        <div className="search-box-wrapper" style={{ minWidth: '260px' }}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon" aria-hidden="true">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            placeholder="Search VIN, license plate, make, or model..."
            value={searchTerm}
            onChange={handleSearchChange}
            className="filter-search-input"
            aria-label="Search fleet vehicles by VIN, plate, make, or model"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => { setSearchTerm(''); setCurrentPage(1); }}
              style={{
                position: 'absolute',
                right: '10px',
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                color: 'var(--color-text-muted)',
                cursor: 'pointer',
                fontSize: '14px',
                padding: '2px',
              }}
              aria-label="Clear search"
            >
              ✕
            </button>
          )}
        </div>

        {/* Status Filter */}
        <div className="role-filter-group">
          <label htmlFor="statusFilter" className="filter-label">Status:</label>
          <select
            id="statusFilter"
            value={selectedStatusFilter}
            onChange={handleStatusChange}
            className="filter-select-input"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Category Filter */}
        <div className="role-filter-group">
          <label htmlFor="categoryFilter" className="filter-label">Category:</label>
          <select
            id="categoryFilter"
            value={selectedCategoryFilter}
            onChange={handleCategoryChange}
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

        {/* Fuel Filter */}
        <div className="role-filter-group">
          <label htmlFor="fuelFilter" className="filter-label">Fuel:</label>
          <select
            id="fuelFilter"
            value={selectedFuelFilter}
            onChange={handleFuelChange}
            className="filter-select-input"
          >
            {FUEL_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Transmission Filter */}
        <div className="role-filter-group">
          <label htmlFor="transmissionFilter" className="filter-label">Trans:</label>
          <select
            id="transmissionFilter"
            value={selectedTransmissionFilter}
            onChange={handleTransmissionChange}
            className="filter-select-input"
          >
            {TRANSMISSION_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Hub Location Filter */}
        <div className="role-filter-group">
          <label htmlFor="hubFilter" className="filter-label">Hub:</label>
          <select
            id="hubFilter"
            value={selectedHubFilter}
            onChange={handleHubChange}
            className="filter-select-input"
          >
            {HUB_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Sorting Dropdown */}
        <div className="role-filter-group">
          <label htmlFor="sortSelect" className="filter-label">Sort:</label>
          <select
            id="sortSelect"
            value={selectedSort}
            onChange={handleSortChange}
            className="filter-select-input"
          >
            {SORT_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Active Filter Chips Strip */}
      {activeFilters.length > 0 && (
        <div className="active-filters-strip">
          <span className="active-filters-label">Active Filters:</span>
          {activeFilters.map((f) => (
            <span key={f.key} className="filter-chip">
              {f.label}
              <button
                type="button"
                className="filter-chip-remove"
                onClick={f.onRemove}
                title={`Remove ${f.label}`}
                aria-label={`Remove ${f.label}`}
              >
                ✕
              </button>
            </span>
          ))}
          <button
            type="button"
            className="clear-all-filters-btn"
            onClick={handleResetFilters}
          >
            Clear All Filters
          </button>
        </div>
      )}

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
                        {activeFilters.length > 0
                          ? 'No vehicles match your combined search, filter, and sorting criteria.'
                          : 'There are currently no vehicles registered in the fleet inventory.'}
                      </p>
                      {activeFilters.length > 0 && (
                        <Button variant="outline" size="sm" onClick={handleResetFilters}>
                          Reset All Filters
                        </Button>
                      )}
                    </td>
                  </tr>
                ) : (
                  vehicles.map((v) => {
                    const categoryName = v.categoryName || v.category || '—';
                    const fuelType = v.fuelType || v.fuel || '—';
                    const transmission = v.transmission || '—';
                    const licensePlate = v.licensePlate || v.plate || '—';
                    const hubLocation = v.hubLocation || v.hub || '—';
                    const mileage = typeof v.mileage === 'number' ? `${v.mileage.toLocaleString()} mi` : (v.mileage ? `${v.mileage} mi` : '—');
                    const dailyRate = typeof v.dailyRate === 'number' ? `$${v.dailyRate.toFixed(2)}/day` : (v.dailyRate ? `$${v.dailyRate}/day` : '—');

                    return (
                      <tr key={v.id}>
                        <td>
                          <div className="user-cell-meta">
                            <div className="user-table-avatar" style={{ backgroundColor: '#0F172A' }}>
                              🚗
                            </div>
                            <div>
                              <strong className="user-name-text">{v.year} {v.make} {v.model}</strong>
                              <p className="user-email-sub">{fuelType} • {transmission} • {dailyRate}</p>
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
                            {canManageStatus && (
                              <button
                                type="button"
                                className="btn-table-action"
                                onClick={() => handleOpenStatusModal(v)}
                                title="Change Operational Status"
                              >
                                Status
                              </button>
                            )}
                            {canAddVehicle && (
                              <>
                                <button
                                  type="button"
                                  className="btn-table-action"
                                  onClick={() => handleEditVehicle(v)}
                                  title="Edit Vehicle Details"
                                >
                                  Edit
                                </button>
                                <button
                                  type="button"
                                  className="btn-table-action"
                                  onClick={() => handleManagePhotos(v)}
                                  title="Manage Vehicle Images"
                                >
                                  Photos
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          {totalCount > 0 && (
            <div className="pagination-footer-card">
              <div className="pagination-summary">
                Showing <strong>{startItem}</strong> to <strong>{endItem}</strong> of <strong>{totalCount}</strong> vehicles
              </div>

              <div className="pagination-actions-cluster">
                <div className="page-size-selector-wrap">
                  <label htmlFor="pageSizeSelect">Show:</label>
                  <select
                    id="pageSizeSelect"
                    value={pageSize}
                    onChange={handlePageSizeChange}
                    className="page-size-select"
                  >
                    {PAGE_SIZE_OPTIONS.map((size) => (
                      <option key={size} value={size}>
                        {size} / page
                      </option>
                    ))}
                  </select>
                </div>

                <div className="pagination-nav-group">
                  <button
                    type="button"
                    className="pagination-page-btn"
                    disabled={currentPage <= 1}
                    onClick={() => setCurrentPage(1)}
                    title="First Page"
                    aria-label="First page"
                  >
                    «
                  </button>
                  <button
                    type="button"
                    className="pagination-page-btn"
                    disabled={!pagination.hasPreviousPage && currentPage <= 1}
                    onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                    title="Previous Page"
                    aria-label="Previous page"
                  >
                    ‹
                  </button>

                  {pageNumbers.map((p) => (
                    <button
                      key={p}
                      type="button"
                      className={`pagination-page-btn ${p === currentPage ? 'active' : ''}`}
                      onClick={() => setCurrentPage(p)}
                      aria-current={p === currentPage ? 'page' : undefined}
                    >
                      {p}
                    </button>
                  ))}

                  <button
                    type="button"
                    className="pagination-page-btn"
                    disabled={!pagination.hasNextPage && currentPage >= totalPages}
                    onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                    title="Next Page"
                    aria-label="Next page"
                  >
                    ›
                  </button>
                  <button
                    type="button"
                    className="pagination-page-btn"
                    disabled={currentPage >= totalPages}
                    onClick={() => setCurrentPage(totalPages)}
                    title="Last Page"
                    aria-label="Last page"
                  >
                    »
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Vehicle Operational Status Confirmation Modal */}
      <Modal
        isOpen={!!statusModalVehicle}
        onClose={handleCloseStatusModal}
        title="Update Operational Status"
        subtitle={`Modify operational availability for ${statusModalVehicle?.year} ${statusModalVehicle?.make} ${statusModalVehicle?.model}`}
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
                {statusModalVehicle?.year} {statusModalVehicle?.make} {statusModalVehicle?.model}
              </strong>
              <span style={{ fontSize: '11px', color: 'var(--color-text-muted, #94a3b8)' }}>
                Plate: {statusModalVehicle?.licensePlate} • VIN: {statusModalVehicle?.vin}
              </span>
            </div>
            <div>
              <StatusBadge status={statusModalVehicle?.status} />
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <label
              htmlFor="targetStatusSelect"
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
              id="targetStatusSelect"
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
                  {opt.label} {opt.value === statusModalVehicle?.status ? '(Current)' : ''}
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

          {targetStatus === 'Maintenance' && statusModalVehicle?.status !== 'Maintenance' && (
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
            disabled={isUpdatingStatus || targetStatus === statusModalVehicle?.status}
            isLoading={isUpdatingStatus}
          >
            Confirm Status Change
          </Button>
        </div>
      </Modal>
    </div>
  );
};

export default ManageFleetPage;
