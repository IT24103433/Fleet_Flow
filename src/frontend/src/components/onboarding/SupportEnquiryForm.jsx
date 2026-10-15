import React, { useRef, useState } from 'react';
import InputField from '../InputField';
import Button from '../common/Button';
import useOnboardingConfig from './useOnboardingConfig';
import '../../onboarding.css';

export default function SupportEnquiryForm() {
  const config = useOnboardingConfig(); const [form, setForm] = useState({ name: '', email: '', topic: 'Booking', message: '', website: '' });
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [result, setResult] = useState(null); const guard = useRef(false); const requestId = useRef(null);
  const submit = async e => {
    e.preventDefault(); if (guard.current) return; guard.current = true; setBusy(true); setError(''); requestId.current ||= crypto.randomUUID();
    try {
      const root = import.meta.env.VITE_NOTIFICATION_API_URL;
      if (!root) throw new Error('Enquiries are temporarily unavailable.');
      const response = await fetch(`${root}/api/support`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...form, requestId: requestId.current }), signal: AbortSignal.timeout(15000) });
      const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
      if (!response.ok) { if (response.status === 400) requestId.current = null; throw new Error(data?.message || (response.status === 429 ? 'Please wait before sending another enquiry.' : 'Your enquiry could not be sent. Try again.')); }
      setResult(data); setForm({ name: '', email: '', topic: 'Booking', message: '', website: '' }); requestId.current = null;
    } catch (err) { setError(err.message); } finally { guard.current = false; setBusy(false); }
  };
  if (!config.enabled) return null;
  const change = e => { setForm(p => ({ ...p, [e.target.name]: e.target.value })); if (!busy) requestId.current = null; };
  return <section className="landing-section support-section" aria-labelledby="support-title"><div><p className="eyebrow">A question about your next drive?</p><h2 id="support-title">Let’s clear it up.</h2><p>Send an enquiry to FleetFlow. Keep passwords, verification codes and identity documents out of your message.</p></div>
    {result ? <div role="status" className="onboarding-panel"><h3>Your enquiry is saved.</h3><p>We can reply to the email address you provided.</p>{result.reference && <p className="form-help">Reference: {result.reference}</p>}<Button variant="outline" onClick={() => setResult(null)}>Send another enquiry</Button></div> : <form onSubmit={submit} className="onboarding-panel">
      <div className="onboarding-fields"><InputField id="support-name" name="name" label="Your name" value={form.name} onChange={change} required disabled={busy} /><InputField id="support-email" name="email" type="email" label="Email address" value={form.email} onChange={change} required disabled={busy} /></div>
      <label htmlFor="support-topic">Topic</label><select id="support-topic" name="topic" value={form.topic} onChange={change} disabled={busy}>{['Booking', 'Driver verification', 'Account', 'Other'].map(topic => <option key={topic}>{topic}</option>)}</select>
      <label htmlFor="support-message">Your enquiry</label><textarea id="support-message" name="message" value={form.message} onChange={change} required minLength={10} maxLength={3000} rows={5} disabled={busy} />
      <div className="support-honeypot" aria-hidden="true"><label htmlFor="support-website">Website</label><input id="support-website" name="website" tabIndex={-1} autoComplete="off" value={form.website} onChange={change} /></div>
      {error && <p role="alert" className="onboarding-error">{error}</p>}<Button type="submit" isLoading={busy}>Send enquiry</Button>
    </form>}
  </section>;
}
