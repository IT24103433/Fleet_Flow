import React, { useState } from 'react';
import StatusBadge from '../components/common/StatusBadge';
import Button from '../components/common/Button';
import { INITIAL_VEHICLES } from '../data/vehicleData';

const CATEGORIES = [
  'All Categories',
  'Executive Sedan',
  'Full-Size SUV',
  'Commercial Cargo',
  'Compact EV',
];

const BrowseFleetPage = ({ onNavigate, onSelectVehicle }) => {
  const [vehicles] = useState(INITIAL_VEHICLES);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All Categories');
  const [selectedFuel, setSelectedFuel] = useState('All Fuels');

  const filteredVehicles = vehicles.filter((v) => {
    const matchesSearch =
      v.model.toLowerCase().includes(searchTerm.toLowerCase()) ||
      v.make.toLowerCase().includes(searchTerm.toLowerCase()) ||
      v.category.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesCategory =
      selectedCategory === 'All Categories' || v.category === selectedCategory;

    const matchesFuel =
      selectedFuel === 'All Fuels' ||
      v.fuel.toLowerCase().includes(selectedFuel.toLowerCase());

    return matchesSearch && matchesCategory && matchesFuel;
  });

  const handleInspectVehicle = (vehicle) => {
    if (onSelectVehicle) {
      onSelectVehicle(vehicle);
    }
    onNavigate('vehicle-details');
  };

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
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-svg-icon">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="text"
              placeholder="Search by make, model, or keywords..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="browse-search-input"
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
              <option value="All Fuels">All Powertrains</option>
              <option value="Electric">100% Electric</option>
              <option value="Hybrid">Hybrid</option>
              <option value="Diesel">Diesel</option>
            </select>
          </div>
        </div>

        {/* Category Pills */}
        <div className="category-pills-scroll">
          {CATEGORIES.map((cat) => (
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

      {/* Results Count & Notice */}
      <div className="catalog-status-bar">
        <span className="catalog-count-text">
          Showing <strong>{filteredVehicles.length}</strong> of {vehicles.length} vehicles
        </span>
        <span className="data-source-badge">Catalog View Verified</span>
      </div>

      {/* Vehicles Grid */}
      {filteredVehicles.length === 0 ? (
        <div className="no-vehicles-found">
          <div className="no-results-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
          </div>
          <h3>No Vehicles Match Your Filter</h3>
          <p>Try clearing your search query or selecting a different category filter.</p>
          <Button variant="outline" size="sm" onClick={() => { setSearchTerm(''); setSelectedCategory('All Categories'); setSelectedFuel('All Fuels'); }}>
            Reset All Filters
          </Button>
        </div>
      ) : (
        <div className="catalog-grid">
          {filteredVehicles.map((vehicle) => (
            <article key={vehicle.id} className="vehicle-catalog-card">
              <div className="vehicle-image-banner">
                <span className="vehicle-category-chip">{vehicle.category}</span>
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
                  <span className="spec-pill">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="13" height="13">
                      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                      <circle cx="9" cy="7" r="4" />
                    </svg>
                    {vehicle.seating}
                  </span>
                  <span className="spec-pill">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="13" height="13">
                      <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                    </svg>
                    {vehicle.fuel}
                  </span>
                  <span className="spec-pill">{vehicle.transmission}</span>
                </div>

                <div className="vehicle-rate-and-cta">
                  <div className="rate-block">
                    <span className="price-number">${vehicle.dailyRate}</span>
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
          ))}
        </div>
      )}
    </div>
  );
};

export default BrowseFleetPage;
