import React, { useState, useEffect } from 'react';
import Modal from './common/Modal';
import Button from './common/Button';
import Alert from './Alert';
import StatusBadge from './common/StatusBadge';
import { createBooking } from '../services/bookingService';
import { formatPriceNumber } from '../utils/currencyUtils';

const getTomorrowDateStr = (daysAhead = 1, hour = 9) => {
  const date = new Date();
  date.setDate(date.getDate() + daysAhead);
  date.setHours(hour, 0, 0, 0);
  const tzOffset = date.getTimezoneOffset() * 60000;
  const localISOTime = new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
  return localISOTime;
};

const BookingModal = ({ isOpen, onClose, vehicle, onSuccess }) => {
  const [startDateTime, setStartDateTime] = useState('');
  const [endDateTime, setEndDateTime] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);
  const [successBooking, setSuccessBooking] = useState(null);

  useEffect(() => {
    if (isOpen) {
      setStartDateTime(getTomorrowDateStr(1, 9));
      setEndDateTime(getTomorrowDateStr(4, 9));
      setErrorMessage(null);
      setSuccessBooking(null);
      setIsSubmitting(false);
    }
  }, [isOpen]);

  if (!vehicle) return null;

  const vehicleName = `${vehicle.year} ${vehicle.make} ${vehicle.model}`;
  const categoryName = vehicle.categoryName || vehicle.category || 'Vehicle';
  const dailyRate = Number(vehicle.dailyRate) || 0;

  // Calculate rental duration in days and total price
  let totalDays = 0;
  let estimatedCost = 0;
  if (startDateTime && endDateTime) {
    const startMs = new Date(startDateTime).getTime();
    const endMs = new Date(endDateTime).getTime();
    if (endMs > startMs) {
      const diffHours = (endMs - startMs) / (1000 * 60 * 60);
      totalDays = Math.max(1, Math.ceil(diffHours / 24));
      estimatedCost = totalDays * dailyRate;
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!startDateTime || !endDateTime) {
      setErrorMessage('Please specify both Start and End rental date & time.');
      return;
    }

    const startDateObj = new Date(startDateTime);
    const endDateObj = new Date(endDateTime);
    const now = new Date();

    if (startDateObj < new Date(now.getTime() - 5 * 60000)) {
      setErrorMessage('Rental start date and time must be in the future.');
      return;
    }

    if (endDateObj <= startDateObj) {
      setErrorMessage('Rental end date and time must be after the start date and time.');
      return;
    }

    setIsSubmitting(true);

    const result = await createBooking({
      vehicleId: vehicle.id,
      startDateTime: startDateObj.toISOString(),
      endDateTime: endDateObj.toISOString(),
    });

    setIsSubmitting(false);

    if (result.success && result.data) {
      setSuccessBooking(result.data);
      if (onSuccess) {
        onSuccess(result.data);
      }
    } else {
      setErrorMessage(result.message || 'Failed to complete vehicle reservation.');
    }
  };

  const handleModalClose = () => {
    setSuccessBooking(null);
    setErrorMessage(null);
    onClose();
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleModalClose}
      title={successBooking ? 'Booking Confirmed' : 'Create Rental Booking'}
      subtitle={successBooking ? `Reservation #${successBooking.id.substring(0, 8)}` : `Reserve ${vehicleName}`}
      maxWidth="560px"
    >
      {errorMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert type="error" title="Booking Validation Failure" message={errorMessage} />
        </div>
      )}

      {successBooking ? (
        <div className="booking-success-card" style={{ padding: '8px 0' }}>
          <Alert
            type="success"
            title="Rental Reservation Successfully Created!"
            message={`Your booking for ${vehicleName} has been confirmed. Confirmation ID: ${successBooking.id}`}
          />

          <div style={{ marginTop: '20px', background: 'var(--color-bg-secondary, #f8fafc)', borderRadius: '8px', padding: '16px', border: '1px solid var(--color-border, #e2e8f0)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
              <h4 style={{ margin: 0, fontSize: '15px' }}>{vehicleName}</h4>
              <StatusBadge status={successBooking.status || 'Confirmed'} />
            </div>

            <div style={{ fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              <div>
                <strong>Booking ID:</strong>
                <div style={{ fontFamily: 'monospace', fontSize: '12px', wordBreak: 'break-all' }}>{successBooking.id}</div>
              </div>
              <div>
                <strong>Customer ID:</strong>
                <div style={{ fontFamily: 'monospace', fontSize: '12px', wordBreak: 'break-all' }}>{successBooking.customerId}</div>
              </div>
              <div>
                <strong>Pick-up Date:</strong>
                <div>{new Date(successBooking.startDateTime).toLocaleString()}</div>
              </div>
              <div>
                <strong>Return Date:</strong>
                <div>{new Date(successBooking.endDateTime).toLocaleString()}</div>
              </div>
              <div style={{ gridColumn: 'span 2', paddingTop: '8px', borderTop: '1px dashed #cbd5e1' }}>
                <strong style={{ fontSize: '14px', color: 'var(--color-text-primary, #0f172a)' }}>Total Paid / Reserved: </strong>
                <span style={{ fontSize: '16px', fontWeight: 'bold', color: 'var(--color-primary, #2563eb)' }}>
                  LKR {formatPriceNumber(successBooking.totalCost)}
                </span>
              </div>
            </div>
          </div>

          <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
            <Button variant="primary" onClick={handleModalClose}>
              Done & Close
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit}>
          {/* Vehicle Summary Header in Modal */}
          <div style={{ background: 'var(--color-bg-secondary, #f8fafc)', padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', border: '1px solid var(--color-border, #e2e8f0)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <span style={{ fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.5px', color: 'var(--color-text-muted, #94a3b8)', fontWeight: '600' }}>
                  {categoryName}
                </span>
                <h4 style={{ margin: '2px 0 0', fontSize: '15px' }}>{vehicleName}</h4>
              </div>
              <div style={{ textAlign: 'right' }}>
                <span style={{ fontSize: '16px', fontWeight: 'bold', color: 'var(--color-primary, #2563eb)' }}>
                  LKR {formatPriceNumber(dailyRate)}
                </span>
                <span style={{ fontSize: '12px', color: 'var(--color-text-secondary, #64748b)' }}>/day</span>
              </div>
            </div>
            <div style={{ fontSize: '12px', color: 'var(--color-text-secondary, #64748b)', marginTop: '6px' }}>
              Station: {vehicle.hubLocation || vehicle.hub || 'Central Station'} • License: {vehicle.licensePlate || 'N/A'}
            </div>
          </div>

          {/* Date Range Inputs */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '20px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '6px' }}>
                Start Date & Time <span style={{ color: 'red' }}>*</span>
              </label>
              <input
                type="datetime-local"
                className="input-field"
                value={startDateTime}
                onChange={(e) => setStartDateTime(e.target.value)}
                required
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '6px' }}>
                End Date & Time <span style={{ color: 'red' }}>*</span>
              </label>
              <input
                type="datetime-local"
                className="input-field"
                value={endDateTime}
                onChange={(e) => setEndDateTime(e.target.value)}
                required
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
              />
            </div>
          </div>

          {/* Pricing & Duration Summary */}
          {totalDays > 0 && (
            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', padding: '12px 16px', marginBottom: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: '#166534', marginBottom: '4px' }}>
                <span>Rental Duration:</span>
                <strong>{totalDays} {totalDays === 1 ? 'day' : 'days'}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '15px', color: '#15803d', fontWeight: 'bold' }}>
                <span>Estimated Total Cost:</span>
                <span>LKR {formatPriceNumber(estimatedCost)}</span>
              </div>
            </div>
          )}

          {/* Modal Actions */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '24px' }}>
            <Button type="button" variant="outline" onClick={handleModalClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={isSubmitting || totalDays <= 0}>
              {isSubmitting ? 'Confirming Booking...' : 'Confirm Rental Booking'}
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
};

export default BookingModal;
