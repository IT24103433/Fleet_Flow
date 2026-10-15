import React, { useRef, useState } from 'react';
import InputField from '../InputField';
import AvailabilityInput from './AvailabilityInput';
import Button from '../common/Button';
import { onboardingRequest } from '../../services/onboardingService';

export default function GoogleSignupCompletion({ request, onComplete, onCancel }) {
  const [name, setName] = useState(request.fullName || ''); const [username, setUsername] = useState('');
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const guard = useRef(false);
  const submit = async event => {
    event.preventDefault(); if (guard.current) return; guard.current = true; setBusy(true); setError('');
    try { await onComplete(await onboardingRequest('google/signup/complete', { method: 'POST', anonymous: true, body: { signupId: request.signupId, username, fullName: name } })); }
    catch (e) { setError(e.message); } finally { guard.current = false; setBusy(false); }
  };
  return <div className="auth-card google-completion"><p className="auth-context">Google account confirmed</p><h1 className="auth-title">Make yourself at home.</h1><p className="auth-subtitle">One last step: choose how you’ll appear on FleetFlow.</p><p className="google-verified-email">{request.email}</p>
    <form className="auth-form" onSubmit={submit}><InputField id="google-fullname" name="fullName" label="Full name" value={name} onChange={e => setName(e.target.value)} required disabled={busy} />
      <AvailabilityInput kind="username" id="google-username" name="username" label="Username" placeholder="Choose your username" value={username} onChange={e => setUsername(e.target.value)} required disabled={busy} />
      {error && <p role="alert" className="onboarding-error">{error}</p>}<Button type="submit" isLoading={busy} fullWidth>Create my account</Button><Button variant="outline" onClick={onCancel} disabled={busy}>Use another sign-in method</Button>
    </form><p className="form-help">Your account is created when you finish this step. Driving verification comes before your first booking.</p></div>;
}
