import React, { useState } from 'react';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';

const ForcePasswordChangePage = ({ username = 'User', onComplete }) => {
  const [formData, setFormData] = useState({
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
  };

  const handleFormSubmit = (e) => {
    e.preventDefault();
    const newErrors = {};

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

    setTimeout(() => {
      setIsLoading(false);
      setNotice({
        type: 'success',
        title: 'Permanent Password Configured',
        message: 'Security credentials updated. Redirecting to your workspace...',
      });
      setTimeout(() => {
        if (onComplete) onComplete();
      }, 1000);
    }, 600);
  };

  return (
    <div className="auth-page-layout">
      <div className="auth-card">
        <div className="auth-header">
          <div className="auth-badge-pill" style={{ backgroundColor: 'rgba(245, 158, 11, 0.1)', color: '#B45309', borderColor: 'rgba(245, 158, 11, 0.3)' }}>
            Security Requirement
          </div>
          <h2 className="auth-title">Password Update Required</h2>
          <p className="auth-subtitle">
            Welcome, <strong>{username}</strong>. An administrator has requested that you set a permanent private password before accessing the platform.
          </p>
        </div>

        {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

        <form onSubmit={handleFormSubmit} noValidate className="auth-form">
          <InputField
            label="New Permanent Password"
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

          <Button
            type="submit"
            variant="primary"
            size="lg"
            fullWidth
            isLoading={isLoading}
          >
            Set Password & Access Workspace
          </Button>
        </form>
      </div>
    </div>
  );
};

export default ForcePasswordChangePage;
