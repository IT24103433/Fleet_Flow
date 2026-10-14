import React, { useState, useRef } from 'react';
import Modal from '../common/Modal';
import InputField from '../InputField';
import Alert from '../Alert';
import Button from '../common/Button';
import { useAuth } from '../../context/AuthContext';
import { changePassword } from '../../services/authService';
import { normalizeValidationErrors } from '../../utils/validationErrorUtils';

const ChangePasswordModal = ({ isOpen, onClose }) => {
  const { token, logout } = useAuth();
  const [formData, setFormData] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const submitting = useRef(false);

  const handleClose = () => {
    if (isLoading) return;
    if (notice?.type === 'success') logout();
    setFormData({ currentPassword: '', newPassword: '', confirmPassword: '' });
    setErrors({});
    setNotice(null);
    onClose();
  };

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: '' }));
    }
    if (notice) setNotice(null);
  };

  const handleFormSubmit = async (e) => {
    e.preventDefault();
    if (submitting.current) return;
    const newErrors = {};

    if (!formData.currentPassword) {
      newErrors.currentPassword = 'Current password is required.';
    }
    if (!formData.newPassword) {
      newErrors.newPassword = 'New password is required.';
    } else if (formData.newPassword.length > 128) {
      newErrors.newPassword = 'Password must be no more than 128 characters.';
    } else if (formData.newPassword.length < 8) {
      newErrors.newPassword = 'Password must be at least 8 characters.';
    } else if (!/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).+$/.test(formData.newPassword)) {
      newErrors.newPassword = 'Include uppercase and lowercase letters, a number, and a special character.';
    }
    if (formData.newPassword !== formData.confirmPassword) {
      newErrors.confirmPassword = 'Passwords do not match.';
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    submitting.current = true; setIsLoading(true);
    setErrors({});
    setNotice(null);

    const result = await changePassword(token, formData.newPassword, formData.currentPassword);
    submitting.current = false; setIsLoading(false);
    if (result.success) {
      setNotice({ type: 'success', title: 'Password updated', message: 'Your new password has been saved. Sign in again to continue.' });
      setFormData({ currentPassword: '', newPassword: '', confirmPassword: '' });
    } else {
      setErrors(normalizeValidationErrors(result.errors));
      setNotice({ type: 'error', title: 'Password not updated', message: result.message || 'Please try again.' });
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title="Change Account Password"
      subtitle="Update your security credentials. We recommend using a strong, unique password."
    >
      {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

      {notice?.type === 'success' ? <Button fullWidth onClick={logout}>Sign in again</Button> : <form onSubmit={handleFormSubmit} noValidate>
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
            <li className={/[a-z]/.test(formData.newPassword) ? 'met' : ''}>Contains a lowercase letter</li>
            <li className={/[0-9]/.test(formData.newPassword) ? 'met' : ''}>Contains a digit (0-9)</li>
            <li className={/[^A-Za-z0-9]/.test(formData.newPassword) ? 'met' : ''}>Contains a special character</li>
          </ul>
        </div>

        <div className="modal-actions-row">
          <Button variant="outline" onClick={handleClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" isLoading={isLoading}>
            Update Password
          </Button>
        </div>
      </form>}
    </Modal>
  );
};

export default ChangePasswordModal;
