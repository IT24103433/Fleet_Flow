import { useEffect, useState } from 'react';
import { onboardingRequest } from '../../services/onboardingService';

export default function useOnboardingConfig() {
  const [state, setState] = useState({ loading: true, enabled: false, error: '' });
  useEffect(() => {
    const controller = new AbortController();
    onboardingRequest('config', { anonymous: true, signal: controller.signal })
      .then(data => setState({ loading: false, enabled: data?.enabled === true, googleClientId: data?.googleClientId || '', error: '' }))
      .catch(error => { if (error.name !== 'AbortError') setState({ loading: false, enabled: false, error: error.message }); });
    return () => controller.abort();
  }, []);
  return state;
}
