import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import React, { useState } from 'react';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import RoleBadge from '../../components/common/RoleBadge';
import { useAuth } from '../../context/AuthContext';
import { updateAdminUser } from '../../services/adminUserService';
const ROLE_DESCRIPTIONS = {
  CUSTOMER: 'Browse vehicles and manage personal bookings and account details.',
  FLEET_MANAGER: 'Manage vehicle details, pricing, status, and maintenance.',
  MAINTENANCE_STAFF: 'Review maintenance needs, record work, and update repair status.',
  ADMIN: 'Manage accounts, roles, fleet vehicles, maintenance, and reports.'
};
const AdminEditUserPage = ({
  selectedUser,
  onNavigate,
  onUserUpdated
}) => {
  const {
    token
  } = useAuth();
  const [formData, setFormData] = useState({
    username: selectedUser?.username || '',
    email: selectedUser?.email || '',
    fullName: selectedUser?.fullName || '',
    phoneNumber: selectedUser?.phoneNumber || '',
    address: selectedUser?.address || '',
    drivingLicenseNumber: selectedUser?.drivingLicenseNumber || '',
    role: selectedUser?.role || selectedUser?.roles?.[0] || 'CUSTOMER'
  });
  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
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
  };
  const handleFormSubmit = async e => {
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
        message: 'No active user account reference selected for modification.'
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
      role: formData.role
    };
    const res = await updateAdminUser(selectedUser.id, payload, token);
    setIsLoading(false);
    if (res.success) {
      setNotice({
        type: 'success',
        title: 'User Account Updated Successfully',
        message: `Account for "${res.data.username}" (${res.data.role}) has been saved.`
      });
      if (onUserUpdated) {
        onUserUpdated(res.data);
      }
    } else {
      setNotice({
        type: 'error',
        title: res.status === 409 ? 'Duplicate Account Conflict' : 'Update Failed',
        message: res.message || 'An error occurred while updating the user account.'
      });
      if (res.errors) {
        setErrors(res.errors);
      }
    }
  };
  if (!selectedUser) {
    return <div className="admin-create-user-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
              <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to User Directory
            </button>
            <h1 className="admin-page-title">Edit account</h1>
          </div>
        </div>
        <div className="create-user-card view-4ac2926f">
          <h3>No User Selected</h3>
          <p className="view-bd7f6405">
            Please select a user account from the directory table to modify details.
          </p>
          <Button variant="primary" onClick={() => onNavigate('admin-users')}>
            Return to User Directory
          </Button>
        </div>
      </div>;
  }
  const createdStr = selectedUser.createdAt ? selectedUser.createdAt.includes('T') ? selectedUser.createdAt.split('T')[0] : selectedUser.createdAt : '—';
  return <div className="admin-create-user-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('admin-users')}>
            <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to User Directory
          </button>
          <h1 className="admin-page-title">Edit account: {selectedUser.username}</h1>
          <p className="admin-page-subtitle">
            Update account details and review the assigned role.
          </p>
        </div>
      </div>

      <div className="create-user-card">
        {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

        {/* Read-only Identity Metadata strip */}
        <div className="view-d38f1407">
          <div>
            <span className="view-26c5476b">Account reference:</span>
            <span className="font-mono view-60fb5c2e">{selectedUser.id}</span>
          </div>
          <div>
            <span className="view-26c5476b">Created on:</span>
            <span className="view-60fb5c2e">{createdStr}</span>
          </div>
        </div>

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          <div className="form-two-col">
            <InputField label="Username" id="username" name="username" value={formData.username} onChange={handleInputChange} error={errors.username} placeholder="e.g. jdoe" required disabled={isLoading} />

            <InputField label="Email Address" type="email" id="email" name="email" value={formData.email} onChange={handleInputChange} error={errors.email} placeholder="e.g. user@fleetflow.io" required disabled={isLoading} />
          </div>

          <div className="form-two-col">
            <InputField label="Full Name" id="fullName" name="fullName" value={formData.fullName} onChange={handleInputChange} error={errors.fullName} placeholder={formData.role === 'CUSTOMER' ? 'e.g. John Doe' : 'Optional for staff'} required={formData.role === 'CUSTOMER'} disabled={isLoading} />

            <InputField label="Phone Number" id="phoneNumber" name="phoneNumber" value={formData.phoneNumber} onChange={handleInputChange} error={errors.phoneNumber} placeholder={formData.role === 'CUSTOMER' ? 'e.g. +94-77-123-4567' : 'Optional for staff'} required={formData.role === 'CUSTOMER'} disabled={isLoading} />
          </div>

          <div className="form-two-col">
            <InputField label="Physical Address" id="address" name="address" value={formData.address} onChange={handleInputChange} error={errors.address} placeholder={formData.role === 'CUSTOMER' ? 'e.g. 123 Galle Road, Colombo' : 'Optional for staff'} required={formData.role === 'CUSTOMER'} disabled={isLoading} />

            <InputField label="Driving License Number" id="drivingLicenseNumber" name="drivingLicenseNumber" value={formData.drivingLicenseNumber} onChange={handleInputChange} error={errors.drivingLicenseNumber} placeholder={formData.role === 'CUSTOMER' ? 'e.g. B-1234567' : 'Optional for staff'} required={formData.role === 'CUSTOMER'} disabled={isLoading} />
          </div>

          {/* Role Selection */}
          <div className="form-group view-82561d36">
            <label htmlFor="role" className="form-label">
              Assigned role <span className="required-indicator">*</span>
            </label>
            <select id="role" name="role" value={formData.role} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isLoading}>
              <option value="CUSTOMER">Customer</option>
              <option value="FLEET_MANAGER">Fleet Manager</option>
              <option value="MAINTENANCE_STAFF">Maintenance Staff</option>
              <option value="ADMIN">Administrator</option>
            </select>
          </div>

          {/* Selected Role Context Notice */}
          <div className="view-7d06a23b">
            <RoleBadge role={formData.role} />
            <span className="view-26c38885">
              {ROLE_DESCRIPTIONS[formData.role]}
            </span>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('admin-users')} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isLoading}>
              Save account changes
            </Button>
          </div>
        </form>
      </div>
    </div>;
};
export default AdminEditUserPage;
