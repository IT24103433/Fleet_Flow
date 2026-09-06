import React, { useState, useEffect } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import { getVehicleById } from '../../services/vehicleService';

const VehicleDetailsPage = ({ selectedVehicle, onNavigate }) => {
  const [vehicle, setVehicle] = useState(selectedVehicle);
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const [reservationNotice, setReservationNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  useEffect(() => {
    let isMounted = true;
    if (selectedVehicle?.id) {
      getVehicleById(selectedVehicle.id).then((result) => {
        if (!isMounted) return;
        if (result.success && result.data) {
          setVehicle(result.data);
        } else if (result.status === 404) {
          setErrorMessage('Vehicle record was not found in the fleet catalog.');
        } else {
          // If network error occurred, retain selectedVehicle if available
          if (!selectedVehicle) {
            setErrorMessage(result.message || 'Failed to retrieve vehicle details.');
          }
        }
        setIsLoading(false);
      });
    }
    return () => {
      isMounted = false;
    };
  }, [selectedVehicle]);

  const handleReserveClick = () => {
    setReservationNotice({
      type: 'info',
      title: 'Booking Service Integration',
      message: `Reservation workflow for ${vehicle?.year} ${vehicle?.make} ${vehicle?.model} (VIN: ${vehicle?.vin}) verified. Booking endpoints (POST /api/bookings) will connect to BookingService in Sprint 2.`,
    });
  };

  if (isLoading) {
    return (
      <div className="vehicle-details-container" style={{ textAlign: 'center', padding: 'var(--space-12) 0' }}>
        <div className="spinner" style={{ width: '32px', height: '32px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
        <p>Loading vehicle specification details from FleetService...</p>
      </div>
    );
  }

  if (errorMessage && !vehicle) {
    return (
      <div className="vehicle-details-container">
        <div className="details-top-bar">
          <button
            type="button"
            className="back-catalog-btn"
            onClick={() => onNavigate('browse')}
          >
            ← Back to Fleet Catalog
          </button>
        </div>
        <Alert type="error" title="Vehicle Details Error" message={errorMessage} />
      </div>
    );
  }

  if (!vehicle) {
    return (
      <div className="vehicle-details-container">
        <div className="details-top-bar">
          <button
            type="button"
            className="back-catalog-btn"
            onClick={() => onNavigate('browse')}
          >
            ← Back to Fleet Catalog
          </button>
        </div>
        <div className="empty-state-card" style={{ padding: 'var(--space-8)' }}>
          <h3>No Vehicle Selected</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '16px' }}>Please choose a vehicle from the catalog to inspect details.</p>
          <Button variant="primary" onClick={() => onNavigate('browse')}>Browse Fleet Catalog</Button>
        </div>
      </div>
    );
  }

  const categoryName = vehicle.categoryName || vehicle.category || 'Executive';
  const fuelType = vehicle.fuelType || vehicle.fuel || 'Hybrid';
  const transmission = vehicle.transmission || 'Automatic';
  const seating = vehicle.seatingCapacity || vehicle.seating || '5 Passengers';
  const hubLocation = vehicle.hubLocation || vehicle.hub || 'Metro Hub - Terminal A';
  const licensePlate = vehicle.licensePlate || vehicle.plate || 'N/A';
  const mileage = typeof vehicle.mileage === 'number' ? `${vehicle.mileage.toLocaleString()} mi` : (vehicle.mileage || '0 mi');
  const isAvailable = vehicle.status === 'Available' || vehicle.status === 'AVAILABLE';

  const images = [
    { id: 'img-1', title: 'Exterior Angle View' },
    { id: 'img-2', title: 'Cockpit & Digital Console' },
    { id: 'img-3', title: 'Cabin & Seating' },
    { id: 'img-4', title: 'Rear Fascia' },
  ];

  const standardFeatures = [
    'Advanced Driver Assistance & Lane Assist',
    'Adaptive Cruise Control with Stop & Go',
    'High-Resolution Navigation & Telematics',
    'Keyless Smart Access & Digital Key Integration',
    'Multi-Zone Automatic Climate Control',
    'Apple CarPlay & Android Auto Compatibility',
  ];

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
        <div style={{ marginBottom: 'var(--space-6)' }}>
          <Alert
            type={reservationNotice.type}
            title={reservationNotice.title}
            message={reservationNotice.message}
          />
        </div>
      )}

      {/* Main Grid: Gallery on left, Pricing & Summary on right */}
      <div className="vehicle-main-grid">
        {/* Gallery & Showcase Column */}
        <div className="vehicle-gallery-col">
          <div className="hero-image-frame">
            <span className="hero-img-badge">{images[selectedImageIndex]?.title}</span>
            <div className="hero-model-watermark">
              <h2>{vehicle.make} {vehicle.model}</h2>
              <p>{categoryName} • {fuelType}</p>
            </div>
          </div>

          {/* Thumbnail Strip */}
          <div className="thumbnail-gallery-strip">
            {images.map((img, idx) => (
              <button
                key={img.id}
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
              {standardFeatures.map((feat, i) => (
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
              <span className="category-tag">{categoryName}</span>
              <h1 className="summary-vehicle-title">{vehicle.year} {vehicle.make} {vehicle.model}</h1>
              <p className="summary-hub-line">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14" aria-hidden="true">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span>Station: {hubLocation}</span>
              </p>
            </div>

            <div className="daily-pricing-box">
              <div className="price-tag-big">
                <span className="dollar-mark">$</span>
                <span className="rate-num">{Number(vehicle.dailyRate).toFixed(0)}</span>
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
                disabled={!isAvailable}
              >
                {isAvailable ? 'Reserve This Vehicle' : `Vehicle ${vehicle.status}`}
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
                <span className="spec-data">{fuelType}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Transmission</span>
                <span className="spec-data">{transmission}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Seating Capacity</span>
                <span className="spec-data">{seating}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Assigned Station</span>
                <span className="spec-data">{hubLocation}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">Odometer</span>
                <span className="spec-data font-mono">{mileage}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">License Plate</span>
                <span className="spec-data font-mono">{licensePlate}</span>
              </div>
              <div className="spec-table-row">
                <span className="spec-name">VIN</span>
                <span className="spec-data font-mono" style={{ wordBreak: 'break-all', fontSize: '11px' }}>{vehicle.vin}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default VehicleDetailsPage;
