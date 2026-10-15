import React, { useState, useRef, useEffect } from 'react';
import { OTPInput } from 'input-otp';
import Button from '../common/Button';
import { onboardingRequest } from '../../services/onboardingService';

// Composition adapted from the MIT shadcn Input OTP pattern listed on 21st.dev.
export default function OtpStep({ challenge, onVerified, onCancel, verifyRequest }) {
  const [request, setRequest] = useState(challenge);
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [seconds, setSeconds] = useState(60);
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const submitting = useRef(false);
  useEffect(() => { const timer = setInterval(() => setSeconds(s => Math.max(0, s - 1)), 1000); return () => clearInterval(timer); }, []);
  const verify = async event => {
    event.preventDefault(); if (submitting.current || (useRecoveryCode ? !/^[A-F0-9]{16}$/i.test(code.replace(/[\s-]/g, '')) : code.length !== 6)) return;
    submitting.current = true; setBusy(true); setError('');
    try {
      const body = { challengeId: request.challengeId, code, useRecoveryCode };
      const result = verifyRequest ? await verifyRequest(body) : await onboardingRequest('verify', { method: 'POST', anonymous: true, body });
      if (result.challengeId) { setRequest(result); setCode(''); setSeconds(60); }
      else await onVerified(result);
    } catch (e) { setError(e.message); }
    finally { submitting.current = false; setBusy(false); }
  };
  const resend = async () => {
    if (submitting.current || seconds) return; submitting.current = true; setBusy(true); setError('');
    try { setRequest(await onboardingRequest(`resend/${request.challengeId}`, { method: 'POST', anonymous: true })); setCode(''); setSeconds(60); }
    catch (e) { setError(e.message); } finally { submitting.current = false; setBusy(false); }
  };
  return <form onSubmit={verify} className="verification-code-form">
    <h1 className="auth-title">Check your email.</h1>
    <p className="auth-subtitle">Enter the six-digit FleetFlow code. Use the latest code if you requested another.</p>
    <label htmlFor="verification-code">{useRecoveryCode ? 'One-time recovery code' : 'Verification code'}</label>
    {useRecoveryCode ? <input id="verification-code" value={code} onChange={e => setCode(e.target.value)} autoComplete="off" disabled={busy} maxLength={24} /> : <OTPInput id="verification-code" maxLength={6} value={code} onChange={setCode} inputMode="numeric" pattern="^[0-9]*$" autoComplete="one-time-code" disabled={busy}
      render={({ slots }) => <div className="otp-slots">{slots.map((slot, i) => <div key={i} className={`otp-slot ${slot.isActive ? 'active' : ''}`} aria-hidden="true">{slot.char || (slot.hasFakeCaret ? <span className="otp-caret" /> : '')}</div>)}</div>} />}
    {error && <p role="alert" className="onboarding-error">{error}</p>}
    <Button type="submit" fullWidth isLoading={busy} disabled={useRecoveryCode ? !/^[A-F0-9]{16}$/i.test(code.replace(/[\s-]/g, '')) : code.length !== 6}>Verify and continue</Button>
    {request.recoveryCodeAllowed && <Button variant="outline" disabled={busy} onClick={() => { setUseRecoveryCode(v => !v); setCode(''); setError(''); }}>{useRecoveryCode ? 'Use email code' : 'Use a recovery code instead'}</Button>}
    <div className="action-row"><Button variant="outline" disabled={busy || seconds > 0} onClick={resend}>{seconds ? `Resend in ${seconds}s` : 'Resend code'}</Button><Button variant="outline" disabled={busy} onClick={onCancel}>Back</Button></div>
    <p className="form-help">Codes expire after ten minutes. Never share your code with anyone.</p>
  </form>;
}
