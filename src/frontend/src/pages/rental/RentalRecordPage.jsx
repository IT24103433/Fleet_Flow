import { useEffect, useRef, useState } from 'react';
import { KeyIcon } from '@phosphor-icons/react/dist/csr/Key';
import { CheckCircleIcon } from '@phosphor-icons/react/dist/csr/CheckCircle';
import { CarProfileIcon } from '@phosphor-icons/react/dist/csr/CarProfile';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import { rentalClient } from '../../services/rentalService';
import { formatLKR } from '../../utils/currencyUtils';
import { vehiclePhoto } from '../../utils/vehiclePresentation';
import { canStartPickup, pickupErrors } from '../../validation/pickupValidation';
import './Rentals.css';
const uuid = /^[a-f\d]{8}(?:-[a-f\d]{4}){3}-[a-f\d]{12}$/i;
const date = value => new Date(value).toLocaleString('en-GB');
function RecordPhoto({ vehicle }) {
  const source = vehiclePhoto(vehicle);
  const [failedSource, setFailedSource] = useState(null);
  return <div className="rental-record-photo">{source && failedSource !== source ? <img src={source} alt={`${vehicle.make} ${vehicle.model}`} onError={() => setFailedSource(source)} /> : <><CarProfileIcon size={48} weight="regular" aria-hidden="true" /><span>Vehicle photo unavailable</span></>}</div>;
}
export default function RentalRecordPage({ bookingId, rentalId, staff = false, onNavigate }) {
  const [result, setResult] = useState(null);
  const [revision, setRevision] = useState(0);
  const [values, setValues] = useState({ odometer: '', fuelPercentage: '', condition: '' });
  const [errors, setErrors] = useState({});
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);
  const guard = useRef(false);
  const mounted = useRef(false);
  const key = `${bookingId || ''}:${rentalId || ''}:${revision}`;
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      try {
        if (!uuid.test(rentalId || '') && !(staff && uuid.test(bookingId || ''))) throw new Error('This record link is incomplete. Open a record from your rental list.');
        const rental = rentalId ? await rentalClient.record(rentalId, controller.signal) : null;
        const booking = rental?.booking || await rentalClient.booking(bookingId, controller.signal);
        const resolved = rental || (booking.rentalId ? await rentalClient.record(booking.rentalId, controller.signal) : null);
        if (!controller.signal.aborted) { setResult({ key, booking: resolved?.booking || booking, rental: resolved }); setValues({ odometer: String(booking.vehicle?.mileage ?? ''), fuelPercentage: '', condition: '' }); }
      } catch (error) { if (!controller.signal.aborted) setResult({ key, error: error.message }); }
    }
    load();
    return () => controller.abort();
  }, [bookingId, rentalId, staff, key]);
  const current = result?.key === key ? result : null;
  const booking = current?.booking;
  const rental = current?.rental;
  function prepare(event) {
    event.preventDefault();
    const next = pickupErrors(values, booking.vehicle?.mileage);
    setErrors(next);
    if (!Object.keys(next).length) { setNotice(null); setReview(true); }
  }
  async function submit() {
    if (guard.current) return;
    guard.current = true; setBusy(true);
    try {
      const response = await rentalClient.pickup(booking.id, { odometer: Number(values.odometer), fuelPercentage: Number(values.fuelPercentage), condition: values.condition.trim() });
      if (mounted.current) { setReview(false); setResult({ key, booking: response.booking, rental: response }); setNotice({ type: 'success', message: 'Pickup confirmed. The rental is active and the vehicle is in use.' }); }
    } catch (error) {
      if (mounted.current) { setReview(false); setNotice({ type: 'error', message: error.message }); setRevision(r => r + 1); }
    } finally { guard.current = false; if (mounted.current) setBusy(false); }
  }
  const back = () => onNavigate(staff ? 'rental-queue' : 'customer-home');
  return <div className={`rental-workspace ${staff ? '' : 'rental-customer-record'}`}>
    <div className="rental-page-heading"><div><p className="eyebrow">{staff ? 'Vehicle handover' : 'Your rental'}</p><h1>{rental?.state === 'Active' ? (staff ? 'The rental is underway.' : 'Your drive is underway.') : 'The details before the drive.'}</h1><p>Booked dates, vehicle details and a clear pickup record.</p></div><Button variant="outline" onClick={back}>{staff ? 'Rental queue' : 'My bookings'}</Button></div>
    {notice && <Alert {...notice} />}
    {!current ? <p className="state-panel" role="status">Loading rental record…</p> : current.error ? <div className="rental-panel"><Alert type="error" title="Record unavailable" message={current.error} /><Button variant="outline" onClick={() => setRevision(r => r + 1)}>Try again</Button></div> : <div className="rental-record-grid">
      <aside className="rental-panel rental-vehicle-summary"><RecordPhoto key={booking.vehicleId} vehicle={booking.vehicle} /><div className="rental-panel-content"><p className="rental-meta">Booking #{booking.id.slice(0, 8)}</p><h2>{booking.vehicle ? `${booking.vehicle.make} ${booking.vehicle.model}` : 'Vehicle details unavailable'}</h2><p>{booking.vehicle?.licensePlate || 'Plate not recorded'} · {booking.vehicle?.hubLocation || 'Location not recorded'}</p><dl className="rental-details"><div><dt>Booked pickup</dt><dd>{date(booking.startDateTime)}</dd></div><div><dt>Booked return</dt><dd>{date(booking.endDateTime)}</dd></div><div><dt>Booking status</dt><dd>{booking.status}</dd></div><div><dt>Original estimate</dt><dd>{formatLKR(booking.totalCost)}</dd></div>{staff && <div><dt>Customer reference</dt><dd>#{booking.customerId.slice(0, 8)}</dd></div>}</dl><Button variant="outline" size="sm" onClick={() => onNavigate(staff ? 'staff-vehicle-details' : 'vehicle-details', { vehicleId: booking.vehicleId })}>Vehicle details</Button></div></aside>
      <section className="rental-panel rental-panel-content">
        {rental ? <><div className={`rental-state-banner ${rental.state === 'Active' ? 'is-active' : ''}`}><CheckCircleIcon size={30} weight="regular" aria-hidden="true" /><div><h2>{rental.state === 'Active' ? 'Pickup confirmed' : 'Pickup awaiting confirmation'}</h2><p>{rental.state === 'Active' ? 'The vehicle remains in use until its return is recorded.' : 'The handover is being reconciled. Refresh this record and wait for confirmation before handing over the keys.'}</p></div></div><dl className="rental-details"><div><dt>{rental.state === 'Active' ? 'Actual pickup' : 'Requested pickup'}</dt><dd>{date(rental.pickupAt)}</dd></div><div><dt>Odometer at pickup</dt><dd>{rental.pickupOdometer.toLocaleString()} km</dd></div><div><dt>Fuel at pickup</dt><dd>{rental.pickupFuelPercentage}%</dd></div>{staff && <div><dt>Recorded by</dt><dd>Staff #{rental.pickedUpByUserId.slice(0, 8)}</dd></div>}<div className="rental-condition"><dt>Vehicle condition</dt><dd>{rental.pickupCondition}</dd></div></dl><p className="rental-meta">Rental #{rental.id.slice(0, 8)} · Times shown in your local timezone</p><Button variant="outline" onClick={() => setRevision(r => r + 1)}>Refresh record</Button></> : staff ? <><div className="rental-form-heading"><KeyIcon size={28} weight="regular" aria-hidden="true" /><div><h2>Record vehicle pickup</h2><p>Inspect the vehicle together and enter the handover details.</p></div></div>{!canStartPickup(booking) ? <div className="state-panel"><p>Pickup requires a confirmed booking, at or after its booked pickup time and before its booked return time.</p><Button variant="outline" onClick={() => setRevision(r => r + 1)}>Refresh eligibility</Button></div> : <form onSubmit={prepare} noValidate><div className="rental-form-row">{[['odometer', 'Odometer (km)', 2147483647], ['fuelPercentage', 'Fuel level (%)', 100]].map(([name, label, max]) => <label className="rental-field" key={name} htmlFor={name}>{label}<input id={name} aria-label={label} type="number" inputMode="numeric" min={name === 'odometer' ? booking.vehicle?.mileage || 0 : 0} max={max} step="1" value={values[name]} onChange={e => setValues(v => ({ ...v, [name]: e.target.value }))} aria-invalid={Boolean(errors[name])} aria-describedby={errors[name] ? `${name}-error` : undefined} required />{errors[name] && <span className="rental-field-error" id={`${name}-error`}>{errors[name]}</span>}</label>)}</div><label className="rental-field" htmlFor="condition">Vehicle condition<textarea id="condition" aria-label="Vehicle condition" rows="5" maxLength="2000" value={values.condition} onChange={e => setValues(v => ({ ...v, condition: e.target.value }))} placeholder="Exterior, interior and any existing damage…" aria-invalid={Boolean(errors.condition)} aria-describedby="condition-help" required /><span id="condition-help" className={errors.condition ? 'rental-field-error' : 'rental-meta'}>{errors.condition || 'Record visible damage and any agreed condition notes.'}</span></label><div className="rental-form-footer"><p>Actual pickup time is recorded by the service when you confirm.</p><Button type="submit" disabled={busy}>Review pickup</Button></div></form>}</> : <p>No pickup record is available for this booking.</p>}
      </section>
    </div>}
    <Modal isOpen={review} onClose={() => { if (!guard.current) setReview(false); }} title="Confirm vehicle pickup" subtitle="The rental will become active and customer cancellation will be disabled."><div className="rental-confirm-icon"><KeyIcon size={32} weight="regular" aria-hidden="true" /></div><p>Confirm that the odometer is <strong>{Number(values.odometer).toLocaleString()} km</strong>, fuel is <strong>{values.fuelPercentage}%</strong>, and the condition notes match your inspection.</p><p className="rental-confirm-condition">{values.condition}</p><p>Hand over the keys only after pickup is confirmed.</p><div className="modal-actions-row"><Button variant="outline" data-autofocus disabled={busy} onClick={() => setReview(false)}>Back to details</Button><Button isLoading={busy} onClick={submit}>Confirm pickup</Button></div></Modal>
  </div>;
}
