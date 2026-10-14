import { useEffect, useRef, useState } from 'react';
import { CheckCircleIcon } from '@phosphor-icons/react/dist/csr/CheckCircle';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import Alert from '../../components/Alert';
import { formatLKR } from '../../utils/currencyUtils';
import { returnErrors } from '../../validation/returnValidation';
import { rentalClient } from '../../services/rentalService';

export default function RentalReturnForm({ rental, onComplete, onUncertain }) {
  const minimum = Math.max(rental.pickupOdometer, rental.booking.vehicle?.mileage || 0);
  const [values, setValues] = useState({ odometer: String(minimum), fuelPercentage: '', condition: '', extraCharges: '0', extraChargeReason: '' });
  const [errors, setErrors] = useState({});
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const guard = useRef(false);
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const final = Number(rental.booking.totalCost) + Number(values.extraCharges || 0);
  function prepare(event) {
    event.preventDefault(); const next = returnErrors(values, minimum); setErrors(next);
    if (!Object.keys(next).length) { setError(''); setReview(true); }
  }
  async function submit() {
    if (guard.current) return;
    guard.current = true; setBusy(true);
    try {
      const result = await rentalClient.complete(rental.id, { odometer: Number(values.odometer), fuelPercentage: Number(values.fuelPercentage),
        condition: values.condition.trim(), extraCharges: Number(values.extraCharges), extraChargeReason: values.extraChargeReason.trim() || null });
      if (mounted.current) { setReview(false); onComplete(result); }
    } catch (failure) {
      if (mounted.current) { setReview(false); setError(failure.message); if (failure.status === 503 || failure.status === 409) onUncertain(); }
    } finally { guard.current = false; if (mounted.current) setBusy(false); }
  }
  const update = (name, value) => setValues(v => ({ ...v, [name]: value }));
  return <div className="rental-return-form"><div className="rental-form-heading"><CheckCircleIcon size={26} weight="regular" aria-hidden="true" /><div><h2>Record vehicle return</h2><p>Check the vehicle and record any additional charges with a reason.</p></div></div>
    {error && <Alert type="error" message={error} />}
    <form onSubmit={prepare} noValidate>
      <div className="rental-form-row">{[['odometer', 'Return odometer (km)', minimum, 2147483647], ['fuelPercentage', 'Return fuel level (%)', 0, 100]].map(([name, label, min, max]) => <label className="rental-field" key={name}>{label}<input id={`return-${name}`} aria-label={label} type="number" min={min} max={max} step="1" value={values[name]} onChange={e => update(name, e.target.value)} aria-invalid={Boolean(errors[name])} aria-describedby={errors[name] ? `return-${name}-error` : undefined} required />{errors[name] && <span className="rental-field-error" id={`return-${name}-error`}>{errors[name]}</span>}</label>)}</div>
      <label className="rental-field">Condition on return<textarea id="return-condition" aria-label="Condition on return" rows="3" maxLength="2000" value={values.condition} onChange={e => update('condition', e.target.value)} aria-invalid={Boolean(errors.condition)} aria-describedby="return-condition-help" required /><span id="return-condition-help" className={errors.condition ? 'rental-field-error' : 'rental-meta'}>{errors.condition || 'Record the exterior, cabin and any newly observed damage.'}</span></label>
      <div className="rental-return-cost"><div><span>Original reserved amount</span><strong>{formatLKR(rental.booking.totalCost)}</strong></div><div><span>Proposed final amount</span><strong>{formatLKR(final)}</strong></div></div>
      <label className="rental-field">Additional charges (LKR)<input id="return-charges" aria-label="Additional charges (LKR)" type="number" min="0" max="1000000000" step="0.01" value={values.extraCharges} onChange={e => update('extraCharges', e.target.value)} aria-invalid={Boolean(errors.extraCharges)} aria-describedby="return-charges-help" /><span id="return-charges-help" className={errors.extraCharges ? 'rental-field-error' : 'rental-meta'}>{errors.extraCharges || 'Enter zero if no additional charges apply. No late fee is added automatically.'}</span></label>
      <label className="rental-field">Reason for additional charges<textarea id="return-reason" aria-label="Reason for additional charges" rows="2" maxLength="500" value={values.extraChargeReason} onChange={e => update('extraChargeReason', e.target.value)} required={Number(values.extraCharges) > 0} aria-invalid={Boolean(errors.extraChargeReason)} aria-describedby="return-reason-help" /><span id="return-reason-help" className={errors.extraChargeReason ? 'rental-field-error' : 'rental-meta'}>{errors.extraChargeReason || 'Required when additional charges are greater than zero.'}</span></label>
      <div className="rental-form-footer"><p>The service records actual return time. The original estimate stays in the booking history.</p><Button type="submit" disabled={busy}>Review return</Button></div>
    </form>
    <Modal isOpen={review} onClose={() => { if (!guard.current) setReview(false); }} title="Complete this rental?" subtitle="Return details and the final amount will be recorded. This handover cannot be submitted again."><div className="rental-confirm-icon"><CheckCircleIcon size={32} weight="regular" aria-hidden="true" /></div><p>Final amount: <strong>{formatLKR(final)}</strong>. Confirm the vehicle has been returned and inspected.</p><p>{Number(values.odometer).toLocaleString()} km · {values.fuelPercentage}% fuel</p>{Number(values.extraCharges) > 0 && <p className="rental-confirm-condition">{values.extraChargeReason}</p>}<div className="modal-actions-row"><Button variant="outline" data-autofocus disabled={busy} onClick={() => setReview(false)}>Back to details</Button><Button isLoading={busy} onClick={submit}>Confirm return</Button></div></Modal>
  </div>;
}
