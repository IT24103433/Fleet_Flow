import React from 'react';
// Magic UI Bento Grid port: ordinary CSS, Phosphor icons supplied by callers.
// Actions remain visible and stationary for operational use. MIT notice retained.
export function BentoGrid({ children, className = '', ...props }) {
  return <div className={`bento-grid ${className}`} {...props}>{children}</div>;
}
export function BentoCard({ children, background, className = '', ...props }) {
  return <section className={`bento-card ${className}`} {...props}>
    {background && <div className="bento-background">{background}</div>}
    <div className="bento-content">{children}</div>
  </section>;
}
