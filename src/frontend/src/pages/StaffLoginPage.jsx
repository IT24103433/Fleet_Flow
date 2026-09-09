import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import { validateLogin } from '../validation/loginValidation';
import { loginUser } from '../services/authService';
import { useAuth } from '../context/AuthContext';

const STAFF_ALLOWED_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];

const extractRoles = (token) => {
  try {
    if (!token || typeof token !== 'string') return [];
    const parts = token.split('.');
    if (parts.length < 2) return [];
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    const payload = JSON.parse(jsonPayload);
    const rawRole =
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
      payload['role'] ??
      [];

    if (Array.isArray(rawRole)) {
      return rawRole.map((r) => String(r).toUpperCase().trim());
    }
    if (typeof rawRole === 'string' && rawRole.trim().length > 0) {
      return [rawRole.toUpperCase().trim()];
    }
    return [];
  } catch {
    return [];
  }
};

const StaffLoginPage = ({ onNavigateToCustomerLogin, onLoginSuccess }) => {
  const { login } = useAuth();
  const [formData, setFormData] = useState({
    usernameOrEmail: '',
    password: '',
  });

  const [errors, setErrors] = useState({});
  const [globalError, setGlobalError] = useState('');
  const [accessDeniedMessage, setAccessDeniedMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));

    if (errors[name]) {
      setErrors((prev) => ({
        ...prev,
        [name]: '',
      }));
    }
    if (globalError) setGlobalError('');
    if (accessDeniedMessage) setAccessDeniedMessage('');
  };

  const handleFormSubmit = async (e) => {
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

    if (result.success) {
      const userRoles = extractRoles(result.data.token);
      const hasStaffPrivilege = userRoles.some((r) => STAFF_ALLOWED_ROLES.includes(r));

      if (hasStaffPrivilege) {
        login(result.data.token, result.data.user);
        if (onLoginSuccess) {
          onLoginSuccess(result.data.token);
        }
      } else {
        // Secondary frontend guard (server should have already blocked this)
        setAccessDeniedMessage(
          'Access Denied: Your account does not have staff operational privileges (Fleet Manager, Maintenance Staff, or Administrator required).'
        );
      }
    } else {
      if (result.status === 403) {
        setAccessDeniedMessage(result.message || 'Your account does not have access to the Staff Portal.');
      } else if (result.status === 400 && result.errors) {
        const fieldErrors = {};
        Object.keys(result.errors).forEach((key) => {
          const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1);
          fieldErrors[normalizedKey] = Array.isArray(result.errors[key]) 
            ? result.errors[key].join(' ') 
            : result.errors[key];
        });
        setErrors(fieldErrors);
      } else {
        setGlobalError(result.message);
      }
    }
  };

  return (
    <div className="auth-page-layout staff-auth-theme">
      <div className="auth-card staff-card">
        <div className="auth-header">
          <div className="staff-portal-badge">
            <svg viewBox="0 0 20 20" fill="currentColor" width="14" height="14" aria-hidden="true">
              <path fillRule="evenodd" d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z" clipRule="evenodd" />
            </svg>
            <span>Staff & Management Portal</span>
          </div>
          <h2 className="auth-title">Operations Sign In</h2>
          <p className="auth-subtitle">Restricted access for Fleet Managers, Maintenance Staff, and Administrators.</p>
        </div>

        <Alert type="error" message={globalError} />
        
        {accessDeniedMessage && (
          <div className="access-denied-box">
            <Alert type="warning" title="Access Denied" message={accessDeniedMessage} />
            <div className="denied-actions">
              <Button
                variant="outline"
                size="sm"
                fullWidth
                onClick={onNavigateToCustomerLogin}
              >
                Go to Customer Portal Sign In
              </Button>
            </div>
          </div>
        )}

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField
            label="Staff Username or Work Email"
            id="usernameOrEmail"
            name="usernameOrEmail"
            value={formData.usernameOrEmail}
            onChange={handleInputChange}
            error={errors.usernameOrEmail}
            placeholder="e.g. manager_dan or staff@fleetflow.internal"
            required
            disabled={isLoading}
          />

          <InputField
            label="Password"
            type="password"
            id="password"
            name="password"
            value={formData.password}
            onChange={handleInputChange}
            error={errors.password}
            placeholder="Enter staff security credentials"
            required
            disabled={isLoading}
          />

          <Button
            type="submit"
            variant="primary"
            size="lg"
            fullWidth
            isLoading={isLoading}
          >
            Authenticate & Access Portal
          </Button>

          <div className="staff-auth-notice">
            <p>
              Staff accounts are provisioned by system administration. Public registration is not permitted for staff roles.
            </p>
          </div>

          <div className="auth-footer-links">
            <button
              type="button"
              className="inline-link-btn"
              onClick={onNavigateToCustomerLogin}
              disabled={isLoading}
            >
              ← Return to Customer Portal
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default StaffLoginPage;
