import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import { validateRegister } from '../validation/registerValidation';
import { registerUser } from '../services/authService';

const RegisterPage = ({ onNavigateToLogin }) => {
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [errors, setErrors] = useState({});
  const [globalError, setGlobalError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);
  const [registeredUser, setRegisteredUser] = useState(null);

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
    const validationErrors = validateRegister(formData);
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
    const result = await registerUser(formData.username, formData.email, formData.password);

    setIsLoading(false);

    if (result.success) {
      setIsSuccess(true);
      setRegisteredUser({
        username: result.data.username,
        email: result.data.email,
      });
    } else {
      if (result.errors) {
        // Map backend validation errors (e.g. "Email", "Password") case-insensitively
        const fieldErrors = {};
        Object.keys(result.errors).forEach((key) => {
          const normalizedKey = key.toLowerCase();
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

  if (isSuccess && registeredUser) {
    return (
      <div className="register-card success-card">
        <div className="success-icon-container">
          <svg className="success-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
        </div>
        <h2 className="success-title">Registration Successful!</h2>
        <div className="success-details">
          <p>Welcome, <strong>{registeredUser.username}</strong>.</p>
          <p>Your account has been registered with <strong>{registeredUser.email}</strong>.</p>
        </div>
        <div className="success-actions">
          <p className="success-note">
            Authentication services are currently being integrated. You will be able to log in to the FleetFlow management portal shortly.
          </p>
          <button 
            className="btn btn-primary"
            onClick={() => {
              setIsSuccess(false);
              setRegisteredUser(null);
              setFormData({ username: '', email: '', password: '', confirmPassword: '' });
            }}
          >
            Register Another User
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="register-card">
      <div className="register-header">
        <h2 className="register-title">Create Account</h2>
        <p className="register-subtitle">Sign up to get started with FleetFlow</p>
      </div>

      <Alert type="error" message={globalError} />

      <form onSubmit={handleFormSubmit} noValidate>
        <InputField
          label="Username"
          id="username"
          name="username"
          value={formData.username}
          onChange={handleInputChange}
          error={errors.username}
          placeholder="e.g. john_doe"
          required
          disabled={isLoading}
        />

        <InputField
          label="Email Address"
          type="email"
          id="email"
          name="email"
          value={formData.email}
          onChange={handleInputChange}
          error={errors.email}
          placeholder="e.g. john@example.com"
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
          placeholder="Min 8 chars (A-Z, a-z, 0-9, special)"
          required
          disabled={isLoading}
        />

        <InputField
          label="Confirm Password"
          type="password"
          id="confirmPassword"
          name="confirmPassword"
          value={formData.confirmPassword}
          onChange={handleInputChange}
          error={errors.confirmPassword}
          placeholder="Repeat password"
          required
          disabled={isLoading}
        />

        <button
          type="submit"
          className="btn btn-primary btn-block"
          disabled={isLoading}
        >
          {isLoading ? 'Creating Account...' : 'Register'}
        </button>
        <div className="auth-switch-link">
          Already have an account?{' '}
          <button
            type="button"
            className="link-btn"
            onClick={onNavigateToLogin}
            disabled={isLoading}
          >
            Log In
          </button>
        </div>
      </form>
    </div>
  );
};

export default RegisterPage;
