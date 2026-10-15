import React, { useEffect, useRef, useState } from 'react';
import { onboardingRequest } from '../../services/onboardingService';

let loading;
function loadGoogle() {
  if (window.google?.accounts?.id) return Promise.resolve();
  if (!loading) loading = new Promise((resolve, reject) => {
    const script = document.createElement('script'); script.src = 'https://accounts.google.com/gsi/client'; script.async = true;
    script.onload = resolve; script.onerror = () => { loading = null; reject(new Error('Google sign-in could not load. Use email sign-in.')); }; document.head.appendChild(script);
  });
  return loading;
}
export default function GoogleButton({ clientId, username, fullName, onComplete, link = false, security = false, password, recoveryCode }) {
  const container = useRef(null); const current = useRef(null); const [error, setError] = useState(''); const [attempt, setAttempt] = useState(0);
  useEffect(() => { current.current = { username, fullName, onComplete, password, recoveryCode }; }, [username, fullName, onComplete, password, recoveryCode]);
  useEffect(() => {
    if (!clientId) return;
    let active = true;
    const prefix = security ? 'google/security' : link ? 'google/link' : 'google';
    Promise.all([loadGoogle(), onboardingRequest(`${prefix}/begin`, { method: 'POST', anonymous: !link && !security })]).then(([, request]) => {
      if (!active) return;
      window.google.accounts.id.initialize({ client_id: clientId, nonce: request.nonce, auto_select: false, callback: async response => {
        if (!active) return;
        try {
          const result = await onboardingRequest(`${prefix}/complete`, { method: 'POST', anonymous: !link && !security, body: { challengeId: request.challengeId, credential: response.credential, username: current.current.username, fullName: current.current.fullName, password: current.current.password, recoveryCode: current.current.recoveryCode } });
          if (active) current.current.onComplete(result);
        } catch (e) { if (active) setError(e.message); }
      } });
      container.current.replaceChildren();
      window.google.accounts.id.renderButton(container.current, { theme: 'outline', size: 'large', text: 'continue_with', shape: 'rectangular', width: Math.min(400, container.current.clientWidth || 320) });
    }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [clientId, link, security, attempt]);
  if (!clientId) return null;
  return <div className="google-auth"><div ref={container} />{error && <><p role="alert" className="onboarding-error">{error}</p><button type="button" className="inline-link-btn" onClick={() => { setError(''); setAttempt(value => value + 1); }}>Restart Google sign-in</button></>}</div>;
}
