import React, { useEffect, useState } from 'react';
import Button from '../../components/common/Button';
import useConfirmAction from '../../components/common/useConfirmAction';
import { onboardingRequest, privatePhoto } from '../../services/onboardingService';
import '../../onboarding.css';

function DocumentPhoto({ photo }) {
  const [url, setUrl] = useState(''); const [failed, setFailed] = useState(false);
  useEffect(() => { const controller = new AbortController(); let objectUrl; let active = true; privatePhoto(photo.id, controller.signal).then(value => { if (!active) { URL.revokeObjectURL(value); return; } objectUrl = value; setUrl(value); }).catch(e => { if (active && e.name !== 'AbortError') setFailed(true); }); return () => { active = false; controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); }; }, [photo.id]);
  return failed ? <p>Document photograph unavailable.</p> : url ? <a href={url} target="_blank" rel="noreferrer"><img src={url} alt={`${photo.kind} submitted document`} /></a> : <p role="status">Loading photograph…</p>;
}
export default function DriverReviewsPage() {
  const [rows, setRows] = useState(null); const [error, setError] = useState(''); const [selected, setSelected] = useState(null); const [reason, setReason] = useState(''); const [aliases, setAliases] = useState('');
  const { requestConfirmation, confirmationDialog } = useConfirmAction();
  const reload = () => onboardingRequest('reviews').then(setRows).catch(e => setError(e.message));
  useEffect(() => { let active = true; onboardingRequest('reviews').then(data => { if (active) setRows(data); }).catch(e => { if (active) setError(e.message); }); return () => { active = false; }; }, []);
  const review = approve => {
    if (!selected) return; if (!approve && !reason.trim()) { setError('Give the customer a reason and correction instructions.'); return; }
    requestConfirmation({ title: approve ? 'Approve these driving documents?' : 'Return this application for correction?', message: approve ? 'Confirm that names, photographs, identifiers and expiry dates match. Duplicate or expired documents cannot be overridden.' : reason, label: approve ? 'Approve documents' : 'Request correction', action: async () => {
      try { await onboardingRequest(`reviews/${selected.id}`, { method: 'POST', body: { approve, reason, nicAliases: aliases.split(',').map(s => s.trim()).filter(Boolean) } }); setSelected(null); setReason(''); setAliases(''); setError(''); await reload(); } catch (e) { setError(e.message); }
    } });
  };
  return <div className="driver-review-workspace"><div className="page-header"><h1>Driver verification</h1><p>Review the evidence. Approve only when the details match.</p></div>{error && <p role="alert" className="onboarding-error">{error}</p>}
    <div className="driver-review-layout"><aside className="onboarding-panel"><h2>Awaiting review</h2>{rows === null ? <p>Loading applications…</p> : rows.length === 0 ? <p>No applications are awaiting review.</p> : rows.map(row => <button type="button" className="driver-review-item" key={row.id} onClick={() => { setSelected(row); setReason(''); setAliases(''); setError(''); }}><strong>{row.details.legalName}</strong><span>{row.details.issuingCountry} · {row.details.identityType}{row.duplicate ? ' · Identifier conflict' : ''}</span></button>)}</aside>
    <section className="onboarding-panel">{selected ? <><h2>{selected.details.legalName}</h2><dl className="review-details"><dt>Current address</dt><dd>{selected.details.address}</dd><dt>Phone</dt><dd>{selected.details.phoneNumber}</dd><dt>Driving licence</dt><dd>{selected.details.licenceNumber} · expires {selected.details.licenceExpiry}</dd><dt>{selected.details.identityType}</dt><dd>{selected.details.identityNumber}{selected.details.passportExpiry ? ` · expires ${selected.details.passportExpiry}` : ''}</dd>{selected.details.permitNumber && <><dt>International Driving Permit</dt><dd>{selected.details.permitNumber} · expires {selected.details.permitExpiry}</dd></>}</dl>
    {selected.duplicate && <p role="alert" className="onboarding-error">An identifier belongs to another approved account. Approval cannot be forced.</p>}
    {selected.details.issuingCountry === 'LK' && !selected.commonSriLicence && <p className="verification-review-note">This licence uses an alternative format. Inspect the issued document carefully before approval.</p>}
    <div className="review-document-gallery">{selected.details.photos.map(photo => <DocumentPhoto key={photo.id} photo={photo} />)}</div>
    <label htmlFor="review-reason">Review note / correction instructions</label><textarea id="review-reason" value={reason} onChange={e => setReason(e.target.value)} maxLength={500} />
    {selected.details.identityType === 'NIC' && <><label htmlFor="nic-aliases">Confirmed old/replacement NIC aliases, if evidenced</label><input id="nic-aliases" value={aliases} onChange={e => setAliases(e.target.value)} placeholder="Separate verified aliases with commas" /></>}
    <div className="action-row"><Button disabled={selected.duplicate} onClick={() => review(true)}>Approve documents</Button><Button variant="outline" onClick={() => review(false)}>Request correction</Button></div>
    </> : <><h2>Choose an application.</h2><p>Document photographs remain private to the customer and authorised administrators.</p></>}</section></div>{confirmationDialog}
  </div>;
}
