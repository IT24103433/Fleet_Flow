import React from 'react';

const EmptyState = ({
  icon,
  title,
  description,
  badge = 'Awaiting API',
  action,
  className = '',
}) => {
  return (
    <div className={`empty-state-card ${className}`.trim()}>
      <div className="empty-state-icon">
        {icon || (
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        )}
      </div>

      {badge && <span className="empty-state-badge">{badge}</span>}
      <h3 className="empty-state-title">{title}</h3>
      <p className="empty-state-desc">{description}</p>

      {action && <div className="empty-state-action">{action}</div>}
    </div>
  );
};

export default EmptyState;
