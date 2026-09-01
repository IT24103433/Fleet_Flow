import React from 'react';

const Footer = ({ onNavigate }) => {
  return (
    <footer className="customer-footer">
      <div className="footer-container">
        <div className="footer-brand-col">
          <div className="footer-brand">
            <div className="brand-logo-mark small">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2">
                <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
                <circle cx="7" cy="17" r="2" />
                <path d="M9 17h6" />
                <circle cx="17" cy="17" r="2" />
              </svg>
            </div>
            <span className="brand-name">FleetFlow</span>
          </div>
          <p className="footer-desc">
            Modern enterprise mobility and fleet management platform. Engineered for seamless vehicle operations, secure customer bookings, and real-time fleet intelligence.
          </p>
        </div>

        <div className="footer-links-col">
          <h4 className="footer-col-title">Platform</h4>
          <ul className="footer-links-list">
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('landing')}>Home</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('browse')}>Browse Fleet</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('login')}>Customer Sign In</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('register')}>Register Account</button></li>
          </ul>
        </div>

        <div className="footer-links-col">
          <h4 className="footer-col-title">Operations</h4>
          <ul className="footer-links-list">
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('staff-login')}>Staff Portal</button></li>
            <li><span className="footer-link-muted">Fleet Management (Phase 2)</span></li>
            <li><span className="footer-link-muted">Vehicle Ingestion (Phase 2)</span></li>
            <li><span className="footer-link-muted">Maintenance Logs (Phase 2)</span></li>
          </ul>
        </div>
      </div>

      <div className="footer-bottom">
        <div className="footer-bottom-container">
          <p className="copyright-text">
            © {new Date().getFullYear()} FleetFlow Mobility Systems. All rights reserved.
          </p>
          <div className="footer-status-indicator">
            <span className="system-status-dot" aria-hidden="true" />
            <span>Platform Online</span>
          </div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
