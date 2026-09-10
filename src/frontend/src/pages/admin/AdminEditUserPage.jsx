import React, { useState } from 'react';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import RoleBadge from '../../components/common/RoleBadge';
import { useAuth } from '../../context/AuthContext';
import { updateAdminUser } from '../../services/adminUserService';

const ROLE_DESCRIPTIONS = {
  CUSTOMER: 'End-user consumer account with vehicle search, reservation creation, and personal profile access.',
  FLEET_MANAGER: 'Operational staff account with vehicle catalog ingestion, fleet dispatch, and status update rights.',
  MAINTENANCE_STAFF: 'Technical service account with vehicle inspection queue and maintenance log update rights.',
  ADMIN: 'System administrator account with user governance, role assignments, and security audit rights.',
};

const AdminEditUserPage = ({ selectedUser, onNavigate, onUserUpdated }) => {
  const { token } = useAuth();

  const [formData, setFormData] = useState({
    username: selectedUser?.username || '',
    email: selectedUser?.email || '',
    fullName: selectedUser?.fullName || '',
    phoneNumber: selectedUser?.phoneNumber || '',
    address: selectedUser?.address || '',
    drivingLicenseNumber: selectedUser?.drivingLicenseNumber || '',
    role: selectedUser?.role || selectedUser?.roles?.[0] || 'CUSTOMER',
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
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

    if (!selectedUser?.id) {
      setNotice({
        type: 'error',
        title: 'User Reference Error',
        message: 'No active user account reference selected for modification.',
      });
      return;
    }

    setIsLoading(true);

    const payload = {
      username: formData.username.trim(),
      email: formData.email.trim(),
      fullName: formData.fullName.trim(),
      phoneNumber: formData.phoneNumber.trim(),
      address: formData.address.trim(),
      drivingLicenseNumber: formData.drivingLicenseNumber.trim(),
      role: formData.role,
    };

    const res = await updateAdminUser(selectedUser.id, payload, token);
    setIsLoading(false);

    if (res.success) {
      setNotice({
        type: 'success',
        title: 'User Account Updated Successfully',
        message: `Account for "${res.data.username}" (${res.data.role}) has been saved to IdentityService.`,
      });
      if (onUserUpdated) {
        onUserUpdated(res.data);
      }
    } else {
      setNotice({
        type: 'error',
        title: res.status === 409 ? 'Duplicate Account Conflict' : 'Update Failed',
        message: res.message || 'An error occurred while updating the user account.',
      });
      if (res.errors) {
        setErrors(res.errors);
      }
    }
  };

  if (!selectedUser) {
    return (
      <div className="admin-create-user-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
              ← Back to User Directory
            </button>
            <h1 className="admin-page-title">Modify User Account</h1>
          </div>
        </div>
        <div className="create-user-card" style={{ maxWidth: '800px', textAlign: 'center', padding: 'var(--space-8)' }}>
          <h3>No User Selected</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '16px' }}>
            Please select a user account from the directory table to modify details.
          </p>
          <Button variant="primary" onClick={() => onNavigate('admin-users')}>
            Return to User Directory
          </Button>
        </div>
      </div>
    );
  }

  const createdStr = selectedUser.createdAt
    ? (selectedUser.createdAt.includes('T') ? selectedUser.createdAt.split('T')[0] : selectedUser.createdAt)
    : '—';

  return (
    <div className="admin-create-user-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
            ← Back to User Directory
          </button>
          <h1 className="admin-page-title">Modify Account: {selectedUser.username}</h1>
          <p className="admin-page-subtitle">
            Update security claims, assigned role privileges, or identity profile attributes.
          </p>
        </div>
      </div>

      <div className="create-user-card">
        {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

        {/* Read-only Identity Metadata strip */}
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          backgroundColor: 'var(--color-surface-hover)',
          padding: 'var(--space-3) var(--space-4)',
          borderRadius: 'var(--radius-sm)',
          marginBottom: 'var(--space-6)',
          border: '1px solid var(--color-border)',
        }}>
          <div>
            <span style={{ fontSize: '11px', color: 'var(--color-text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>User Identifier:</span>
            <span className="font-mono" style={{ fontSize: '12px', marginLeft: '8px', color: 'var(--color-text-primary)' }}>{selectedUser.id}</span>
          </div>
          <div>
            <span style={{ fontSize: '11px', color: 'var(--color-text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Provisioned Date:</span>
            <span style={{ fontSize: '12px', marginLeft: '8px', color: 'var(--color-text-primary)' }}>{createdStr}</span>
          </div>
        </div>

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          <div className="form-two-col">
            <InputField
              label="Username"
              id="username"
              name="username"
              value={formData.username}
              onChange={handleInputChange}
              error={errors.username}
              placeholder="e.g. jdoe"
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
              placeholder="e.g. user@fleetflow.io"
              required
              disabled={isLoading}
            />
          </div>

          <div className="form-two-col">
            <InputField
              label="Full Name"
              id="fullName"
              name="fullName"
              value={formData.fullName}
              onChange={handleInputChange}
              error={errors.fullName}
              placeholder={formData.role === 'CUSTOMER' ? 'e.g. John Doe' : 'Optional for staff'}
              required={formData.role === 'CUSTOMER'}
              disabled={isLoading}
            />

            <InputField
              label="Phone Number"
              id="phoneNumber"
              name="phoneNumber"
              value={formData.phoneNumber}
              onChange={handleInputChange}
              error={errors.phoneNumber}
              placeholder={formData.role === 'CUSTOMER' ? 'e.g. +94-77-123-4567' : 'Optional for staff'}
              required={formData.role === 'CUSTOMER'}
              disabled={isLoading}
            />
          </div>

          <div className="form-two-col">
            <InputField
              label="Physical Address"
              id="address"
              name="address"
              value={formData.address}
              onChange={handleInputChange}
              error={errors.address}
              placeholder={formData.role === 'CUSTOMER' ? 'e.g. 123 Galle Road, Colombo' : 'Optional for staff'}
              required={formData.role === 'CUSTOMER'}
              disabled={isLoading}
            />

            <InputField
              label="Driving License Number"
              id="drivingLicenseNumber"
              name="drivingLicenseNumber"
              value={formData.drivingLicenseNumber}
              onChange={handleInputChange}
              error={errors.drivingLicenseNumber}
              placeholder={formData.role === 'CUSTOMER' ? 'e.g. B-1234567' : 'Optional for staff'}
              required={formData.role === 'CUSTOMER'}
              disabled={isLoading}
            />
          </div>

          {/* Role Selection */}
          <div className="form-group" style={{ marginTop: 'var(--space-2)' }}>
            <label htmlFor="role" className="form-label">
              Assigned Authorization Role <span className="required-indicator">*</span>
            </label>
            <select
              id="role"
              name="role"
              value={formData.role}
              onChange={handleInputChange}
              className="browse-select"
              style={{ width: '100%' }}
              disabled={isLoading}
            >
              <option value="CUSTOMER">Customer (Consumer Mobility User)</option>
              <option value="FLEET_MANAGER">Fleet Manager (Inventory Operations)</option>
              <option value="MAINTENANCE_STAFF">Maintenance Staff (Vehicle Technical Service)</option>
              <option value="ADMIN">System Administrator (Full Governance)</option>
            </select>
          </div>

          {/* Selected Role Context Notice */}
          <div style={{
            backgroundColor: 'var(--color-surface-hover)',
            border: '1px solid var(--color-border)',
            borderRadius: 'var(--radius-sm)',
            padding: 'var(--space-3) var(--space-4)',
            marginBottom: 'var(--space-6)',
            display: 'flex',
            alignItems: 'center',
            gap: 'var(--space-3)',
          }}>
            <RoleBadge role={formData.role} />
            <span style={{ fontSize: '13px', color: 'var(--color-text-secondary)' }}>
              {ROLE_DESCRIPTIONS[formData.role]}
            </span>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('admin-users')} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isLoading}>
              Save User Changes
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AdminEditUserPage;
