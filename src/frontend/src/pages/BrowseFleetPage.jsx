import React, { useState, useEffect, useCallback } from 'react';
import StatusBadge from '../components/common/StatusBadge';
import Button from '../components/common/Button';
import Alert from '../components/Alert';
import { getVehicles, getCategories } from '../services/vehicleService';

const POWERTRAIN_OPTIONS = [
  'All Powertrains',
  'Electric',
  'Hybrid',
  'Plug-in Hybrid',
  'Gasoline',
  'Diesel',
];

const BrowseFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const [vehicles, setVehicles] = useState([]);
  const [categories, setCategories] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All Categories');
  const [selectedFuel, setSelectedFuel] = useState('All Powertrains');

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

    const params = {};
    if (selectedCategory && selectedCategory !== 'All Categories') {
      params.category = selectedCategory;
    }
    if (selectedFuel && selectedFuel !== 'All Powertrains') {
      params.fuel = selectedFuel;
    }
    if (searchTerm.trim()) {
      params.searchTerm = searchTerm.trim();
    }

    const result = await getVehicles(params);

    if (result.success) {
      setVehicles(result.data);
    } else {
      setErrorMessage(result.message || 'Failed to load vehicle catalog.');
      setVehicles([]);
    }
    setIsLoading(false);
  }, [selectedCategory, selectedFuel, searchTerm]);

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
  };

  const categoryList = ['All Categories', ...categories.map((c) => c.name || c)];

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
              placeholder="Search by make, model, category, or keywords..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="browse-search-input"
              aria-label="Search vehicles"
            />
          </div>

          <div className="filter-dropdown-wrap">
            <label htmlFor="fuelFilter" className="dropdown-label">Powertrain:</label>
            <select
              id="fuelFilter"
              value={selectedFuel}
              onChange={(e) => setSelectedFuel(e.target.value)}
              className="browse-select"
            >
              {POWERTRAIN_OPTIONS.map((fuel) => (
                <option key={fuel} value={fuel}>
                  {fuel}
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
              onClick={() => setSelectedCategory(cat)}
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
            Showing <strong>{vehicles.length}</strong> {vehicles.length === 1 ? 'vehicle' : 'vehicles'} available in catalog
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
        <div className="catalog-grid">
          {vehicles.map((vehicle) => {
            const categoryName = vehicle.categoryName || vehicle.category || 'Standard';
            const fuelType = vehicle.fuelType || vehicle.fuel || 'Hybrid';
            const transmission = vehicle.transmission || 'Automatic';
            const seating = vehicle.seatingCapacity || vehicle.seating || '5 Seats';

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
                      <span className="price-number">${Number(vehicle.dailyRate).toFixed(0)}</span>
                      <span className="price-period">/ day</span>
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
      )}
    </div>
  );
};

export default BrowseFleetPage;
