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
}) => {
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
        aria-invalid={!!error}
        aria-describedby={error ? `${id}-error` : undefined}
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
