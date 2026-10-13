import { CheckCircleIcon } from "@phosphor-icons/react/dist/csr/CheckCircle";
import { InfoIcon } from "@phosphor-icons/react/dist/csr/Info";
import { WarningIcon } from "@phosphor-icons/react/dist/csr/Warning";
import { XCircleIcon } from "@phosphor-icons/react/dist/csr/XCircle";
import React from 'react';
const Alert = ({
  type = 'error',
  message,
  title,
  actionText,
  onAction
}) => {
  if (!message) return null;
  const renderIcon = () => {
    switch (type) {
      case 'success':
        return <CheckCircleIcon className="alert-icon" weight="regular" aria-hidden="true" />;
      case 'warning':
        return <WarningIcon className="alert-icon" weight="regular" aria-hidden="true" />;
      case 'info':
        return <InfoIcon className="alert-icon" weight="regular" aria-hidden="true" />;
      case 'error':
      default:
        return <XCircleIcon className="alert-icon" weight="regular" aria-hidden="true" />;
    }
  };
  return <div className={`alert alert-${type}`} role={type === 'error' || type === 'warning' ? 'alert' : 'status'}>
      {renderIcon()}
      <div className="alert-content">
        {title && <strong className="alert-title">{title}</strong>}
        <span className="alert-message">{message}</span>
        {actionText && onAction && <button type="button" className="alert-action" onClick={onAction}>{actionText}</button>}
      </div>
    </div>;
};
export default Alert;
