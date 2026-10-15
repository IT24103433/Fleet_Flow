import React, { useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import Button from '../common/Button';
import InputField from '../InputField';
import ChangePasswordModal from '../security/ChangePasswordModal';
import OtpStep from './OtpStep';
import GoogleButton from './GoogleButton';
import { onboardingRequest } from '../../services/onboardingService';

export default function AccountSecurityPanel({ profile, onRefresh, customer = true, googleClientId }) {
  const { login } = useAuth(); const [password, setPassword] = useState(''); const [email, setEmail] = useState('');
  const [challenge, setChallenge] = useState(null); const [codes, setCodes] = useState([]); const [purpose, setPurpose] = useState('');
  const [proof, setProof] = useState(null); const [googleAttempt, setGoogleAttempt] = useState(0); const [passwordModal, setPasswordModal] = useState(false);
  const [newPassword, setNewPassword] = useState(''); const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false); const guard = useRef(false);
  const [linkPassword, setLinkPassword] = useState(''); const [recoveryCode, setRecoveryCode] = useState('');
  const request = async path => {
    if (guard.current) return; guard.current = true; setBusy(true); setError(''); setNotice('');
    try { setChallenge(await onboardingRequest(path, { method: 'POST', body: { password, newEmail: email, reauthenticationId: proof?.reauthenticationId } })); setPurpose(path); setPassword(''); }
    catch (err) { setError(err.message); } finally { setProof(null); setGoogleAttempt(value=>value+1); guard.current = false; setBusy(false); }
  };
  const complete = async result => {
    if (result.session) login(result.session.token, result.session.user);
    setChallenge(null); setEmail(''); setNewPassword(''); setConfirmation(''); setCodes(result.recoveryCodes || []); setNotice('Your account security settings have been updated.'); await onRefresh();
  };
  const authorised = profile.hasLocalPassword ? Boolean(password) : Boolean(proof && new Date(proof.expiresAt) > new Date());
  return <section className="onboarding-panel"><div data-account-heading><h2>Sign-in & security</h2><p>{profile.hasLocalPassword?'Confirm sensitive changes with your current password and an email code.':'You sign in with Google. Confirm that linked Google account before making sensitive changes.'}</p></div>
    {error && <p role="alert" className="onboarding-error">{error}</p>}{notice && <p role="status" className="onboarding-success">{notice}</p>}
    {codes.length > 0 && <div className="recovery-code-panel"><h3>Save your recovery codes now</h3><p>Each code works once. Store them privately; they won’t be displayed again.</p><ul>{codes.map(code => <li key={code}><code>{code}</code></li>)}</ul><Button variant="outline" onClick={() => setCodes([])}>I have saved these codes</Button></div>}
    {challenge ? <>
      {purpose==='password/set'&&<><InputField label="New FleetFlow password" id="set-password" type="password" autoComplete="new-password" value={newPassword} onChange={e=>setNewPassword(e.target.value)}/><InputField label="Confirm new password" id="set-password-confirm" type="password" autoComplete="new-password" value={confirmation} onChange={e=>setConfirmation(e.target.value)}/><p className="form-help">At least eight characters, with uppercase, lowercase, a number and a symbol.</p></>}
      <OtpStep challenge={challenge} onVerified={complete} onCancel={() => setChallenge(null)} verifyRequest={purpose==='password/set'?body=>{if(newPassword!==confirmation)throw new Error('Your passwords do not match.');return onboardingRequest('password/set/confirm',{method:'POST',body:{...body,newPassword}});}:undefined}/>
    </> : <>
      <div className="account-signin-summary"><span>Current email</span><strong>{profile.email}</strong><small>{profile.emailVerificationMethod==='AdminWaived'?'Approved by administrator':profile.emailVerificationMethod==='Unverified'?'Awaiting email verification':'Email verified'}</small>{profile.emailVerificationMethod==='Unverified'&&<Button variant="outline" onClick={()=>request('verify-email')} disabled={busy}>Verify current email</Button>}</div>
      {profile.hasLocalPassword ? <div className="security-confirmation"><InputField id="account-security-password" label="Current FleetFlow password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} disabled={busy} /></div> : <div className="security-confirmation"><h3>Confirm it’s you</h3>{proof?<p role="status" className="onboarding-success">Google account confirmed. Choose your change within five minutes.</p>:googleClientId?<GoogleButton key={googleAttempt} clientId={googleClientId} security onComplete={setProof}/>:<p role="status">Google confirmation is not configured in this environment. Contact your administrator.</p>}</div>}
      <div className="security-setting-row"><div><h3>Change email address</h3><p>Your existing email stays active until verification finishes. We notify it after a successful change.</p><InputField id="account-new-email" label="New email address" type="email" value={email} onChange={e=>setEmail(e.target.value)} disabled={busy}/></div><Button variant="outline" disabled={busy||!authorised||!email} onClick={()=>request('email-change')}>Verify new email</Button></div>
      <div className="security-setting-row"><div><h3>{profile.hasLocalPassword?'Your password':'Add password sign-in'}</h3><p>{profile.hasLocalPassword?'Update your password without changing your Google connection.':'Optional: set a FleetFlow password while keeping Google sign-in. Confirm Google above, then verify your email.'}</p></div><Button variant="outline" disabled={busy||(!profile.hasLocalPassword&&!authorised)} onClick={()=>profile.hasLocalPassword?setPasswordModal(true):request('password/set')}>{profile.hasLocalPassword?'Change password':'Set a password'}</Button></div>
      {(!customer||profile.emailSecondStepEnabled)&&<div className="security-setting-row"><div><h3>Email login verification</h3><p>{profile.emailSecondStepEnabled?'Enabled: your password or Google sign-in is followed by an email code. Recovery codes are single-use.':'Optional extra verification after your staff password.'}</p></div><Button variant="outline" disabled={busy||!authorised} onClick={()=>request(`second-step/${!profile.emailSecondStepEnabled}`)}>{profile.emailSecondStepEnabled?'Disable email login verification':'Enable email login verification'}</Button></div>}
      {customer&&profile.hasLocalPassword&&googleClientId&&<details className="google-link-settings"><summary>Connect Google sign-in</summary><p>Sign in with your current password before connecting a Google account. Existing accounts are never merged just because an email matches.</p><InputField label="Password to link Google" type="password" id="google-link-password" value={linkPassword} onChange={e=>setLinkPassword(e.target.value)}/>{profile.emailSecondStepEnabled&&<InputField label="One-time recovery code" id="google-link-recovery" value={recoveryCode} onChange={e=>setRecoveryCode(e.target.value)}/>}<GoogleButton clientId={googleClientId} link password={linkPassword} recoveryCode={recoveryCode} onComplete={()=>{setLinkPassword('');setRecoveryCode('');setNotice('Google sign-in is connected.');}}/></details>}
    </>}
    <ChangePasswordModal isOpen={passwordModal} onClose={()=>setPasswordModal(false)}/>
  </section>;
}
