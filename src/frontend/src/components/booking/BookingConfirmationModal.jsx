import React from 'react';
import Modal from '../common/Modal';
import Button from '../common/Button';
import StatusBadge from '../common/StatusBadge';
import { formatPriceNumber } from '../../utils/currencyUtils';
import './BookingConfirmationModal.css';

const BookingConfirmationModal = ({
  isOpen,
  onClose,
  booking,
  onNavigate,
}) => {
  if (!isOpen || !booking || !booking.id) {
    return null;
  }

  const vehicle = booking.vehicle || {};
  const vehicleTitle = vehicle.make && vehicle.model
    ? `${vehicle.year || ''} ${vehicle.make} ${vehicle.model}`.trim()
    : 'Vehicle Reservation';
  const categoryName = vehicle.categoryName || vehicle.category || 'Rental Vehicle';
  const station = vehicle.hubLocation || 'Central Station';
  const licensePlate = vehicle.licensePlate || 'N/A';

  const handleDashboardClick = () => {
    onClose();
    if (typeof onNavigate === 'function') {
      onNavigate('customer-home');
    } else {
      window.location.hash = '#/dashboard';
    }
  };

  const startFormatted = booking.startDateTime
    ? new Date(booking.startDateTime).toLocaleString()
    : 'N/A';
  const endFormatted = booking.endDateTime
    ? new Date(booking.endDateTime).toLocaleString()
    : 'N/A';

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Booking Confirmation"
      subtitle={`Reference: ${booking.id.substring(0, 8)}...`}
      maxWidth="560px"
    >
      <div className="booking-confirmation-container">
        {/* Success Banner */}
        <div className="booking-confirmation-banner">
          <svg className="booking-confirmation-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <div className="booking-confirmation-banner-text">
            <h4>Rental Reservation Confirmed!</h4>
            <p>Your vehicle has been successfully reserved and added to your account schedule.</p>
          </div>
        </div>

        {/* Detailed Reservation Summary Card */}
        <div className="booking-details-card">
          <div className="booking-header-row">
            <div>
              <h4 className="booking-vehicle-title">{vehicleTitle}</h4>
              <p className="booking-vehicle-subtext">
                {categoryName} • Station: {station} • License: {licensePlate}
              </p>
            </div>
            <StatusBadge status={booking.status || 'Confirmed'} />
          </div>

          <div className="booking-info-grid">
            <div className="booking-info-item" style={{ gridColumn: 'span 2' }}>
              <span className="booking-info-label">Booking Reference GUID</span>
              <span className="booking-guid-badge">{booking.id}</span>
            </div>

            {booking.customerId && (
              <div className="booking-info-item" style={{ gridColumn: 'span 2' }}>
                <span className="booking-info-label">Customer ID</span>
                <span className="booking-guid-badge" style={{ background: '#f8fafc' }}>{booking.customerId}</span>
              </div>
            )}

            <div className="booking-info-item">
              <span className="booking-info-label">Pick-up Date & Time</span>
              <span className="booking-info-value">{startFormatted}</span>
            </div>

            <div className="booking-info-item">
              <span className="booking-info-label">Return Date & Time</span>
              <span className="booking-info-value">{endFormatted}</span>
            </div>

            <div className="booking-summary-total">
              <span className="booking-total-label">Total Cost:</span>
              <span className="booking-total-amount">
                LKR {formatPriceNumber(booking.totalCost)}
              </span>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="booking-modal-actions">
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button variant="primary" onClick={handleDashboardClick}>
            Go to My Dashboard
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default BookingConfirmationModal;
