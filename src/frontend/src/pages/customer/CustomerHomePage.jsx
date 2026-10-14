import { rentalStateLabel } from '../../utils/rentalPresentation';
import { ArrowUpRightIcon } from "@phosphor-icons/react/dist/csr/ArrowUpRight";
import React, { useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import StatusBadge from '../../components/common/StatusBadge';
import VehicleCard from '../../components/common/VehicleCard';
import Alert from '../../components/Alert';
import { getVehicles } from '../../services/vehicleService';
import { cancelBooking, getMyBookings } from '../../services/bookingService';
import { formatLKR } from '../../utils/currencyUtils';
import { isBookingCancellable } from '../../validation/bookingValidation';
export default function CustomerHomePage({
  onNavigate
}) {
  const {
    user,
    token
  } = useAuth();
  const [result, setResult] = useState(null);
  const [revision, setRevision] = useState(0);
  const [cancelTarget, setCancelTarget] = useState(null);
  const [pending, setPending] = useState(false);
  const [notice, setNotice] = useState(null);
  const submitting = useRef(false);
  const key = `${token}:${revision}`;
  useEffect(() => {
    let active = true;
    Promise.all([getMyBookings(), getVehicles({
      status: 'Available',
      pageSize: 3
    })]).then(([bookings, vehicles]) => {
      if (active) setResult({
        key,
        bookings,
        vehicles
      });
    });
    return () => {
      active = false;
    };
  }, [token, key]);
  const current = result?.key === key ? result : null;
  const submitCancellation = async () => {
    if (submitting.current || !cancelTarget) return;
    submitting.current = true;
    setPending(true);
    const response = await cancelBooking(cancelTarget.id);
    submitting.current = false;
    setPending(false);
    if (response.success && response.data) {
      setResult(previous => ({
        ...previous,
        bookings: {
          ...previous.bookings,
          data: previous.bookings.data.map(booking => booking.id === response.data.id ? response.data : booking)
        }
      }));
      setCancelTarget(null);
      setNotice({
        type: 'success',
        message: 'Booking cancelled. It remains in your booking history.'
      });
    } else setNotice({
      type: 'error',
      message: response.message || 'We couldn’t cancel this booking. Please try again.'
    });
  };
  return <div className="customer-home-container"><div className="account-heading"><div><p className="eyebrow">Your FleetFlow account</p><h1>Good to see you, {user?.fullName || user?.username || 'there'}.</h1><p>Your bookings, dates, and details. All in view.</p></div><Button onClick={() => onNavigate('browse')}>Find a vehicle <span aria-hidden="true"><ArrowUpRightIcon size={18} weight="regular" aria-hidden="true" /></span></Button></div>{notice && !cancelTarget && <Alert {...notice} />}
    <section className="home-section" aria-labelledby="bookings-title"><div className="section-heading-row"><div><h2 id="bookings-title">My bookings</h2><p>Upcoming plans and your rental history.</p></div><Button variant="outline" size="sm" disabled={!current} onClick={() => setRevision(value => value + 1)}>Refresh</Button></div>{!current ? <p className="state-panel" role="status">Loading your bookings…</p> : !current.bookings.success ? <Alert type="error" title="Booking history unavailable" message={current.bookings.message || 'Please try again.'} /> : !current.bookings.data.length ? <div className="state-panel"><h3>Your next journey starts with a vehicle.</h3><p>You haven’t made a booking yet. Your reservations will appear here.</p><Button onClick={() => onNavigate('browse')}>Browse vehicles</Button></div> : <div className="booking-grid">{current.bookings.data.map(booking => <article className="booking-card" key={booking.id}><div className="booking-card-heading"><span className="booking-reference">#{booking.id.slice(0, 8)}</span><StatusBadge status={booking.status} />{booking.rentalState && <span className="booking-reference">{rentalStateLabel(booking.rentalState)}</span>}</div><h3>{booking.vehicle ? `${booking.vehicle.make} ${booking.vehicle.model}` : 'Vehicle details unavailable'}</h3><p>{booking.vehicle?.hubLocation || 'Location not recorded'}</p><dl className="booking-dates"><div><dt>Pickup</dt><dd>{new Date(booking.startDateTime).toLocaleString('en-GB')}</dd></div><div><dt>Return</dt><dd>{new Date(booking.endDateTime).toLocaleString('en-GB')}</dd></div></dl>{<Button variant="outline" size="sm" onClick={() => onNavigate('rental-record', { rentalId: booking.rentalId, bookingId: booking.id })}>{booking.rentalId ? 'View rental' : 'View booking'}</Button>}<div className="booking-card-bottom"><div><span>Reserved amount</span><strong>{formatLKR(booking.totalCost)}</strong>{booking.finalRentalAmount != null && <><span>Final amount</span><strong>{formatLKR(booking.finalRentalAmount)}</strong></>}</div>{isBookingCancellable(booking.status, booking.rentalState) && <Button variant="outline" size="sm" onClick={() => {
              setNotice(null);
              setCancelTarget(booking);
            }}>Cancel booking</Button>}</div></article>)}</div>}</section>
    <section className="home-section"><div className="section-heading-row"><div><p className="eyebrow">Planning ahead?</p><h2>Available vehicles</h2><p>A selection from the current fleet.</p></div><Button variant="outline" onClick={() => onNavigate('browse')}>View all vehicles</Button></div>{!current ? <p className="state-panel" role="status">Loading vehicles…</p> : !current.vehicles.success ? <Alert type="error" title="Vehicles unavailable" message={current.vehicles.message || 'Please try again.'} /> : current.vehicles.data.length ? <div className="vehicle-grid">{current.vehicles.data.map(vehicle => <VehicleCard key={vehicle.id} vehicle={vehicle} onSelect={selected => onNavigate('vehicle-details', {
          vehicle: selected
        })} />)}</div> : <p className="state-panel">No available vehicles are listed right now. Check the catalogue again later.</p>}</section>
    <Modal isOpen={Boolean(cancelTarget)} onClose={() => {
      if (!pending) setCancelTarget(null);
    }} title="Cancel this booking?" subtitle="This action cannot be undone. Your cancelled booking will remain in your history.">{cancelTarget && <><p className="booking-reference">Booking #{cancelTarget.id.slice(0, 8)}</p>{notice?.type === 'error' && <Alert {...notice} />}<div className="modal-actions-row"><Button variant="outline" disabled={pending} onClick={() => setCancelTarget(null)}>Keep booking</Button><Button variant="danger" isLoading={pending} onClick={submitCancellation}>Cancel booking</Button></div></>}</Modal>
  </div>;
}
