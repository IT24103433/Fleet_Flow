import React, { useRef, useState } from 'react';
import AuthLayout from '../../components/layout/AuthLayout';
import InputField from '../../components/InputField';
import Button from '../../components/common/Button';
import AvailabilityInput from '../../components/onboarding/AvailabilityInput';
import OtpStep from '../../components/onboarding/OtpStep';
import GoogleButton from '../../components/onboarding/GoogleButton';
import GoogleSignupCompletion from '../../components/onboarding/GoogleSignupCompletion';
import { onboardingRequest } from '../../services/onboardingService';
import { useAuth } from '../../context/AuthContext';
import '../../onboarding.css';

export default function VerifiedRegisterPage({ config, onNavigateToLogin }) {
  const [form, setForm] = useState({ fullName: '', username: '', email: '', password: '', confirmPassword: '' });
  const [challenge, setChallenge] = useState(null); const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const guard = useRef(false);
  const [googleProfile, setGoogleProfile] = useState(null);
  const { login } = useAuth();
  const complete = result => { if (result.authenticationStatus === 'ProfileRequired') setGoogleProfile(result); else if (result.challengeId) { setGoogleProfile(null); setChallenge(result); } else if (result.token) { login(result.token, result.user); window.location.hash = '#/customer-profile'; } };
  const change = e => setForm(prev => ({ ...prev, [e.target.name]: e.target.value }));
  const submit = async e => {
    e.preventDefault(); if (guard.current) return;
    if (form.password !== form.confirmPassword) { setError('The passwords do not match.'); return; }
    guard.current = true; setBusy(true); setError('');
    try { complete(await onboardingRequest('signup', { method: 'POST', body: form, anonymous: true })); }
    catch (err) { setError(err.message); } finally { guard.current = false; setBusy(false); }
  };
  if (googleProfile) return <AuthLayout><GoogleSignupCompletion request={googleProfile} onComplete={complete} onCancel={() => setGoogleProfile(null)} /></AuthLayout>;
  return <AuthLayout><div className="auth-card">{challenge ? <OtpStep challenge={challenge} onVerified={complete} onCancel={() => setChallenge(null)} /> : <>
    <h1 className="auth-title">Start your next journey.</h1><p className="auth-subtitle">Create your account now. Add driving documents when you’re ready to book.</p>
    {config.googleClientId && <div className="auth-provider-section"><GoogleButton clientId={config.googleClientId} onComplete={complete} /><p className="auth-divider"><span>or sign up with email</span></p></div>}
    <form onSubmit={submit} className="auth-form">
      <InputField id="fullName" name="fullName" label="Full name" value={form.fullName} onChange={change} required disabled={busy} />
      <AvailabilityInput kind="username" id="username" name="username" label="Username" value={form.username} onChange={change} placeholder="Choose your username" required disabled={busy} />
      <AvailabilityInput kind="email" id="email" name="email" label="Email address" type="email" value={form.email} onChange={change} required disabled={busy} />
      <InputField id="password" name="password" label="Password" type="password" value={form.password} onChange={change} autoComplete="new-password" required disabled={busy} />
      <p className="form-help">Use at least eight characters with uppercase, lowercase, a number and a symbol.</p>
      <InputField id="confirmPassword" name="confirmPassword" label="Confirm password" type="password" value={form.confirmPassword} onChange={change} autoComplete="new-password" required disabled={busy} />
      {error && <p role="alert" className="onboarding-error">{error}</p>}
      <Button type="submit" fullWidth isLoading={busy}>Verify email and create account</Button>
    </form>
    <p className="auth-switch-text">Already have an account? <button type="button" className="inline-link-btn" onClick={onNavigateToLogin}>Sign in</button></p>
  </>}</div></AuthLayout>;
}
