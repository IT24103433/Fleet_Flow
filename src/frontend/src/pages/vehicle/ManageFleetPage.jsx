import { WarningCircleIcon } from '@phosphor-icons/react/dist/csr/WarningCircle';
import { CaretDoubleLeftIcon } from '@phosphor-icons/react/dist/csr/CaretDoubleLeft';
import { CaretDoubleRightIcon } from '@phosphor-icons/react/dist/csr/CaretDoubleRight';
import { CaretLeftIcon } from '@phosphor-icons/react/dist/csr/CaretLeft';
import { CaretRightIcon } from '@phosphor-icons/react/dist/csr/CaretRight';
import { PlusIcon } from '@phosphor-icons/react/dist/csr/Plus';
import { XIcon } from '@phosphor-icons/react/dist/csr/X';
import { CarProfileIcon } from "@phosphor-icons/react/dist/csr/CarProfile";
import { MagnifyingGlassIcon } from "@phosphor-icons/react/dist/csr/MagnifyingGlass";
import { formatDailyRate } from "../../utils/currencyUtils";
import useConfirmAction from "../../components/common/useConfirmAction";
import VehicleThumbnail from '../../components/common/VehicleThumbnail';
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { useAuth } from '../../context/AuthContext';
import { getVehicles, getCategories, updateVehicleStatus, retireVehicle, reactivateVehicle } from '../../services/vehicleService';
import { getAvailableStatusTransitionsForRoles, canUserChangeStatus, validateStatusChange, canUserRetireVehicle, validateRetirement } from '../../validation/vehicleStatusValidation';
const STATUS_OPTIONS = [{
  value: 'ALL',
  label: 'All Active Inventory'
}, {
  value: 'Available',
  label: 'Available'
}, {
  value: 'InUse',
  label: 'In Use'
}, {
  value: 'Maintenance',
  label: 'Maintenance'
}, {
  value: 'Retired',
  label: 'Retired (Retired)'
}, {
  value: 'ALL_INC_RETIRED',
  label: 'All Inventory (Inc. Retired)'
}];
const FUEL_OPTIONS = [{
  value: 'ALL',
  label: 'All Fuel types'
}, {
  value: 'Gasoline',
  label: 'Gasoline'
}, {
  value: 'Diesel',
  label: 'Diesel'
}, {
  value: 'Electric',
  label: 'Electric'
}, {
  value: 'Hybrid',
  label: 'Hybrid'
}];
const TRANSMISSION_OPTIONS = [{
  value: 'ALL',
  label: 'All Transmissions'
}, {
  value: 'Automatic',
  label: 'Automatic'
}, {
  value: 'Manual',
  label: 'Manual'
}];
const HUB_OPTIONS = [{
  value: 'ALL',
  label: 'All Hubs'
}, {
  value: 'Metro Hub',
  label: 'Metro Hub'
}, {
  value: 'Logistics Depot',
  label: 'Logistics Depot'
}, {
  value: 'Airport Terminal 2',
  label: 'Airport Terminal 2'
}, {
  value: 'North Logistics Hub',
  label: 'North Logistics Hub'
}, {
  value: 'South Operations Depot',
  label: 'South Operations Depot'
}];
const SORT_OPTIONS = [{
  value: 'createdAt:desc',
  label: 'Newest Added'
}, {
  value: 'createdAt:asc',
  label: 'Oldest Added'
}, {
  value: 'make:asc',
  label: 'Make & Model (A–Z)'
}, {
  value: 'make:desc',
  label: 'Make & Model (Z–A)'
}, {
  value: 'dailyRate:asc',
  label: 'Daily Rate (Low to High)'
}, {
  value: 'dailyRate:desc',
  label: 'Daily Rate (High to Low)'
}, {
  value: 'year:desc',
  label: 'Model Year (Newest)'
}, {
  value: 'mileage:asc',
  label: 'Odometer (Lowest First)'
}];
const PAGE_SIZE_OPTIONS = [5, 10, 20, 50];
const ManageFleetPage = ({
  onNavigate,
  onSelectVehicle
}) => {
  const {
    requestConfirmation,
    confirmationDialog
  } = useConfirmAction();
  const {
    roles,
    token
  } = useAuth();
  const canAddVehicle = (roles || []).some(r => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
  const canManageStatus = canUserChangeStatus(roles);
  const canRetireVehicle = canUserRetireVehicle(roles);

  // Status Change Modal States
  const [statusModalVehicle, setStatusModalVehicle] = useState(null);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);

  // Retirement Deactivation Modal States
  const [retireModalVehicle, setRetireModalVehicle] = useState(null);
  const [retireReason, setRetireReason] = useState('');
  const [isRetiring, setIsRetiring] = useState(false);
  const [retireModalError, setRetireModalError] = useState(null);
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
    hasPreviousPage: false
  });
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);

  // Fetch Categories on Mount
  useEffect(() => {
    let isMounted = true;
    getCategories().then(result => {
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
      sortOrder: sortOrder || 'desc'
    };
    if (selectedStatusFilter === 'ALL_INC_RETIRED') {
      params.includeRetired = true;
    } else if (selectedStatusFilter && selectedStatusFilter !== 'ALL') {
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
          hasPreviousPage: currentPage > 1
        });
      }
    } else {
      setErrorMessage(result.message || 'Vehicle inventory could not be loaded. Please try again.');
      setVehicles([]);
      setPagination(prev => ({
        ...prev,
        totalCount: 0,
        totalPages: 1
      }));
    }
    setIsLoading(false);
  }, [currentPage, pageSize, selectedSort, selectedStatusFilter, selectedCategoryFilter, selectedFuelFilter, selectedTransmissionFilter, selectedHubFilter, searchTerm]);

  // Trigger search with debounce
  useEffect(() => {
    const handler = setTimeout(() => {
      fetchVehicles();
    }, 250);
    return () => clearTimeout(handler);
  }, [fetchVehicles]);

  // Reset to page 1 whenever search, filter, or sort changes
  const handleSearchChange = e => {
    setSearchTerm(e.target.value);
    setCurrentPage(1);
  };
  const handleStatusChange = e => {
    setSelectedStatusFilter(e.target.value);
    setCurrentPage(1);
  };
  const handleCategoryChange = e => {
    setSelectedCategoryFilter(e.target.value);
    setCurrentPage(1);
  };
  const handleFuelChange = e => {
    setSelectedFuelFilter(e.target.value);
    setCurrentPage(1);
  };
  const handleTransmissionChange = e => {
    setSelectedTransmissionFilter(e.target.value);
    setCurrentPage(1);
  };
  const handleHubChange = e => {
    setSelectedHubFilter(e.target.value);
    setCurrentPage(1);
  };
  const handleSortChange = e => {
    setSelectedSort(e.target.value);
    setCurrentPage(1);
  };
  const handlePageSizeChange = e => {
    const newSize = Number(e.target.value);
    setPageSize(newSize);
    setCurrentPage(1);
  };
  const handleInspectVehicle = vehicle => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('staff-vehicle-details');
  };
  const handleEditVehicle = vehicle => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('edit-vehicle');
  };
  const handleManagePhotos = vehicle => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-images');
  };
  const allowedStatusOptions = useMemo(() => {
    return getAvailableStatusTransitionsForRoles(roles);
  }, [roles]);
  const selectedStatusDesc = useMemo(() => {
    return allowedStatusOptions.find(o => o.value === targetStatus)?.description || '';
  }, [allowedStatusOptions, targetStatus]);
  const handleOpenStatusModal = vehicle => {
    setStatusModalVehicle(vehicle);
    const nextDefault = allowedStatusOptions.find(s => s.value !== vehicle.status)?.value || allowedStatusOptions[0]?.value || '';
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
      setVehicles(prev => prev.map(v => v.id === updated.id ? {
        ...v,
        status: updated.status,
        updatedAt: updated.updatedAt
      } : v));
      setStatusSuccessMessage(`Vehicle ${statusModalVehicle.licensePlate || statusModalVehicle.vin} operational status successfully updated to "${targetStatus}".`);
      setStatusModalVehicle(null);
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setStatusModalError(result.message || 'Failed to update vehicle status.');
    }
  };
  const handleOpenRetireModal = vehicle => {
    setRetireModalVehicle(vehicle);
    setRetireReason('');
    setRetireModalError(null);
  };
  const handleCloseRetireModal = () => {
    if (isRetiring) return;
    setRetireModalVehicle(null);
    setRetireReason('');
    setRetireModalError(null);
  };
  const handleConfirmRetire = async () => {
    if (!retireModalVehicle) return;
    const validation = validateRetirement(retireModalVehicle, roles);
    if (!validation.isValid) {
      setRetireModalError(validation.error);
      return;
    }
    setIsRetiring(true);
    setRetireModalError(null);
    const result = await retireVehicle(retireModalVehicle.id, retireReason, token);
    setIsRetiring(false);
    if (result.success) {
      const updated = result.data;
      setVehicles(prev => prev.map(v => v.id === updated.id ? {
        ...v,
        status: 'Retired',
        updatedAt: updated.updatedAt
      } : v));
      setStatusSuccessMessage(`Vehicle ${retireModalVehicle.licensePlate || retireModalVehicle.vin} (${retireModalVehicle.year} ${retireModalVehicle.make} ${retireModalVehicle.model}) successfully retired. Unit is decommissioned from active fleet views while vehicle specifications and maintenance history remain preserved.`);
      setRetireModalVehicle(null);
      if (selectedStatusFilter === 'ALL') {
        fetchVehicles();
      }
      setTimeout(() => {
        setStatusSuccessMessage(null);
      }, 6000);
    } else {
      setRetireModalError(result.message || 'Failed to retire vehicle.');
    }
  };
  const handleReactivateVehicle = async vehicle => {
    if (!vehicle || !canRetireVehicle) return;
    requestConfirmation({
      title: "Return this vehicle to the fleet?",
      message: "This vehicle will become available for rental bookings.",
      action: async () => {
        const result = await reactivateVehicle(vehicle.id, token);
        if (result.success) {
          const updated = result.data;
          setVehicles(prev => prev.map(v => v.id === updated.id ? {
            ...v,
            status: 'Available',
            updatedAt: updated.updatedAt
          } : v));
          setStatusSuccessMessage(`Vehicle ${vehicle.licensePlate || vehicle.vin} has been reactivated and returned to active fleet inventory.`);
          setTimeout(() => {
            setStatusSuccessMessage(null);
          }, 6000);
        } else {
          setErrorMessage(result.message || 'Failed to reactivate vehicle.');
        }
      },
      label: "Confirm change",
      variant: "primary"
    });
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
      list.push({
        key: 'search',
        label: `Search: "${searchTerm.trim()}"`,
        onRemove: () => {
          setSearchTerm('');
          setCurrentPage(1);
        }
      });
    }
    if (selectedStatusFilter !== 'ALL') {
      const match = STATUS_OPTIONS.find(s => s.value === selectedStatusFilter);
      list.push({
        key: 'status',
        label: `Status: ${match?.label || selectedStatusFilter}`,
        onRemove: () => {
          setSelectedStatusFilter('ALL');
          setCurrentPage(1);
        }
      });
    }
    if (selectedCategoryFilter !== 'ALL') {
      list.push({
        key: 'category',
        label: `Category: ${selectedCategoryFilter}`,
        onRemove: () => {
          setSelectedCategoryFilter('ALL');
          setCurrentPage(1);
        }
      });
    }
    if (selectedFuelFilter !== 'ALL') {
      list.push({
        key: 'fuel',
        label: `Fuel: ${selectedFuelFilter}`,
        onRemove: () => {
          setSelectedFuelFilter('ALL');
          setCurrentPage(1);
        }
      });
    }
    if (selectedTransmissionFilter !== 'ALL') {
      list.push({
        key: 'transmission',
        label: `Trans: ${selectedTransmissionFilter}`,
        onRemove: () => {
          setSelectedTransmissionFilter('ALL');
          setCurrentPage(1);
        }
      });
    }
    if (selectedHubFilter !== 'ALL') {
      list.push({
        key: 'hub',
        label: `Hub: ${selectedHubFilter}`,
        onRemove: () => {
          setSelectedHubFilter('ALL');
          setCurrentPage(1);
        }
      });
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
  return <div className="manage-fleet-container">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Vehicle inventory</h1>
          <p className="admin-page-subtitle">
            Search the fleet, review vehicle details, and keep pricing and status up to date.
          </p>
        </div>

        {canAddVehicle && <Button variant="primary" onClick={() => onNavigate('add-vehicle')}>
            <PlusIcon size={18} weight="regular" aria-hidden="true" /> Add vehicle
          </Button>}
      </div>

      {statusSuccessMessage && <div className="view-4933c088">
          <Alert type="success" title="Status Updated" message={statusSuccessMessage} />
        </div>}

      {/* Filter and Search Bar */}
      <div className="users-filter-bar view-83af1819">
        <div className="search-box-wrapper view-7974bf52">
          <MagnifyingGlassIcon className="search-icon" weight="regular" aria-hidden="true" />
          <input type="text" placeholder="Search VIN, license plate, make, or model..." value={searchTerm} onChange={handleSearchChange} className="filter-search-input" aria-label="Search fleet vehicles by VIN, plate, make, or model" />
          {searchTerm && <button type="button" onClick={() => {
          setSearchTerm('');
          setCurrentPage(1);
        }} aria-label="Clear search" className="view-477ca243">
              <XIcon size={18} weight="regular" aria-hidden="true" />
            </button>}
        </div>

        {/* Status Filter */}
        <div className="role-filter-group">
          <label htmlFor="statusFilter" className="filter-label">Status:</label>
          <select id="statusFilter" value={selectedStatusFilter} onChange={handleStatusChange} className="filter-select-input">
            {STATUS_OPTIONS.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>
        </div>

        {/* Category Filter */}
        <div className="role-filter-group">
          <label htmlFor="categoryFilter" className="filter-label">Category:</label>
          <select id="categoryFilter" value={selectedCategoryFilter} onChange={handleCategoryChange} className="filter-select-input">
            <option value="ALL">All Categories</option>
            {categories.map(cat => <option key={cat.id || cat.name} value={cat.name}>
                {cat.name}
              </option>)}
          </select>
        </div>

        {/* Fuel Filter */}
        <div className="role-filter-group">
          <label htmlFor="fuelFilter" className="filter-label">Fuel:</label>
          <select id="fuelFilter" value={selectedFuelFilter} onChange={handleFuelChange} className="filter-select-input">
            {FUEL_OPTIONS.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>
        </div>

        {/* Transmission Filter */}
        <div className="role-filter-group">
          <label htmlFor="transmissionFilter" className="filter-label">Trans:</label>
          <select id="transmissionFilter" value={selectedTransmissionFilter} onChange={handleTransmissionChange} className="filter-select-input">
            {TRANSMISSION_OPTIONS.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>
        </div>

        {/* Hub Location Filter */}
        <div className="role-filter-group">
          <label htmlFor="hubFilter" className="filter-label">Hub:</label>
          <select id="hubFilter" value={selectedHubFilter} onChange={handleHubChange} className="filter-select-input">
            {HUB_OPTIONS.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>
        </div>

        {/* Sorting Dropdown */}
        <div className="role-filter-group">
          <label htmlFor="sortSelect" className="filter-label">Sort:</label>
          <select id="sortSelect" value={selectedSort} onChange={handleSortChange} className="filter-select-input">
            {SORT_OPTIONS.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>
        </div>
      </div>

      {/* Active Filter Chips Strip */}
      {activeFilters.length > 0 && <div className="active-filters-strip">
          <span className="active-filters-label">Active Filters:</span>
          {activeFilters.map(f => <span key={f.key} className="filter-chip">
              {f.label}
              <button type="button" className="filter-chip-remove" onClick={f.onRemove} title={`Remove ${f.label}`} aria-label={`Remove ${f.label}`}>
                <XIcon size={18} weight="regular" aria-hidden="true" />
              </button>
            </span>)}
          <button type="button" className="clear-all-filters-btn" onClick={handleResetFilters}>
            Clear All Filters
          </button>
        </div>}

      {/* Error Alert */}
      {errorMessage && <div className="view-7257e5f1">
          <Alert type="error" title="Fleet Inventory Service Error" message={errorMessage} />
          <div className="view-34336c9e">
            <Button variant="outline" size="sm" onClick={fetchVehicles}>
              ↻ Retry Loading Fleet
            </Button>
          </div>
        </div>}

      {/* Loading State */}
      {isLoading && <div className="view-69fb17b4">
          <div className="spinner view-7cfb17c1" />
          <p>Loading vehicle inventory…</p>
        </div>}

      {/* Fleet Inventory Table / Empty State */}
      {!isLoading && !errorMessage && <div className="table-card">
          <div className="table-responsive-wrapper">
            <table className="admin-table responsive-record-table" role="table">
              <thead>
                <tr role="row">
                  <th role="columnheader">Vehicle Details</th>
                  <th role="columnheader">VIN & Plate</th>
                  <th role="columnheader">Category</th>
                  <th role="columnheader">Status</th>
                  <th role="columnheader">Hub Location</th>
                  <th role="columnheader">Odometer</th>
                  <th className="view-37dd0c2a" role="columnheader">Operational Actions</th>
                </tr>
              </thead>
              <tbody>
                {vehicles.length === 0 ? <tr role="row">
                    <td colSpan={7} className="view-14ca39d7" role="cell">
                      <div className="view-7ed2a071">
                        <CarProfileIcon width="40" height="40" className="view-6002529c" weight="regular" aria-hidden="true" />
                      </div>
                      <strong className="view-7edc5dd5">
                        No Fleet Units Found
                      </strong>
                      <p className="view-3dcdf628">
                        {activeFilters.length > 0 ? 'No vehicles match your combined search, filter, and sorting criteria.' : 'There are currently no vehicles registered in the fleet inventory.'}
                      </p>
                      {activeFilters.length > 0 && <Button variant="outline" size="sm" onClick={handleResetFilters}>
                          Reset All Filters
                        </Button>}
                    </td>
                  </tr> : vehicles.map(v => {
              const categoryName = v.categoryName || v.category || '—';
              const fuelType = v.fuelType || v.fuel || '—';
              const transmission = v.transmission || '—';
              const licensePlate = v.licensePlate || v.plate || '—';
              const hubLocation = v.hubLocation || v.hub || '—';
              const mileage = typeof v.mileage === 'number' ? `${v.mileage.toLocaleString()} mi` : v.mileage ? `${v.mileage} mi` : '—';
              const dailyRate = formatDailyRate(v.dailyRate);
              const isRetired = v.status === 'Retired';
              return <tr key={v.id} className={isRetired ? 'vehicle-row-retired' : ''} role="row">
                        <td data-label="Vehicle" role="cell">
                          <div className="user-cell-meta">
                            <VehicleThumbnail vehicle={v} token={token} />
                            <div>
                              <strong className="user-name-text">
                                {v.year} {v.make} {v.model}
                                {isRetired && <span className="decommissioned-inline-pill">Retired</span>}
                              </strong>
                              <p className="user-email-sub">{fuelType} • {transmission} • {dailyRate}</p>
                            </div>
                          </div>
                        </td>
                        <td data-label="VIN & plate" role="cell">
                          <span className="font-mono view-3dcfcac1">{v.vin}</span>
                          <span className="plate-pill">{licensePlate}</span>
                        </td>
                        <td data-label="Category" role="cell">
                          <span className="table-date">{categoryName}</span>
                        </td>
                        <td data-label="Status" role="cell">
                          <div className="view-1bdedad3">
                            <StatusBadge status={v.status} />
                            {isRetired && <span className="retired-preserved-note">History Preserved</span>}
                          </div>
                        </td>
                        <td data-label="Pickup location" role="cell">
                          <span className="table-date">{hubLocation}</span>
                        </td>
                        <td data-label="Odometer" role="cell">
                          <span className="table-date font-mono">{mileage}</span>
                        </td>
                        <td className="view-37dd0c2a" data-label="Actions" role="cell">
                          <div className="table-action-btns">
                            <button type="button" className="btn-table-action" onClick={() => handleInspectVehicle(v)} title="Inspect Vehicle Specifications">
                              Details
                            </button>
                            {canManageStatus && <button type="button" className="btn-table-action" onClick={() => handleOpenStatusModal(v)} title="Change Operational Status">
                                Status
                              </button>}
                            {canRetireVehicle && (isRetired ? <button type="button" className="btn-table-action btn-table-action-reactivate" onClick={() => handleReactivateVehicle(v)} title="Reactivate Vehicle back to Active Fleet">
                                  Reactivate
                                </button> : <button type="button" className="btn-table-action btn-table-action-retire" onClick={() => handleOpenRetireModal(v)} title={v.status === 'InUse' ? 'Cannot retire a vehicle in active customer use' : 'Retire or Decommission Vehicle'} disabled={v.status === 'InUse'}>
                                  Retire
                                </button>)}
                            {canAddVehicle && <>
                                <button type="button" className="btn-table-action" onClick={() => handleEditVehicle(v)} title="Edit Vehicle Details">
                                  Edit
                                </button>
                                <button type="button" className="btn-table-action" onClick={() => handleManagePhotos(v)} title="Manage Vehicle Images">
                                  Photos
                                </button>
                              </>}
                          </div>
                        </td>
                      </tr>;
            })}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          {totalCount > 0 && <div className="pagination-footer-card">
              <div className="pagination-summary">
                Showing <strong>{startItem}</strong> to <strong>{endItem}</strong> of <strong>{totalCount}</strong> vehicles
              </div>

              <div className="pagination-actions-cluster">
                <div className="page-size-selector-wrap">
                  <label htmlFor="pageSizeSelect">Show:</label>
                  <select id="pageSizeSelect" value={pageSize} onChange={handlePageSizeChange} className="page-size-select">
                    {PAGE_SIZE_OPTIONS.map(size => <option key={size} value={size}>
                        {size} / page
                      </option>)}
                  </select>
                </div>

                <div className="pagination-nav-group">
                  <button type="button" className="pagination-page-btn" disabled={currentPage <= 1} onClick={() => setCurrentPage(1)} title="First Page" aria-label="First page">
                    <CaretDoubleLeftIcon size={16} weight="regular" aria-hidden="true" />
                  </button>
                  <button type="button" className="pagination-page-btn" disabled={!pagination.hasPreviousPage && currentPage <= 1} onClick={() => setCurrentPage(p => Math.max(1, p - 1))} title="Previous Page" aria-label="Previous page">
                    <CaretLeftIcon size={16} weight="regular" aria-hidden="true" />
                  </button>

                  {pageNumbers.map(p => <button key={p} type="button" className={`pagination-page-btn ${p === currentPage ? 'active' : ''}`} onClick={() => setCurrentPage(p)} aria-current={p === currentPage ? 'page' : undefined}>
                      {p}
                    </button>)}

                  <button type="button" className="pagination-page-btn" disabled={!pagination.hasNextPage && currentPage >= totalPages} onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))} title="Next Page" aria-label="Next page">
                    <CaretRightIcon size={16} weight="regular" aria-hidden="true" />
                  </button>
                  <button type="button" className="pagination-page-btn" disabled={currentPage >= totalPages} onClick={() => setCurrentPage(totalPages)} title="Last Page" aria-label="Last page">
                    <CaretDoubleRightIcon size={16} weight="regular" aria-hidden="true" />
                  </button>
                </div>
              </div>
            </div>}
        </div>}

      {/* Vehicle Operational Status Confirmation Modal */}
      <Modal isOpen={!!statusModalVehicle} onClose={handleCloseStatusModal} title="Update Operational Status" subtitle={`Modify operational availability for ${statusModalVehicle?.year} ${statusModalVehicle?.make} ${statusModalVehicle?.model}`}>
        {statusModalError && <div className="view-4933c088">
            <Alert type="error" title="Status Update Error" message={statusModalError} />
          </div>}

        <div className="view-b1d83fd0">
          <div className="view-836939bd">
            <div>
              <strong className="view-ab7198dc">
                {statusModalVehicle?.year} {statusModalVehicle?.make} {statusModalVehicle?.model}
              </strong>
              <span className="view-409503d5">
                Plate: {statusModalVehicle?.licensePlate} • VIN: {statusModalVehicle?.vin}
              </span>
            </div>
            <div>
              <StatusBadge status={statusModalVehicle?.status} />
            </div>
          </div>

          <div className="view-4933c088">
            <label htmlFor="targetStatusSelect" className="view-810b6506">
              Select New Operational Status:
            </label>
            <select id="targetStatusSelect" value={targetStatus} onChange={e => {
            setTargetStatus(e.target.value);
            setStatusModalError(null);
          }} disabled={isUpdatingStatus} className="filter-select-input view-9b20feff">
              {allowedStatusOptions.map(opt => <option key={opt.value} value={opt.value}>
                  {opt.label} {opt.value === statusModalVehicle?.status ? '(Current)' : ''}
                </option>)}
            </select>
            {selectedStatusDesc && <p className="view-68b8cce9">
                {selectedStatusDesc}
              </p>}
          </div>

          {targetStatus === 'Retired' && <div className="view-ba27b3e9">
              <strong>Warning:</strong> Retiring a vehicle will permanently decommission this unit and remove it from commercial rental availability.
            </div>}

          {targetStatus === 'Maintenance' && statusModalVehicle?.status !== 'Maintenance' && <div className="view-afce8558">
              <strong>Maintenance Notice:</strong> This unit will be marked out of service for inspection and repair. Customer bookings cannot be dispatched for this vehicle until restored to Available.
            </div>}
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseStatusModal} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button variant={targetStatus === 'Retired' ? 'danger' : 'primary'} onClick={handleConfirmStatusChange} disabled={isUpdatingStatus || targetStatus === statusModalVehicle?.status} isLoading={isUpdatingStatus}>
            Confirm Status Change
          </Button>
        </div>
      </Modal>

      {/* Vehicle Decommission / Retirement Confirmation Modal */}
      <Modal isOpen={Boolean(retireModalVehicle)} onClose={handleCloseRetireModal} title="Retire & Decommission Vehicle" subtitle={`Decommission ${retireModalVehicle?.year} ${retireModalVehicle?.make} ${retireModalVehicle?.model} from active commercial service`}>
        {retireModalError && <div className="view-4933c088">
            <Alert type="error" title="Decommission Error" message={retireModalError} />
          </div>}

        <div className="view-b1d83fd0">
          <div className="view-836939bd">
            <div>
              <strong className="view-ab7198dc">
                {retireModalVehicle?.year} {retireModalVehicle?.make} {retireModalVehicle?.model}
              </strong>
              <span className="view-409503d5">
                Plate: {retireModalVehicle?.licensePlate} • VIN: {retireModalVehicle?.vin}
              </span>
            </div>
            <div>
              <StatusBadge status={retireModalVehicle?.status} />
            </div>
          </div>

          <div className="view-454aaa1d">
            <div className="view-a9c77021">
              <WarningCircleIcon size={20} weight="regular" aria-hidden="true" />
              <div>
                <strong className="view-39b4304b">Vehicle retirement</strong>
                <p className="view-02419cff">
                  Retiring this vehicle marks it as decommissioned and safely excludes it from normal active-fleet views and customer rental availability.
                  <strong> All existing vehicle history, specifications, photos, and maintenance records will remain fully preserved in the database.</strong>
                </p>
              </div>
            </div>
          </div>

          <div className="view-4933c088">
            <label htmlFor="retireReasonInput" className="view-810b6506">
              Retirement / Decommission Reason (Optional):
            </label>
            <input id="retireReasonInput" type="text" placeholder="e.g., End of lease term, fleet renewal, sold, mechanical salvage" value={retireReason} onChange={e => setRetireReason(e.target.value)} disabled={isRetiring} className="filter-search-input view-9b20feff" />
          </div>
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleCloseRetireModal} disabled={isRetiring}>
            Cancel
          </Button>
          <Button variant="danger" onClick={handleConfirmRetire} disabled={isRetiring} isLoading={isRetiring}>
            Confirm Decommission & Retire
          </Button>
        </div>
      </Modal>
    {confirmationDialog}</div>;
};
export default ManageFleetPage;
