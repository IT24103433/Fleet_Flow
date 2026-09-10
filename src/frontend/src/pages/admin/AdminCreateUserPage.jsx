import React, { useState } from 'react';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import RoleBadge from '../../components/common/RoleBadge';
import { useAuth } from '../../context/AuthContext';
import { createAdminUser } from '../../services/adminUserService';

const ROLE_DESCRIPTIONS = {
  CUSTOMER: 'End-user consumer account with vehicle search, reservation creation, and personal profile access.',
  FLEET_MANAGER: 'Operational staff account with vehicle catalog ingestion, fleet dispatch, and status update rights.',
  MAINTENANCE_STAFF: 'Technical service account with vehicle inspection queue and maintenance log update rights.',
  ADMIN: 'System administrator account with user governance, role assignments, and security audit rights.',
};

const AdminCreateUserPage = ({ onNavigate }) => {
  const { token } = useAuth();
  const [formData, setFormData] = useState({
    fullName: '',
    username: '',
    email: '',
    phoneNumber: '',
    address: '',
    drivingLicenseNumber: '',
    role: 'FLEET_MANAGER',
    initialPassword: '',
    forcePasswordChange: true,
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleInputChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: '' }));
    }
  };

  const handleFormSubmit = async (e) => {
    e.preventDefault();
    setNotice(null);
    const newErrors = {};

    if (!formData.username.trim()) newErrors.username = 'Username is required.';
    if (!formData.email.trim()) newErrors.email = 'Email address is required.';
    if (!formData.initialPassword) newErrors.initialPassword = 'Initial password is required.';
    else if (formData.initialPassword.length < 8) newErrors.initialPassword = 'Password must be at least 8 characters.';

    if (formData.role === 'CUSTOMER') {
      if (!formData.fullName.trim()) newErrors.fullName = 'Full name is required for customer accounts.';
      if (!formData.phoneNumber.trim()) newErrors.phoneNumber = 'Phone number is required for customer accounts.';
      if (!formData.address.trim()) newErrors.address = 'Address is required for customer accounts.';
      if (!formData.drivingLicenseNumber.trim()) newErrors.drivingLicenseNumber = 'Driving license number is required for customer accounts.';
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    setIsLoading(true);

    const payload = {
      fullName: formData.fullName.trim(),
      username: formData.username.trim(),
      email: formData.email.trim(),
      phoneNumber: formData.phoneNumber.trim(),
      address: formData.address.trim(),
      drivingLicenseNumber: formData.drivingLicenseNumber.trim(),
      initialPassword: formData.initialPassword,
      role: formData.role,
    };

    const res = await createAdminUser(payload, token);
    setIsLoading(false);

    if (res.success) {
      setNotice({
        type: 'success',
        title: 'User Provisioned Successfully',
        message: `Account for "${res.data.username}" (${res.data.role}) was created and persisted in IdentityService.`,
      });
      setFormData({
        fullName: '',
        username: '',
        email: '',
        phoneNumber: '',
        address: '',
        drivingLicenseNumber: '',
        role: 'FLEET_MANAGER',
        initialPassword: '',
        forcePasswordChange: true,
      });
      setErrors({});
    } else {
      setNotice({
        type: 'error',
        title: 'Provisioning Failed',
        message: res.message,
      });
      if (res.errors) {
        setErrors(res.errors);
      }
    }
  };

  return (
    <div className="admin-create-user-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
            ← Back to User Directory
          </button>
          <h1 className="admin-page-title">Provision New User Account</h1>
          <p className="admin-page-subtitle">
            Create an operational staff member, administrator, or customer with specific role assignments.
          </p>
        </div>
      </div>

      <div className="create-user-card">
        {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          <div className="form-two-col">
            <InputField
              label="Full Name"
              id="fullName"
              name="fullName"
              value={formData.fullName}
              onChange={handleInputChange}
              error={errors.fullName}
              placeholder={formData.role === 'CUSTOMER' ? 'e.g. John Doe' : 'Optional'}
              required={formData.role === 'CUSTOMER'}
              disabled={isLoading}
            />

            <InputField
              label="Username"
              id="username"
              name="username"
              value={formData.username}
              onChange={handleInputChange}
              error={errors.username}
              placeholder="e.g. manager_rachel or tech_dave"
              required
              disabled={isLoading}
            />
          </div>

          <div className="form-two-col">
            <InputField
              label="Work / Contact Email"
              type="email"
              id="email"
              name="email"
              value={formData.email}
              onChange={handleInputChange}
              error={errors.email}
              placeholder="e.g. rachel@fleetflow.internal"
              required
              disabled={isLoading}
            />

            <InputField
              label="Initial / Temporary Password"
              type="password"
              id="initialPassword"
              name="initialPassword"
              value={formData.initialPassword}
              onChange={handleInputChange}
              error={errors.initialPassword}
              placeholder="Min 8 characters"
              required
              disabled={isLoading}
            />
          </div>

          {/* Role Selector */}
          <div className="form-group">
            <label htmlFor="role" className="form-label">
              Role Assignment <span className="required-indicator">*</span>
            </label>
            <div className="role-options-grid">
              {['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN', 'CUSTOMER'].map((roleKey) => (
                <label
                  key={roleKey}
                  className={`role-option-card ${formData.role === roleKey ? 'selected' : ''}`}
                >
                  <input
                    type="radio"
                    name="role"
                    value={roleKey}
                    checked={formData.role === roleKey}
                    onChange={handleInputChange}
                    className="role-radio-native"
                  />
                  <div className="role-card-inner">
                    <RoleBadge role={roleKey} />
                    <p className="role-card-desc">{ROLE_DESCRIPTIONS[roleKey]}</p>
                  </div>
                </label>
              ))}
            </div>
          </div>

          {/* Conditional Customer Profile Details */}
          {formData.role === 'CUSTOMER' && (
            <div className="customer-fields-section">
              <h4 style={{ marginBottom: '1rem', color: 'var(--text-primary, #fff)', fontSize: '0.95rem' }}>
                Customer Profile Information
              </h4>
              <div className="form-two-col">
                <InputField
                  label="Phone Number"
                  id="phoneNumber"
                  name="phoneNumber"
                  value={formData.phoneNumber}
                  onChange={handleInputChange}
                  error={errors.phoneNumber}
                  placeholder="e.g. +1-555-0199"
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
                  placeholder="e.g. DL-98765432"
                  required
                  disabled={isLoading}
                />
              </div>

              <div className="form-group">
                <InputField
                  label="Residential Address"
                  id="address"
                  name="address"
                  value={formData.address}
                  onChange={handleInputChange}
                  error={errors.address}
                  placeholder="e.g. 742 Evergreen Terrace, Springfield"
                  required
                  disabled={isLoading}
                />
              </div>
            </div>
          )}

          <div className="checkbox-control-box" style={{ marginTop: '0.5rem' }}>
            <input
              type="checkbox"
              id="forcePasswordChange"
              name="forcePasswordChange"
              checked={formData.forcePasswordChange}
              onChange={handleInputChange}
              disabled={isLoading}
            />
            <label htmlFor="forcePasswordChange" className="checkbox-label">
              <strong>Force password change on first login</strong>
              <span className="checkbox-subtext">User must set a private password upon authenticating.</span>
            </label>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('admin-users')} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isLoading}>
              Provision Account
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AdminCreateUserPage;
