import React, { useEffect, useMemo, useState } from 'react';
import InputField from '../InputField';
import { onboardingRequest } from '../../services/onboardingService';
import { CheckCircleIcon } from '@phosphor-icons/react/dist/csr/CheckCircle';
import { XCircleIcon } from '@phosphor-icons/react/dist/csr/XCircle';
import { CircleNotchIcon } from '@phosphor-icons/react/dist/csr/CircleNotch';

export default function AvailabilityInput({ kind, value, ...props }) {
  const [status, setStatus] = useState({ value: '', phase: '', message: '' });
  const requestKey = useMemo(() => ({ kind, value }), [kind, value]);
  const valid = kind === 'username' ? /^[a-zA-Z0-9_-]{3,50}$/.test(value) : /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
  useEffect(() => {
    const valid = kind === 'username' ? /^[a-zA-Z0-9_-]{3,50}$/.test(value) : /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    if (!valid) return;
    const controller = new AbortController(); let active = true;
    const timer = setTimeout(() => {
      setStatus({ value, key: requestKey, phase: 'checking', message: 'Checking availability…' });
      onboardingRequest(`availability?kind=${kind}&value=${encodeURIComponent(value)}`, { anonymous: true, signal: controller.signal })
        .then(data => { if (active) setStatus({ value, key: requestKey, phase: data.available ? 'available' : 'unavailable', message: data.available ? 'Available' : `This ${kind === 'username' ? 'username' : 'email address'} is not available.` }); })
        .catch(error => { if (active && error.name !== 'AbortError') setStatus({ value, key: requestKey, phase: 'error', message: 'Could not check right now. We’ll check again when you continue.' }); });
    }, 400);
    return () => { active = false; clearTimeout(timer); controller.abort(); };
  }, [kind, value, requestKey]);
  const current = valid ? status.key === requestKey ? status : { phase: 'checking', message: 'Checking availability…' } : { phase: '', message: value && kind === 'username' ? 'Use 3–50 letters, numbers, underscores or hyphens.' : '' };
  const Icon = current.phase === 'available' ? CheckCircleIcon : current.phase === 'unavailable' ? XCircleIcon : CircleNotchIcon;
  return <div><InputField {...props} value={value} descriptionId={`${props.id}-availability`} /><p id={`${props.id}-availability`} className={`availability-feedback ${current.phase}`} aria-live="polite">{current.phase && <Icon size={16} weight="regular" aria-hidden="true" />}{current.message}</p></div>;
}
