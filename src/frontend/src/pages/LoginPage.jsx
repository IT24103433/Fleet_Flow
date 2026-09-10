import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import { validateLogin } from '../validation/loginValidation';
import { loginUser } from '../services/authService';
import { useAuth } from '../context/AuthContext';

const LoginPage = ({ onNavigateToRegister, onNavigateToStaffLogin, onLoginSuccess }) => {
  const { login } = useAuth();
  const [formData, setFormData] = useState({
    usernameOrEmail: '',
    password: '',
  });

  const [errors, setErrors] = useState({});
  const [globalError, setGlobalError] = useState('');
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
    if (globalError) {
      setGlobalError('');
    }
  };

  const handleFormSubmit = async (e) => {
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

    if (result.success) {
      login(result.data.token, result.data.user);
      if (onLoginSuccess) {
        onLoginSuccess(result.data.token);
      }
    } else {
      if (result.status === 403) {
        setGlobalError(result.message || 'Your account does not have access to the Customer Portal.');
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
    <div className="auth-page-layout">
      <div className="auth-card">
        <div className="auth-header">
          <div className="auth-badge-pill">Customer Portal</div>
          <h2 className="auth-title">Sign In to FleetFlow</h2>
          <p className="auth-subtitle">Access personal vehicle bookings, reservations, and account details.</p>
        </div>

        <Alert type="error" message={globalError} />

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField
            label="Username or Email"
            id="usernameOrEmail"
            name="usernameOrEmail"
            value={formData.usernameOrEmail}
            onChange={handleInputChange}
            error={errors.usernameOrEmail}
            placeholder="e.g. alex@example.com or alex_drive"
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
            placeholder="Enter your account password"
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
            Sign In
          </Button>

          <div className="auth-footer-links">
            <p className="auth-switch-text">
              Don't have an account?{' '}
              <button
                type="button"
                className="inline-link-btn"
                onClick={onNavigateToRegister}
                disabled={isLoading}
              >
                Create Account
              </button>
            </p>

            {onNavigateToStaffLogin && (
              <div className="staff-portal-switch-divider">
                <span>Internal Staff Member?</span>
                <button
                  type="button"
                  className="staff-switch-link"
                  onClick={onNavigateToStaffLogin}
                  disabled={isLoading}
                >
                  Go to Staff Portal Login →
                </button>
              </div>
            )}
          </div>
        </form>
      </div>
    </div>
  );
};

export default LoginPage;
