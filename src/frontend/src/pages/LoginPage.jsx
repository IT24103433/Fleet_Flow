import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import { validateLogin } from '../validation/loginValidation';
import { loginUser } from '../services/authService';
import { useAuth } from '../context/AuthContext';

const LoginPage = ({ onNavigateToRegister }) => {
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

    // Clear field-specific error as user types
    if (errors[name]) {
      setErrors((prev) => ({
        ...prev,
        [name]: '',
      }));
    }
    // Clear global error
    if (globalError) {
      setGlobalError('');
    }
  };

  const handleFormSubmit = async (e) => {
    e.preventDefault();
    setErrors({});
    setGlobalError('');

    // Client-side validation
    const validationErrors = validateLogin(formData);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      // Focus first field with error for better accessibility
      const firstErrorKey = Object.keys(validationErrors)[0];
      const element = document.getElementById(firstErrorKey);
      if (element) element.focus();
      return;
    }

    setIsLoading(true);

    // Call service API
    const result = await loginUser(formData.usernameOrEmail, formData.password);

    setIsLoading(false);

    if (result.success) {
      // successful login
      login(result.data.token, result.data.user);
    } else {
      if (result.status === 400 && result.errors) {
        // Map backend validation errors (e.g. "UsernameOrEmail", "Password") case-insensitively
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
    <div className="register-card">
      <div className="register-header">
        <h2 className="register-title">Sign In</h2>
        <p className="register-subtitle">Log in to manage your FleetFlow account</p>
      </div>

      <Alert type="error" message={globalError} />

      <form onSubmit={handleFormSubmit} noValidate>
        <InputField
          label="Username or Email"
          id="usernameOrEmail"
          name="usernameOrEmail"
          value={formData.usernameOrEmail}
          onChange={handleInputChange}
          error={errors.usernameOrEmail}
          placeholder="Enter username or email address"
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
          placeholder="Enter your password"
          required
          disabled={isLoading}
        />

        <button
          type="submit"
          className="btn btn-primary btn-block"
          disabled={isLoading}
        >
          {isLoading ? 'Signing In...' : 'Log In'}
        </button>

        <div className="auth-switch-link">
          Don't have an account?{' '}
          <button
            type="button"
            className="link-btn"
            onClick={onNavigateToRegister}
            disabled={isLoading}
          >
            Register
          </button>
        </div>
      </form>
    </div>
  );
};

export default LoginPage;
