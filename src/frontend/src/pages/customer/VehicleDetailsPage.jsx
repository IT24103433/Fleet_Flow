import React, { useState } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import { INITIAL_VEHICLES } from '../../data/vehicleData';

const VehicleDetailsPage = ({ selectedVehicle, onNavigate }) => {
  const vehicle = selectedVehicle || INITIAL_VEHICLES[0];
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const [reservationNotice, setReservationNotice] = useState(null);

  const images = vehicle.images || [
    { id: 'img-1', title: 'Main Exterior', url: null },
    { id: 'img-2', title: 'Cockpit View', url: null },
    { id: 'img-3', title: 'Cabin & Seating', url: null },
  ];

  const handleReserveClick = () => {
    setReservationNotice({
      type: 'info',
      title: 'Booking Service Integration',
      message: `Reservation workflow for ${vehicle.year} ${vehicle.make} ${vehicle.model} verified. Booking endpoints (POST /api/bookings) will be connected to BookingService in Sprint 2.`,
    });
  };

  return (
    <div className="vehicle-details-container">
      {/* Top Navigation & Breadcrumb */}
      <div className="details-top-bar">
        <button
          type="button"
          className="back-catalog-btn"
          onClick={() => onNavigate('browse')}
        >
          ← Back to Fleet Catalog
        </button>
        <div className="top-status-slot">
          <StatusBadge status={vehicle.status} />
        </div>
      </div>

      {reservationNotice && (
        <Alert
          type={reservationNotice.type}
          title={reservationNotice.title}
          message={reservationNotice.message}
        />
      )}

      {/* Main Grid: Gallery on left, Pricing & Summary on right */}
      <div className="vehicle-main-grid">
        {/* Gallery & Showcase Column */}
        <div className="vehicle-gallery-col">
          <div className="hero-image-frame">
            <div className="hero-img-content">
              <span className="hero-img-badge">{images[selectedImageIndex]?.title || 'Vehicle Spec'}</span>
              <div className="hero-model-watermark">
                <h2>{vehicle.make} {vehicle.model}</h2>
                <p>{vehicle.category} • {vehicle.fuel}</p>
              </div>
            </div>
          </div>

          {/* Thumbnail Strip */}
          <div className="thumbnail-gallery-strip">
            {images.map((img, idx) => (
              <button
                key={img.id || idx}
                type="button"
                className={`thumb-btn ${selectedImageIndex === idx ? 'active' : ''}`}
                onClick={() => setSelectedImageIndex(idx)}
              >
                <div className="thumb-preview">
                  <span>View {idx + 1}</span>
                </div>
                <span className="thumb-label">{img.title}</span>
              </button>
            ))}
          </div>

          {/* Key Features & Equipment */}
          <div className="features-checklist-card">
            <h3 className="features-card-title">Standard Equipment & Technology</h3>
            <ul className="features-bullet-list">
              {vehicle.features?.map((feat, i) => (
                <li key={i} className="feature-item">
                  <svg viewBox="0 0 20 20" fill="currentColor" className="check-svg-icon" aria-hidden="true">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                  <span>{feat}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Vehicle Specs & Reservation Summary Column */}
        <div className="vehicle-summary-col">
          <div className="summary-card">
            <div className="summary-title-section">
              <span className="category-tag">{vehicle.category}</span>
              <h1 className="summary-vehicle-title">{vehicle.year} {vehicle.make} {vehicle.model}</h1>
              <p className="summary-hub-line">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span>Station: {vehicle.hub}</span>
              </p>
            </div>

            <div className="daily-pricing-box">
              <div className="price-tag-big">
                <span className="dollar-mark">$</span>
                <span className="rate-num">{vehicle.dailyRate}</span>
                <span className="rate-unit">/ day</span>
              </div>
              <span className="tax-notice">Includes standard liability & unlimited local mileage</span>
            </div>

            <div className="reservation-action-block">
              <Button
                variant="primary"
                size="lg"
                fullWidth
                onClick={handleReserveClick}
                disabled={vehicle.status !== 'AVAILABLE'}
              >
                {vehicle.status === 'AVAILABLE' ? 'Reserve This Vehicle' : `Vehicle ${vehicle.status}`}
              </Button>
              <p className="instant-confirm-text">
                Free cancellation up to 24 hours prior to scheduled dispatch.
              </p>
            </div>

            {/* Technical Specifications Grid */}
            <div className="specs-detail-table">
              <h4 className="specs-table-heading">Technical Specifications</h4>
              <div className="spec-table-row">
                <span className="spec-name">Powertrain</span>
                <span className="spec-data">{vehicle.engine}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Transmission</span>
                <span className="spec-data">{vehicle.transmission}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Fuel / Energy</span>
                <span className="spec-data">{vehicle.fuel}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Estimated Range</span>
                <span className="spec-data">{vehicle.range}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Seating Capacity</span>
                <span className="spec-data">{vehicle.seating}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Luggage Volume</span>
                <span className="spec-data">{vehicle.luggage}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">VIN</span>
                <span className="spec-data font-mono">{vehicle.vin}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default VehicleDetailsPage;
