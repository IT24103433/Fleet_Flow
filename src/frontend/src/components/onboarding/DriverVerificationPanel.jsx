import React, { useEffect, useRef, useState } from 'react';
import Button from '../common/Button';
import InputField from '../InputField';
import StatusBadge from '../common/StatusBadge';
import CountryPhoneInput from './CountryPhoneInput';
import CountrySelector from './CountrySelector';
import { onboardingRequest } from '../../services/onboardingService';
import { UploadSimpleIcon } from '@phosphor-icons/react/dist/csr/UploadSimple';

const defaults = { legalName: '', phoneNumber: '', address: '', issuingCountry: 'LK', identityType: 'NIC', identityNumber: '', licenceNumber: '', licenceExpiry: null, passportExpiry: null, permitNumber: '', permitExpiry: null, photos: [] };
export default function DriverVerificationPanel() {
  const [form, setForm] = useState(defaults); const [status, setStatus] = useState('Loading');
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false); const [editing, setEditing] = useState(false); const guard = useRef(false);
  useEffect(() => { const controller = new AbortController(); onboardingRequest('driver', { signal: controller.signal }).then(data => { setForm({ ...defaults, ...data.details }); setStatus(data.status); }).catch(e => { if (e.name !== 'AbortError') setError(e.message); }); return () => controller.abort(); }, []);
  const update = e => setForm(f => ({ ...f, [e.target.name]: e.target.value || (e.target.name.endsWith('Expiry') ? null : '') }));
  const run = async action => {
    if (guard.current) return; guard.current = true; setBusy(true); setError(''); setNotice('');
    try { const data = await action(); setForm({ ...defaults, ...data.details }); setStatus(data.status); setEditing(false); }
    catch (e) { setError(e.message); } finally { guard.current = false; setBusy(false); }
  };
  const save = submit => run(async () => { const data = await onboardingRequest(submit ? 'driver/submit' : 'driver/draft', { method: submit ? 'POST' : 'PUT', body: form }); setNotice(submit ? 'Your documents are awaiting administrator review.' : 'Your draft has been saved.'); return data; });
  const upload = (kind, file) => run(async () => {
    if (!file) return { status, details: form };
    await onboardingRequest('driver/draft', { method: 'PUT', body: form });
    const body = new FormData(); body.append('file', file);
    return onboardingRequest(`driver/photos/${kind}`, { method: 'POST', body });
  });
  const locked = busy || status === 'Loading' || (!editing && ['Approved', 'Pending review'].includes(status));
  const kinds = ['DL', form.identityType, ...(form.issuingCountry !== 'LK' ? ['IDP'] : [])];
  const intent = (() => { try { return JSON.parse(sessionStorage.getItem('fleetflow:booking-intent') || 'null'); } catch { return null; } })();
  return <section className="onboarding-panel" aria-labelledby="driver-verification-heading">
    <div className="onboarding-panel-heading"><div><h2 id="driver-verification-heading">Ready to drive.</h2><p>Your driving details are reviewed once, then checked against each new booking.</p></div><StatusBadge status={status} /></div>
    <ol className="verification-progress"><li>Driving details</li><li>Document photographs</li><li>Administrator review</li></ol>
    {error && <p role="alert" className="onboarding-error">{error}</p>}{notice && <p role="status" className="onboarding-success">{notice}</p>}
    {form.reviewReason && <div className="verification-review-note"><strong>Review note</strong><p>{form.reviewReason}</p></div>}
    {status === 'Approved' && <p className="onboarding-success">Your documents are approved. They must remain valid through your selected return date.</p>}
    {intent?.vehicleId && status === 'Approved' && <Button onClick={() => { window.location.hash = `#/vehicle-details?vehicleId=${encodeURIComponent(intent.vehicleId)}`; }}>Return to selected vehicle</Button>}
    {['Approved', 'Pending review'].includes(status) && !editing && <Button variant="outline" onClick={() => setEditing(true)}>Update details and request a new review</Button>}
    {editing && <p className="form-help">Saving changed driving details removes booking approval until the new revision is reviewed.</p>}
    <div className="onboarding-fields">
      <InputField label="Full legal name on your documents" id="legalName" name="legalName" value={form.legalName || ''} onChange={update} disabled={locked} />
      <CountrySelector value={form.issuingCountry} onChange={issuingCountry => setForm(f => ({ ...f, issuingCountry, identityType: issuingCountry === 'LK' ? f.identityType : 'Passport' }))} disabled={locked} />
      <CountryPhoneInput value={form.phoneNumber} onChange={phoneNumber => setForm(f => ({ ...f, phoneNumber }))} disabled={locked} />
      <InputField label="Current address" id="driverAddress" name="address" value={form.address || ''} onChange={update} disabled={locked} />
      <InputField label="Driving licence number" id="licenceNumber" name="licenceNumber" value={form.licenceNumber || ''} onChange={update} disabled={locked} />
      <InputField label="Driving licence expiry" type="date" id="licenceExpiry" name="licenceExpiry" value={form.licenceExpiry || ''} onChange={update} disabled={locked} />
      <div><label htmlFor="identityType">Identity document</label><select id="identityType" name="identityType" value={form.identityType} onChange={update} disabled={locked || form.issuingCountry !== 'LK'}>{form.issuingCountry === 'LK' && <option value="NIC">Sri Lankan NIC</option>}<option value="Passport">Passport</option></select></div>
      <InputField label={form.identityType === 'NIC' ? 'NIC number' : 'Passport number'} id="identityNumber" name="identityNumber" value={form.identityNumber || ''} onChange={update} disabled={locked} />
      {form.identityType === 'Passport' && <InputField label="Passport expiry" type="date" id="passportExpiry" name="passportExpiry" value={form.passportExpiry || ''} onChange={update} disabled={locked} />}
      {form.issuingCountry !== 'LK' && <><InputField label="International Driving Permit number" id="permitNumber" name="permitNumber" value={form.permitNumber || ''} onChange={update} disabled={locked} /><InputField label="International Driving Permit expiry" type="date" id="permitExpiry" name="permitExpiry" value={form.permitExpiry || ''} onChange={update} disabled={locked} /></>}
    </div>
    <p className="form-help">Enter names exactly as shown on your licence and NIC/passport. Name mismatches, unreadable photos and expired documents can cause rejection. Sri Lankan NICs use 9 digits with V/X, or 12 digits. Alternative issued licence formats require manual review.</p>
    <div className="document-upload-grid">{kinds.map(kind => <div className="document-upload" key={kind}>
      <UploadSimpleIcon size={24} weight="regular" aria-hidden="true" /><h3>{kind === 'DL' ? 'Driving licence' : kind === 'IDP' ? 'International Driving Permit' : kind}</h3>
      <p>Readable front/back or relevant page. JPEG, PNG or WebP; up to 5 MB per photo.</p>
      <label htmlFor={`upload-${kind}`} className="inline-link-btn">Choose photograph</label><input id={`upload-${kind}`} type="file" accept="image/jpeg,image/png,image/webp" disabled={locked || form.photos.filter(p => p.kind === kind).length >= 2} onChange={e => { const file = e.target.files?.[0]; if (file) upload(kind, file); e.target.value = ''; }} />
      {form.photos.filter(p => p.kind === kind).map((photo, i) => <div key={photo.id} className="document-file-row"><span>Photograph {i + 1} uploaded</span><Button size="sm" variant="outline" disabled={locked} onClick={() => run(async () => { if (editing) await onboardingRequest('driver/draft', { method: 'PUT', body: form }); return onboardingRequest(`driver/photos/${photo.id}`, { method: 'DELETE' }); })}>Remove</Button></div>)}
    </div>)}</div>
    <div className="action-row"><Button variant="outline" disabled={locked} onClick={() => save(false)}>Save draft</Button><Button disabled={locked} onClick={() => save(true)} isLoading={busy}>Submit for review</Button></div>
  </section>;
}
