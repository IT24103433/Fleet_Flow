import React, { useState, useRef } from 'react';
import Modal from './common/Modal';
import Button from './common/Button';
import Alert from './Alert';
import StatusBadge from './common/StatusBadge';
import { checkVehicleAvailability, createBooking } from '../services/bookingService';
import { formatPriceNumber } from '../utils/currencyUtils';
import { normalizeValidationErrors } from '../utils/validationErrorUtils';
import { bookingEstimate } from '../utils/vehiclePresentation';
import { onboardingRequest } from '../services/onboardingService';
const getTomorrowDateStr = (daysAhead = 1, hour = 9) => {
  const date = new Date();
  date.setDate(date.getDate() + daysAhead);
  date.setHours(hour, 0, 0, 0);
  const tzOffset = date.getTimezoneOffset() * 60000;
  const localISOTime = new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
  return localISOTime;
};
const BookingModal = ({
  isOpen,
  onClose,
  vehicle,
  onSuccess,
  onNavigate
}) => {
  const savedIntent = (() => { try { const draft = JSON.parse(sessionStorage.getItem('fleetflow:booking-intent') || 'null'); return draft?.vehicleId === vehicle?.id ? draft : null; } catch { return null; } })();
  const [startDateTime, setStartDateTime] = useState(() => savedIntent?.startDateTime || getTomorrowDateStr(1, 9));
  const [endDateTime, setEndDateTime] = useState(() => savedIntent?.endDateTime || getTomorrowDateStr(4, 9));
  const [verificationRequired, setVerificationRequired] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [successBooking, setSuccessBooking] = useState(null);
  const submission = useRef(false);
  if (!vehicle) return null;
  const vehicleName = `${vehicle.year} ${vehicle.make} ${vehicle.model}`;
  const categoryName = vehicle.categoryName || vehicle.category || 'Vehicle';
  const dailyRate = vehicle.dailyRate;

  // Calculate rental duration in days and total price
  const {
    days: totalDays,
    cost: estimatedCost
  } = bookingEstimate(startDateTime, endDateTime, dailyRate);
  const handleSubmit = async e => {
    e.preventDefault();
    if (submission.current) return;
    setErrorMessage(null);
    setFieldErrors({});
    if (!startDateTime || !endDateTime) {
      setErrorMessage('Please specify both Start and End rental date & time.');
      setFieldErrors({
        ...(!startDateTime ? {
          startDateTime: 'Start date and time are required.'
        } : {}),
        ...(!endDateTime ? {
          endDateTime: 'End date and time are required.'
        } : {})
      });
      return;
    }
    const startDateObj = new Date(startDateTime);
    const endDateObj = new Date(endDateTime);
    const now = new Date();
    if (!Number.isFinite(startDateObj.getTime()) || !Number.isFinite(endDateObj.getTime()) || estimatedCost == null) {
      setErrorMessage('Enter valid rental dates and check that a daily rate is recorded.');
      return;
    }
    if (startDateObj <= now) {
      setErrorMessage('Rental start date and time must be in the future.');
      setFieldErrors({
        startDateTime: 'Rental start date and time must be in the future.'
      });
      return;
    }
    if (endDateObj <= startDateObj) {
      setErrorMessage('Rental end date and time must be after the start date and time.');
      setFieldErrors({
        endDateTime: 'Rental end date and time must be after the start date and time.'
      });
      return;
    }
    submission.current = true;
    setIsSubmitting(true);
    setSuccessBooking(null);
    try {
      const policy = await onboardingRequest('config', { anonymous: true });
      if (policy?.enabled) {
        const eligibility = await onboardingRequest(`eligibility?returnAt=${encodeURIComponent(endDateObj.toISOString())}`);
        if (!eligibility.eligible) {
          sessionStorage.setItem('fleetflow:booking-intent', JSON.stringify({ vehicleId: vehicle.id, startDateTime, endDateTime }));
          setVerificationRequired(true); setErrorMessage(eligibility.reason || 'Complete driver verification before booking.');
          submission.current = false; setIsSubmitting(false); return;
        }
      }
    } catch (error) { submission.current = false; setIsSubmitting(false); setErrorMessage(error.message); return; }
    const availabilityResult = await checkVehicleAvailability(vehicle.id, startDateObj.toISOString(), endDateObj.toISOString());
    if (!availabilityResult.success || !availabilityResult.isAvailable) {
      submission.current = false;
      setIsSubmitting(false);
      setErrorMessage(availabilityResult.message || 'The selected vehicle is not available for that rental period.');
      return;
    }
    const result = await createBooking({
      vehicleId: vehicle.id,
      startDateTime: startDateObj.toISOString(),
      endDateTime: endDateObj.toISOString()
    });
    submission.current = false;
    setIsSubmitting(false);
    if (result.success && result.data) {
      setSuccessBooking(result.data);
      sessionStorage.removeItem('fleetflow:booking-intent');
      if (onSuccess) {
        onSuccess(result.data);
      }
    } else {
      setFieldErrors(normalizeValidationErrors(result.errors));
      setErrorMessage(result.message || 'Failed to complete vehicle reservation.');
    }
  };
  const handleModalClose = () => {
    if (submission.current) return;
    setStartDateTime(getTomorrowDateStr(1, 9));
    setEndDateTime(getTomorrowDateStr(4, 9));
    setIsSubmitting(false);
    setSuccessBooking(null);
    setErrorMessage(null);
    setFieldErrors({});
    onClose();
  };
  const confirmedVehicle = successBooking?.vehicle || vehicle;
  const confirmedVehicleName = confirmedVehicle ? `${confirmedVehicle.year} ${confirmedVehicle.make} ${confirmedVehicle.model}` : vehicleName;
  return <Modal isOpen={isOpen} onClose={handleModalClose} title={successBooking ? 'Booking created' : 'Choose your rental dates'} subtitle={successBooking ? `Reservation #${successBooking.id.substring(0, 8)}` : `Reserve ${vehicleName}`} maxWidth="560px">
      {verificationRequired && <div className="action-row"><Button onClick={() => { onClose(); if (onNavigate) onNavigate('customer-profile'); else window.location.hash = '#/customer-profile?section=driving'; }}>Complete driver verification</Button></div>}
      {errorMessage && <div className="view-4933c088">
          <Alert type="error" title="Booking Validation Failure" message={errorMessage} />
        </div>}

      {successBooking ? <div className="booking-success-card view-5788b5cd">
          <Alert type="success" title="Your booking is saved" message={`Booking #${successBooking.id.slice(0, 8)} for ${confirmedVehicleName}. You can find the details in My bookings.`} />

          <div className="view-85da18bc">
            <div className="view-ab4de765">
              <h4 className="view-05291f22">{confirmedVehicleName}</h4>
              <StatusBadge status={successBooking.status} />
            </div>

            <div className="view-73be117d">
              <div>
                <strong>Booking ID:</strong>
                <div className="view-c33e604a">{successBooking.id}</div>
              </div>
              <div>
                <strong>Vehicle:</strong>
                <div>{confirmedVehicle?.licensePlate || successBooking.vehicleId}</div>
              </div>
              <div>
                <strong>Pick-up Date:</strong>
                <div>{new Date(successBooking.startDateTime).toLocaleString()}</div>
              </div>
              <div>
                <strong>Return Date:</strong>
                <div>{new Date(successBooking.endDateTime).toLocaleString()}</div>
              </div>
              <div className="view-ffe206a5">
                <strong className="view-ec48a151">Total Reserved: </strong>
                <span className="view-6208e615">
                  LKR {formatPriceNumber(successBooking.totalCost)}
                </span>
              </div>
            </div>
          </div>

          <div className="view-578fa8e3">
            {onNavigate && <Button variant="outline" onClick={() => { handleModalClose(); onNavigate('customer-home'); }}>My bookings</Button>}
            <Button variant="primary" onClick={handleModalClose}>
              Done
            </Button>
          </div>
        </div> : <form onSubmit={handleSubmit}>
          {/* Vehicle Summary Header in Modal */}
          <div className="view-d6aef1d2">
            <div className="view-3490dbc8">
              <div>
                <span className="view-eee5dd41">
                  {categoryName}
                </span>
                <h4 className="view-3970320f">{vehicleName}</h4>
              </div>
              <div className="view-37dd0c2a">
                <span className="view-6208e615">
                  LKR {formatPriceNumber(dailyRate)}
                </span>
                <span className="view-150363c2">/day</span>
              </div>
            </div>
            <div className="view-91b55929">
              Pickup location: {vehicle.hubLocation || vehicle.hub || 'Not recorded'} • Registration: {vehicle.licensePlate || 'Not recorded'}
            </div>
          </div>

          {/* Date Range Inputs */}
          <div className="view-f514f124 booking-date-fields">
            <div>
              <label htmlFor="bookingStartDateTime" className="view-83e308c1">
                Pickup date & time <span className="required-indicator">*</span>
              </label>
              <input id="bookingStartDateTime" type="datetime-local" className={`input-field ${fieldErrors.startDateTime ? 'input-error' : ''}`.trim() + " view-1ac81575"} value={startDateTime} onChange={e => {
            setStartDateTime(e.target.value);
            setFieldErrors(current => ({
              ...current,
              startDateTime: undefined
            }));
          }} required data-autofocus aria-invalid={Boolean(fieldErrors.startDateTime)} aria-describedby={fieldErrors.startDateTime ? 'bookingStartDateTimeError' : undefined} />
              {fieldErrors.startDateTime && <div id="bookingStartDateTimeError" className="field-error-message">{fieldErrors.startDateTime}</div>}
            </div>

            <div>
              <label htmlFor="bookingEndDateTime" className="view-83e308c1">
                Return date & time <span className="required-indicator">*</span>
              </label>
              <input id="bookingEndDateTime" type="datetime-local" className={`input-field ${fieldErrors.endDateTime ? 'input-error' : ''}`.trim() + " view-1ac81575"} value={endDateTime} onChange={e => {
            setEndDateTime(e.target.value);
            setFieldErrors(current => ({
              ...current,
              endDateTime: undefined
            }));
          }} required aria-invalid={Boolean(fieldErrors.endDateTime)} aria-describedby={fieldErrors.endDateTime ? 'bookingEndDateTimeError' : undefined} />
              {fieldErrors.endDateTime && <div id="bookingEndDateTimeError" className="field-error-message">{fieldErrors.endDateTime}</div>}
            </div>
          </div>

          {/* Pricing & Duration Summary */}
          {totalDays > 0 && <div className="view-cd1dde64">
              <div className="view-d7025caa">
                <span>Rental Duration:</span>
                <strong>{totalDays} {totalDays === 1 ? 'day' : 'days'}</strong>
              </div>
              <div className="view-3d2cb959">
                <span>Estimated Total Cost:</span>
                <span>LKR {formatPriceNumber(estimatedCost)}</span>
              </div>
            </div>}

          {/* Modal Actions */}
          <div className="view-84395218">
            <Button type="button" variant="outline" onClick={handleModalClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={isSubmitting || totalDays <= 0 || estimatedCost == null}>
              {isSubmitting ? 'Checking & booking…' : 'Book vehicle'}
            </Button>
          </div>
        </form>}
    </Modal>;
};
export default BookingModal;
