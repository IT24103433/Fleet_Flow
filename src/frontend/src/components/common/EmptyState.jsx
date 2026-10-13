import { InfoIcon } from "@phosphor-icons/react/dist/csr/Info";
import React from 'react';
const EmptyState = ({
  icon,
  title,
  description,
  badge = '',
  action,
  className = ''
}) => {
  return <div className={`empty-state-card ${className}`.trim()}>
      <div className="empty-state-icon">
        {icon || <InfoIcon size={20} weight="regular" aria-hidden="true" />}
      </div>

      {badge && <span className="empty-state-badge">{badge}</span>}
      <h3 className="empty-state-title">{title}</h3>
      <p className="empty-state-desc">{description}</p>

      {action && <div className="empty-state-action">{action}</div>}
    </div>;
};
export default EmptyState;
