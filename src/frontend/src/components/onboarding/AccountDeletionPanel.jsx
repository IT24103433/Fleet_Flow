import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import Button from '../common/Button';
import useConfirmAction from '../common/useConfirmAction';
import OtpStep from './OtpStep';
import { onboardingRequest } from '../../services/onboardingService';

export default function AccountDeletionPanel() {
  const { logout } = useAuth(); const [challenge, setChallenge] = useState(null); const [error, setError] = useState('');
  const { requestConfirmation, confirmationDialog } = useConfirmAction();
  const request = () => requestConfirmation({ title: 'Request account deletion?', message: 'Resolve future bookings and unfinished rentals first. After email verification, access is revoked and administrators can restore your account for thirty days. Afterwards personal data is anonymised; necessary rental history is retained.', label: 'Request verification code', action: async () => {
    try { setError(''); setChallenge(await onboardingRequest('deletion', { method: 'POST' })); } catch (e) { setError(e.message); }
  } });
  return <section className="onboarding-panel account-deletion-panel"><h2>Delete your customer account</h2><p>Deletion needs an email verification code. It will not cancel or remove your rental obligations.</p>
    {error && <p role="alert" className="onboarding-error">{error}</p>}
    {challenge ? <OtpStep challenge={challenge} onCancel={() => setChallenge(null)} verifyRequest={body => onboardingRequest('deletion/confirm', { method: 'POST', body })} onVerified={() => { logout(); window.location.hash = '#/landing'; }} /> : <Button variant="danger" onClick={request}>Request account deletion</Button>}
    {confirmationDialog}
  </section>;
}
