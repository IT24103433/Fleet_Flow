import OtpStep from '../components/onboarding/OtpStep';
import '../onboarding.css';
import useOnboardingConfig from '../components/onboarding/useOnboardingConfig';
import GoogleButton from '../components/onboarding/GoogleButton';
import GoogleSignupCompletion from '../components/onboarding/GoogleSignupCompletion';
import PasswordRecovery from '../components/onboarding/PasswordRecovery';
import { ArrowRightIcon } from "@phosphor-icons/react/dist/csr/ArrowRight";
import AuthLayout from '../components/layout/AuthLayout';
import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import { validateLogin } from '../validation/loginValidation';
import { loginUser } from '../services/authService';
import { useAuth } from '../context/AuthContext';
const LoginPage = ({
  onNavigateToRegister,
  onNavigateToStaffLogin,
  onLoginSuccess
}) => {
  const {
    login
  } = useAuth();
  const [challenge, setChallenge] = useState(null);
  const [googleProfile, setGoogleProfile] = useState(null);
  const [recovering, setRecovering] = useState(false);
  const onboardingConfig = useOnboardingConfig();
  const [formData, setFormData] = useState({
    usernameOrEmail: '',
    password: ''
  });
  const [errors, setErrors] = useState({});
  const [globalError, setGlobalError] = useState('');
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
    if (globalError) {
      setGlobalError('');
    }
  };
  const handleFormSubmit = async e => {
    e.preventDefault();
    setErrors({});
    setGlobalError('');
    const validationErrors = validateLogin(formData);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      const firstErrorKey = Object.keys(validationErrors)[0];
      const element = document.getElementById(firstErrorKey);
      if (element) element.focus();
      return;
    }
    setIsLoading(true);
    const result = await loginUser(formData.usernameOrEmail, formData.password, 'customer');
    setIsLoading(false);
    if (result.success && result.data?.challengeId) { setChallenge(result.data); return; }
    if (result.success) {
      login(result.data.token, result.data.user);
      if (onLoginSuccess) {
        onLoginSuccess(result.data.token, result.data.user);
      }
    } else {
      if (result.status === 403) {
        setGlobalError(result.message || 'Your account does not have access to the Customer Portal.');
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
  const googleComplete = result => { if (result.authenticationStatus === 'ProfileRequired') setGoogleProfile(result); else if (result.challengeId) { setGoogleProfile(null); setChallenge(result); } else if (result.token) { login(result.token, result.user); onLoginSuccess?.(result.token, result.user); } };
  if (googleProfile) return <AuthLayout><GoogleSignupCompletion request={googleProfile} onComplete={googleComplete} onCancel={() => setGoogleProfile(null)} /></AuthLayout>;
  if (challenge) return <AuthLayout><div className="auth-card"><OtpStep challenge={challenge} onCancel={() => setChallenge(null)} onVerified={result => { login(result.token, result.user); onLoginSuccess?.(result.token, result.user); }} /></div></AuthLayout>;
  if (recovering) return <AuthLayout><div className="auth-card"><PasswordRecovery onBack={() => setRecovering(false)} /></div></AuthLayout>;
  return <AuthLayout>
      <div className="auth-card">
        <div className="auth-header">
          <div className="auth-badge-pill">Customer Portal</div>
          <h1 className="auth-title">Welcome back.</h1>
          <p className="auth-subtitle">Sign in to book a vehicle and keep your rental details together.</p>
        </div>

        <Alert type="error" message={globalError} />
        {onboardingConfig.enabled && onboardingConfig.googleClientId && <div className="auth-provider-section"><GoogleButton clientId={onboardingConfig.googleClientId} onComplete={googleComplete} /><p className="auth-divider"><span>or sign in with email</span></p></div>}

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField label="Username or Email" id="usernameOrEmail" name="usernameOrEmail" value={formData.usernameOrEmail} onChange={handleInputChange} error={errors.usernameOrEmail} placeholder="Enter your user name" required disabled={isLoading} />

          <InputField label="Password" type="password" id="password" name="password" value={formData.password} onChange={handleInputChange} error={errors.password} placeholder="Enter your account password" required disabled={isLoading} />

          <Button type="submit" variant="primary" size="lg" fullWidth isLoading={isLoading}>
            Sign In
          </Button>

          <div className="auth-footer-links">
            {onboardingConfig.enabled && <button type="button" className="inline-link-btn" onClick={() => setRecovering(true)} disabled={isLoading}>Forgot your password?</button>}
            <p className="auth-switch-text">
              Don't have an account?{' '}
              <button type="button" className="inline-link-btn" onClick={onNavigateToRegister} disabled={isLoading}>
                Create Account
              </button>
            </p>

            {onNavigateToStaffLogin && <div className="staff-portal-switch-divider">
                <span>Internal Staff Member?</span>
                <button type="button" className="staff-switch-link" onClick={onNavigateToStaffLogin} disabled={isLoading}>
                  Go to Staff Portal Login <ArrowRightIcon size={18} weight="regular" aria-hidden="true" />
                </button>
              </div>}
          </div>
        </form>
      </div>
    </AuthLayout>;
};
export default LoginPage;
