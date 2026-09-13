import React, { useState, useEffect, useCallback, useMemo } from 'react';
import StatusBadge from '../components/common/StatusBadge';
import Button from '../components/common/Button';
import Alert from '../components/Alert';
import { getVehicles, getCategories } from '../services/vehicleService';
import { formatPriceNumber } from '../utils/currencyUtils';

const POWERTRAIN_OPTIONS = [
  'All Powertrains',
  'Electric',
  'Hybrid',
  'Plug-in Hybrid',
  'Gasoline',
  'Diesel',
];

const TRANSMISSION_OPTIONS = [
  'All Transmissions',
  'Automatic',
  'Manual',
];

const SORT_OPTIONS = [
  { value: 'createdAt:desc', label: 'Newest Arrivals' },
  { value: 'dailyRate:asc', label: 'Daily Rate (Low to High)' },
  { value: 'dailyRate:desc', label: 'Daily Rate (High to Low)' },
  { value: 'make:asc', label: 'Make & Model (A–Z)' },
  { value: 'year:desc', label: 'Model Year (Newest)' },
];

const PAGE_SIZE = 9;

const BrowseFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const [vehicles, setVehicles] = useState([]);
  const [categories, setCategories] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All Categories');
  const [selectedFuel, setSelectedFuel] = useState('All Powertrains');
  const [selectedTransmission, setSelectedTransmission] = useState('All Transmissions');
  const [selectedSort, setSelectedSort] = useState('createdAt:desc');

  const [currentPage, setCurrentPage] = useState(1);
  const [pagination, setPagination] = useState({
    totalCount: 0,
    page: 1,
    pageSize: PAGE_SIZE,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  });

  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);

  // Load Categories on mount
  useEffect(() => {
    let isMounted = true;
    const fetchCategories = async () => {
      const result = await getCategories();
      if (isMounted && result.success && Array.isArray(result.data)) {
        setCategories(result.data);
      }
    };
    fetchCategories();
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
      pageSize: PAGE_SIZE,
      sortBy: sortBy || 'createdAt',
      sortOrder: sortOrder || 'desc',
    };

    if (selectedCategory && selectedCategory !== 'All Categories') {
      params.category = selectedCategory;
    }
    if (selectedFuel && selectedFuel !== 'All Powertrains') {
      params.fuel = selectedFuel;
    }
    if (selectedTransmission && selectedTransmission !== 'All Transmissions') {
      params.transmission = selectedTransmission;
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
          pageSize: PAGE_SIZE,
          totalPages: Math.ceil(result.data.length / PAGE_SIZE) || 1,
          hasNextPage: false,
          hasPreviousPage: currentPage > 1,
        });
      }
    } else {
      setErrorMessage(result.message || 'Failed to load vehicle catalog.');
      setVehicles([]);
      setPagination((prev) => ({ ...prev, totalCount: 0, totalPages: 1 }));
    }
    setIsLoading(false);
  }, [selectedCategory, selectedFuel, selectedTransmission, selectedSort, searchTerm, currentPage]);

  // Trigger fetch when filters change (with debounce on search)
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

  const handleResetFilters = () => {
    setSearchTerm('');
    setSelectedCategory('All Categories');
    setSelectedFuel('All Powertrains');
    setSelectedTransmission('All Transmissions');
    setSelectedSort('createdAt:desc');
    setCurrentPage(1);
  };

  const categoryList = ['All Categories', ...categories.map((c) => c.name || c)];

  const totalCount = pagination.totalCount || 0;
  const totalPages = pagination.totalPages || 1;

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
    <div className="browse-fleet-container">
      {/* Hero Header */}
      <div className="browse-header-section">
        <div className="browse-header-content">
          <span className="browse-tag">FleetFlow Mobility Catalog</span>
          <h1 className="browse-title">Explore Premium Vehicle Fleet</h1>
          <p className="browse-subtitle">
            Select from our curated collection of executive sedans, spacious SUVs, commercial vans, and high-efficiency electric vehicles.
          </p>
        </div>
      </div>

      {/* Filter and Search Controls */}
      <div className="browse-controls-card">
        <div className="search-and-dropdowns-row">
          <div className="search-input-field">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-svg-icon" aria-hidden="true">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="text"
              placeholder="Search by make, model, VIN, plate, or keywords..."
              value={searchTerm}
              onChange={(e) => { setSearchTerm(e.target.value); setCurrentPage(1); }}
              className="browse-search-input"
              aria-label="Search vehicles"
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
                }}
                aria-label="Clear search"
              >
                ✕
              </button>
            )}
          </div>

          <div className="filter-dropdown-wrap">
            <label htmlFor="fuelFilter" className="dropdown-label">Powertrain:</label>
            <select
              id="fuelFilter"
              value={selectedFuel}
              onChange={(e) => { setSelectedFuel(e.target.value); setCurrentPage(1); }}
              className="browse-select"
            >
              {POWERTRAIN_OPTIONS.map((fuel) => (
                <option key={fuel} value={fuel}>
                  {fuel}
                </option>
              ))}
            </select>
          </div>

          <div className="filter-dropdown-wrap">
            <label htmlFor="transmissionFilter" className="dropdown-label">Trans:</label>
            <select
              id="transmissionFilter"
              value={selectedTransmission}
              onChange={(e) => { setSelectedTransmission(e.target.value); setCurrentPage(1); }}
              className="browse-select"
            >
              {TRANSMISSION_OPTIONS.map((trans) => (
                <option key={trans} value={trans}>
                  {trans}
                </option>
              ))}
            </select>
          </div>

          <div className="filter-dropdown-wrap">
            <label htmlFor="sortFilter" className="dropdown-label">Sort:</label>
            <select
              id="sortFilter"
              value={selectedSort}
              onChange={(e) => { setSelectedSort(e.target.value); setCurrentPage(1); }}
              className="browse-select"
            >
              {SORT_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Category Pills */}
        <div className="category-pills-scroll" role="tablist" aria-label="Vehicle Categories">
          {categoryList.map((cat) => (
            <button
              key={cat}
              type="button"
              className={`category-pill-btn ${selectedCategory === cat ? 'active' : ''}`}
              onClick={() => { setSelectedCategory(cat); setCurrentPage(1); }}
            >
              {cat}
            </button>
          ))}
        </div>
      </div>

      {/* Error State Banner */}
      {errorMessage && (
        <div style={{ marginBottom: 'var(--space-6)' }}>
          <Alert
            type="error"
            title="Service Communication Error"
            message={errorMessage}
          />
          <div style={{ marginTop: 'var(--space-3)', display: 'flex', justifyContent: 'flex-end' }}>
            <Button variant="outline" size="sm" onClick={fetchVehicles}>
              ↻ Retry Fleet Query
            </Button>
          </div>
        </div>
      )}

      {/* Loading State */}
      {isLoading && (
        <div style={{ textAlign: 'center', padding: 'var(--space-12) 0', color: 'var(--color-text-secondary)' }}>
          <div className="spinner" style={{ width: '32px', height: '32px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
          <p style={{ fontWeight: 500 }}>Connecting to Fleet Service & loading inventory...</p>
        </div>
      )}

      {/* Results Count & Notice */}
      {!isLoading && !errorMessage && (
        <div className="catalog-status-bar">
          <span className="catalog-count-text">
            Showing <strong>{vehicles.length}</strong> {vehicles.length === 1 ? 'vehicle' : 'vehicles'} (Page {currentPage} of {totalPages}, Total: {totalCount})
          </span>
          <span className="data-source-badge">Live FleetService API</span>
        </div>
      )}

      {/* Vehicles Grid / Empty State */}
      {!isLoading && !errorMessage && vehicles.length === 0 && (
        <div className="no-vehicles-found">
          <div className="no-results-icon" style={{ width: '48px', height: '48px', color: 'var(--color-text-muted)', marginBottom: '8px' }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="48" height="48" aria-hidden="true">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
          </div>
          <h3>No Vehicles Match Your Query</h3>
          <p style={{ color: 'var(--color-text-secondary)', fontSize: '14px', maxWidth: '420px', margin: '0 auto 16px' }}>
            There are currently no vehicles matching your active search or filter criteria.
          </p>
          <Button variant="outline" size="sm" onClick={handleResetFilters}>
            Reset All Filters
          </Button>
        </div>
      )}

      {!isLoading && !errorMessage && vehicles.length > 0 && (
        <>
          <div className="catalog-grid">
            {vehicles.map((vehicle) => {
              const categoryName = vehicle.categoryName || vehicle.category || '—';
              const fuelType = vehicle.fuelType || vehicle.fuel || '—';
              const transmission = vehicle.transmission || '—';
              const seating = vehicle.seatingCapacity || vehicle.seating || '—';

              return (
                <article key={vehicle.id} className="vehicle-catalog-card">
                  <div className="vehicle-image-banner">
                    <span className="vehicle-category-chip">{categoryName}</span>
                    <div className="vehicle-card-status-slot">
                      <StatusBadge status={vehicle.status} />
                    </div>
                    <div className="vehicle-visual-model">
                      <span>{vehicle.make} {vehicle.model}</span>
                    </div>
                  </div>

                  <div className="vehicle-catalog-body">
                    <div className="vehicle-title-strip">
                      <h3 className="vehicle-name-h3">{vehicle.year} {vehicle.make} {vehicle.model}</h3>
                    </div>

                    <div className="vehicle-spec-pills-row">
                      <span className="spec-pill" title="Seating Capacity">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="13" height="13" aria-hidden="true">
                          <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                          <circle cx="9" cy="7" r="4" />
                        </svg>
                        {seating}
                      </span>
                      <span className="spec-pill" title="Powertrain">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="13" height="13" aria-hidden="true">
                          <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                        </svg>
                        {fuelType}
                      </span>
                      <span className="spec-pill" title="Transmission">{transmission}</span>
                    </div>

                    <div className="vehicle-rate-and-cta">
                      <div className="rate-block">
                        {vehicle.dailyRate != null && !isNaN(Number(vehicle.dailyRate)) ? (
                          <>
                            <span className="price-number">LKR {formatPriceNumber(vehicle.dailyRate)}</span>
                            <span className="price-period">/day</span>
                          </>
                        ) : (
                          <span className="price-number">—</span>
                        )}
                      </div>
                      <Button
                        variant="primary"
                        size="sm"
                        onClick={() => handleInspectVehicle(vehicle)}
                      >
                        View Details
                      </Button>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>

          {/* Customer Catalog Pagination */}
          {totalPages > 1 && (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '6px', margin: '2rem 0 1rem' }}>
              <button
                type="button"
                className="pagination-page-btn"
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                aria-label="Previous page"
              >
                ‹ Prev
              </button>
              {pageNumbers.map((p) => (
                <button
                  key={p}
                  type="button"
                  className={`pagination-page-btn ${p === currentPage ? 'active' : ''}`}
                  onClick={() => setCurrentPage(p)}
                >
                  {p}
                </button>
              ))}
              <button
                type="button"
                className="pagination-page-btn"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                aria-label="Next page"
              >
                Next ›
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default BrowseFleetPage;
