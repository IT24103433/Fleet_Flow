import React from 'react';
// Adapted from Magic UI's MIT Shimmer Button. See /licenses/magicui.txt.
export default function ShimmerButton({ children, className = '', type = 'button', ...props }) {
  return <button type={type} className={`shimmer-button ${className}`} {...props}>
    <span className="shimmer-spark-container" aria-hidden="true"><span className="shimmer-slide"><span className="shimmer-spin" /></span></span>
    <span className="shimmer-backdrop" aria-hidden="true" />
    <span className="shimmer-content">{children}</span>
    <span className="shimmer-highlight" aria-hidden="true" />
  </button>;
}
