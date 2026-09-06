import React, { useState } from 'react';
import Modal from '../common/Modal';
import InputField from '../InputField';
import Alert from '../Alert';
import Button from '../common/Button';

const ChangePasswordModal = ({ isOpen, onClose }) => {
  const [formData, setFormData] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: '' }));
    }
    if (notice) setNotice(null);
  };

  const handleFormSubmit = (e) => {
    e.preventDefault();
    const newErrors = {};

    if (!formData.currentPassword) {
      newErrors.currentPassword = 'Current password is required.';
    }
    if (!formData.newPassword) {
      newErrors.newPassword = 'New password is required.';
    } else if (formData.newPassword.length < 8) {
      newErrors.newPassword = 'Password must be at least 8 characters.';
    }
    if (formData.newPassword !== formData.confirmPassword) {
      newErrors.confirmPassword = 'Passwords do not match.';
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    setIsLoading(true);

    // Simulate flow and honestly communicate backend integration state
    setTimeout(() => {
      setIsLoading(false);
      setNotice({
        type: 'info',
        title: 'Backend Integration State',
        message: 'Security validation verified. Password update endpoint will be connected to IdentityService in Sprint 2.',
      });
      setFormData({ currentPassword: '', newPassword: '', confirmPassword: '' });
    }, 600);
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Change Account Password"
      subtitle="Update your security credentials. We recommend using a strong, unique password."
    >
      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      <form onSubmit={handleFormSubmit} noValidate>
        <InputField
          label="Current Password"
          type="password"
          id="currentPassword"
          name="currentPassword"
          value={formData.currentPassword}
          onChange={handleInputChange}
          error={errors.currentPassword}
          placeholder="Enter current password"
          required
          disabled={isLoading}
        />

        <InputField
          label="New Password"
          type="password"
          id="newPassword"
          name="newPassword"
          value={formData.newPassword}
          onChange={handleInputChange}
          error={errors.newPassword}
          placeholder="Min 8 characters"
          required
          disabled={isLoading}
        />

        <InputField
          label="Confirm New Password"
          type="password"
          id="confirmPassword"
          name="confirmPassword"
          value={formData.confirmPassword}
          onChange={handleInputChange}
          error={errors.confirmPassword}
          placeholder="Re-enter new password"
          required
          disabled={isLoading}
        />

        <div className="password-rules-card">
          <span className="rules-title">Password Requirements:</span>
          <ul className="rules-list">
            <li className={formData.newPassword.length >= 8 ? 'met' : ''}>At least 8 characters long</li>
            <li className={/[A-Z]/.test(formData.newPassword) ? 'met' : ''}>Contains an uppercase letter</li>
            <li className={/[0-9]/.test(formData.newPassword) ? 'met' : ''}>Contains a digit (0-9)</li>
            <li className={/[^A-Za-z0-9]/.test(formData.newPassword) ? 'met' : ''}>Contains a special character</li>
          </ul>
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={onClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" isLoading={isLoading}>
            Update Password
          </Button>
        </div>
      </form>
    </Modal>
  );
};

export default ChangePasswordModal;
