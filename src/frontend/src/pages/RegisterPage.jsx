import React, { useState } from 'react';
import InputField from '../components/InputField';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import { validateRegister } from '../validation/registerValidation';
import { registerUser } from '../services/authService';

const RegisterPage = ({ onNavigateToLogin }) => {
  const [formData, setFormData] = useState({
    fullName: '',
    username: '',
    email: '',
    phoneNumber: '',
    address: '',
    drivingLicenseNumber: '',
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

    const validationErrors = validateRegister(formData);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      const firstErrorKey = Object.keys(validationErrors)[0];
      const element = document.getElementById(firstErrorKey);
      if (element) element.focus();
      return;
    }

    setIsLoading(true);

    const result = await registerUser(formData);

    setIsLoading(false);

    if (result.success) {
      setIsSuccess(true);
      setRegisteredUser({
        fullName: result.data.fullName,
        username: result.data.username,
        email: result.data.email,
        phoneNumber: result.data.phoneNumber,
        address: result.data.address,
        drivingLicenseNumber: result.data.drivingLicenseNumber,
      });
    } else {
      if (result.errors) {
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

  if (isSuccess && registeredUser) {
    return (
      <div className="auth-page-layout">
        <div className="auth-card success-card">
          <div className="success-icon-wrapper">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" className="success-check-icon">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>
          <h2 className="auth-title">Account Created Successfully!</h2>
          <p className="auth-subtitle">Welcome to FleetFlow Mobility, <strong>{registeredUser.fullName || registeredUser.username}</strong>.</p>
          
          <div className="registration-meta-box">
            <div className="meta-row">
              <span className="meta-label">Full Name</span>
              <span className="meta-val">{registeredUser.fullName}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Username</span>
              <span className="meta-val">{registeredUser.username}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Registered Email</span>
              <span className="meta-val">{registeredUser.email}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Phone Number</span>
              <span className="meta-val">{registeredUser.phoneNumber}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Address</span>
              <span className="meta-val">{registeredUser.address}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Driving License</span>
              <span className="meta-val">{registeredUser.drivingLicenseNumber}</span>
            </div>
            <div className="meta-row">
              <span className="meta-label">Assigned Role</span>
              <span className="meta-val role-highlight">Customer</span>
            </div>
          </div>

          <div className="success-action-group">
            <Button
              variant="primary"
              size="lg"
              fullWidth
              onClick={onNavigateToLogin}
            >
              Sign In to Your Account
            </Button>
            <button 
              type="button"
              className="inline-link-btn"
              onClick={() => {
                setIsSuccess(false);
                setRegisteredUser(null);
                setFormData({
                  fullName: '',
                  username: '',
                  email: '',
                  phoneNumber: '',
                  address: '',
                  drivingLicenseNumber: '',
                  password: '',
                  confirmPassword: '',
                });
              }}
            >
              Register Another Customer
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="auth-page-layout">
      <div className="auth-card">
        <div className="auth-header">
          <div className="auth-badge-pill">Customer Registration</div>
          <h2 className="auth-title">Create your FleetFlow customer account</h2>
          <p className="auth-subtitle">Join FleetFlow to browse available vehicles, reserve rides, and manage rentals.</p>
        </div>

        <Alert type="error" message={globalError} />

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField
            label="Full Legal Name"
            id="fullName"
            name="fullName"
            value={formData.fullName}
            onChange={handleInputChange}
            error={errors.fullName}
            placeholder="e.g. Alex Morgan"
            required
            disabled={isLoading}
          />

          <InputField
            label="Username"
            id="username"
            name="username"
            value={formData.username}
            onChange={handleInputChange}
            error={errors.username}
            placeholder="e.g. alex_mobility"
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
            placeholder="e.g. alex@example.com"
            required
            disabled={isLoading}
          />

          <InputField
            label="Phone Number"
            type="tel"
            id="phoneNumber"
            name="phoneNumber"
            value={formData.phoneNumber}
            onChange={handleInputChange}
            error={errors.phoneNumber}
            placeholder="e.g. +1 555 234 5678"
            required
            disabled={isLoading}
          />

          <InputField
            label="Home / Billing Address"
            id="address"
            name="address"
            value={formData.address}
            onChange={handleInputChange}
            error={errors.address}
            placeholder="e.g. 100 Mobility Way, Metro City"
            required
            disabled={isLoading}
          />

          <InputField
            label="Driving License Number"
            id="drivingLicenseNumber"
            name="drivingLicenseNumber"
            value={formData.drivingLicenseNumber}
            onChange={handleInputChange}
            error={errors.drivingLicenseNumber}
            placeholder="e.g. DL-8492048-A"
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
            placeholder="Min 8 chars (letters, digits, special)"
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
            placeholder="Re-enter your password"
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
            Create Customer Account
          </Button>

          <div className="auth-footer-links">
            <p className="auth-switch-text">
              Already have an account?{' '}
              <button
                type="button"
                className="inline-link-btn"
                onClick={onNavigateToLogin}
                disabled={isLoading}
              >
                Sign In
              </button>
            </p>
          </div>
        </form>
      </div>
    </div>
  );
};

export default RegisterPage;
