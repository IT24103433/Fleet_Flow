import React, { useRef, useState } from 'react';
import InputField from '../InputField';
import Button from '../common/Button';
import OtpStep from './OtpStep';
import { onboardingRequest } from '../../services/onboardingService';

export default function PasswordRecovery({ onBack }) {
  const [email, setEmail] = useState(''); const [challenge, setChallenge] = useState(null);
  const [password, setPassword] = useState(''); const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [done, setDone] = useState(false); const guard = useRef(false);
  const request = async e => {
    e.preventDefault(); if (guard.current) return; guard.current = true; setBusy(true); setError('');
    try { setChallenge(await onboardingRequest('recovery', { method: 'POST', anonymous: true, body: { email } })); }
    catch (err) { setError(err.message); } finally { guard.current = false; setBusy(false); }
  };
  if (done) return <div><h1 className="auth-title">Your password is updated.</h1><p>Previous sessions have been revoked. Sign in with your new password.</p><Button onClick={onBack}>Return to sign in</Button></div>;
  if (challenge) return <div>
    <InputField id="recovery-password" label="New password" type="password" autoComplete="new-password" value={password} onChange={e => setPassword(e.target.value)} />
    <InputField id="recovery-confirmation" label="Confirm new password" type="password" autoComplete="new-password" value={confirmation} onChange={e => setConfirmation(e.target.value)} />
    <p className="form-help">Use at least eight characters with uppercase, lowercase, a number and a symbol.</p>
    <OtpStep challenge={challenge} onCancel={() => setChallenge(null)} verifyRequest={body => {
      if (password !== confirmation) throw new Error('Your passwords do not match.');
      return onboardingRequest('recovery/reset', { method: 'POST', anonymous: true, body: { ...body, newPassword: password } });
    }} onVerified={() => { setPassword(''); setConfirmation(''); setDone(true); }} />
  </div>;
  return <form onSubmit={request}><h1 className="auth-title">Recover your account.</h1><p>If an active account exists, we’ll send a verification code. Your account remains unchanged until verification succeeds.</p>
    <InputField id="recovery-email" label="Account email" type="email" value={email} onChange={e => setEmail(e.target.value)} required disabled={busy} />
    {error && <p role="alert" className="onboarding-error">{error}</p>}
    <Button type="submit" isLoading={busy}>Request verification code</Button><Button variant="outline" onClick={onBack} disabled={busy}>Back to sign in</Button>
  </form>;
}
