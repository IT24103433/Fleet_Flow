import OtpStep from '../components/onboarding/OtpStep';
import PasswordRecovery from '../components/onboarding/PasswordRecovery';
import useOnboardingConfig from '../components/onboarding/useOnboardingConfig';
import '../onboarding.css';
import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import { LockKeyIcon } from "@phosphor-icons/react/dist/csr/LockKey";
import AuthLayout from '../components/layout/AuthLayout';
import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import { validateLogin } from '../validation/loginValidation';
import { loginUser } from '../services/authService';
import { useAuth } from '../context/AuthContext';
const STAFF_ALLOWED_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];
const extractRoles = token => {
  try {
    if (!token || typeof token !== 'string') return [];
    const parts = token.split('.');
    if (parts.length < 2) return [];
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
    const payload = JSON.parse(jsonPayload);
    const rawRole = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload['role'] ?? [];
    if (Array.isArray(rawRole)) {
      return rawRole.map(r => String(r).toUpperCase().trim());
    }
    if (typeof rawRole === 'string' && rawRole.trim().length > 0) {
      return [rawRole.toUpperCase().trim()];
    }
    return [];
  } catch {
    return [];
  }
};
const StaffLoginPage = ({
  onNavigateToCustomerLogin,
  onLoginSuccess
}) => {
  const {
    login
  } = useAuth();
  const [challenge, setChallenge] = useState(null);
  const [recovering, setRecovering] = useState(false);
  const onboardingConfig = useOnboardingConfig();
  const [formData, setFormData] = useState({
    usernameOrEmail: '',
    password: ''
  });
  const [errors, setErrors] = useState({});
  const [globalError, setGlobalError] = useState('');
  const [accessDeniedMessage, setAccessDeniedMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const handleInputChange = e => {
    const {
      name,
      value
    } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    if (errors[name]) {
      setErrors(prev => ({
        ...prev,
        [name]: ''
      }));
    }
    if (globalError) setGlobalError('');
    if (accessDeniedMessage) setAccessDeniedMessage('');
  };
  const handleFormSubmit = async e => {
    e.preventDefault();
    setErrors({});
    setGlobalError('');
    setAccessDeniedMessage('');
    const validationErrors = validateLogin(formData);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      const firstErrorKey = Object.keys(validationErrors)[0];
      const element = document.getElementById(firstErrorKey);
      if (element) element.focus();
      return;
    }
    setIsLoading(true);
    const result = await loginUser(formData.usernameOrEmail, formData.password, 'staff');
    setIsLoading(false);
    if (result.success && result.data?.challengeId) { setChallenge(result.data); return; }
    if (result.success) {
      const userRoles = extractRoles(result.data.token);
      const hasStaffPrivilege = userRoles.some(r => STAFF_ALLOWED_ROLES.includes(r));
      if (hasStaffPrivilege) {
        login(result.data.token, result.data.user);
        if (onLoginSuccess) {
          onLoginSuccess(result.data.token, result.data.user);
        }
      } else {
        // Secondary frontend guard (server should have already blocked this)
        setAccessDeniedMessage('Access Denied: Your account does not have staff operational privileges (Fleet Manager, Maintenance Staff, or Administrator required).');
      }
    } else {
      if (result.status === 403) {
        setAccessDeniedMessage(result.message || 'Your account does not have access to the Staff Portal.');
      } else if (result.status === 400 && result.errors) {
        const fieldErrors = {};
        Object.keys(result.errors).forEach(key => {
          const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1);
          fieldErrors[normalizedKey] = Array.isArray(result.errors[key]) ? result.errors[key].join(' ') : result.errors[key];
        });
        setErrors(fieldErrors);
      } else {
        setGlobalError(result.message);
      }
    }
  };
  if (challenge) return <AuthLayout staff><div className="auth-card"><OtpStep challenge={challenge} onCancel={() => setChallenge(null)} onVerified={result => { login(result.token, result.user); onLoginSuccess?.(result.token, result.user); }} /></div></AuthLayout>;
  if (recovering) return <AuthLayout staff><div className="auth-card"><PasswordRecovery onBack={() => setRecovering(false)} /></div></AuthLayout>;
  return <AuthLayout staff>
      <div className="auth-card staff-card">
        <div className="auth-header">
          <div className="staff-portal-badge">
            <LockKeyIcon width="14" height="14" weight="regular" aria-hidden="true" />
            <span>Staff & Management Portal</span>
          </div>
          <h1 className="auth-title">Staff sign in</h1>
          <p className="auth-subtitle">Restricted access for Fleet Managers, Maintenance Staff, and Administrators.</p>
        </div>

        <Alert type="error" message={globalError} />

        {accessDeniedMessage && <div className="access-denied-box">
            <Alert type="warning" title="Access Denied" message={accessDeniedMessage} />
            <div className="denied-actions">
              <Button variant="outline" size="sm" fullWidth onClick={onNavigateToCustomerLogin}>
                Go to Customer Portal Sign In
              </Button>
            </div>
          </div>}

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField label="Staff Username or Work Email" id="usernameOrEmail" name="usernameOrEmail" value={formData.usernameOrEmail} onChange={handleInputChange} error={errors.usernameOrEmail} placeholder="Enter your user name" required disabled={isLoading} />

          <InputField label="Password" type="password" id="password" name="password" value={formData.password} onChange={handleInputChange} error={errors.password} placeholder="Enter your password" required disabled={isLoading} />

          <Button type="submit" variant="primary" size="lg" fullWidth isLoading={isLoading}>
            Sign in
          </Button>

          <div className="staff-auth-notice">
            <p>
              Your administrator creates staff accounts. Contact them if you need access.
            </p>
          </div>

          <div className="auth-footer-links">
            {onboardingConfig.enabled && <button type="button" className="inline-link-btn" onClick={() => setRecovering(true)} disabled={isLoading}>Forgot your password?</button>}
            <button type="button" className="inline-link-btn" onClick={onNavigateToCustomerLogin} disabled={isLoading}>
              <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Return to Customer Portal
            </button>
          </div>
        </form>
      </div>
    </AuthLayout>;
};
export default StaffLoginPage;
