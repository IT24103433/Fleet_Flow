import React from 'react';

const InputField = ({
  label,
  type = 'text',
  id,
  name,
  value,
  onChange,
  error,
  required = false,
  disabled = false,
  placeholder = '',
  autoComplete,
  descriptionId,
}) => {
  const completion = autoComplete ?? ({ fullName: 'name', username: 'username', usernameOrEmail: 'username', email: 'email', phoneNumber: 'tel', address: 'street-address', password: 'current-password', currentPassword: 'current-password', newPassword: 'new-password', confirmPassword: 'new-password' })[name];
  return (
    <div className="form-group">
      <label htmlFor={id} className="form-label">
        {label} {required && <span className="required-indicator">*</span>}
      </label>
      <input
        type={type}
        id={id}
        name={name}
        value={value}
        onChange={onChange}
        disabled={disabled}
        placeholder={placeholder}
        autoComplete={completion}
        aria-required={required}
        aria-invalid={!!error}
        aria-describedby={[error ? `${id}-error` : '', descriptionId].filter(Boolean).join(' ') || undefined}
        className={`form-input ${error ? 'input-error' : ''}`}
      />
      {error && (
        <span id={`${id}-error`} className="field-error-message" role="alert">
          {error}
        </span>
      )}
    </div>
  );
};

export default InputField;
