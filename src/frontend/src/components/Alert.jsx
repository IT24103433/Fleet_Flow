import React from 'react';

const Alert = ({ type = 'error', message }) => {
  if (!message) return null;

  return (
    <div className={`alert alert-${type}`} role="alert">
      <span className="alert-message">{message}</span>
    </div>
  );
};

export default Alert;
