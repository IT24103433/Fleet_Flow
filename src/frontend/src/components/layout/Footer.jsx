import { CarProfileIcon } from "@phosphor-icons/react/dist/csr/CarProfile";
import React from 'react';
const Footer = ({
  onNavigate
}) => {
  return <footer className="customer-footer">
      <div className="footer-container">
        <div className="footer-brand-col">
          <div className="footer-brand">
            <div className="brand-logo-mark small">
              <CarProfileIcon size={20} weight="regular" aria-hidden="true" />
            </div>
            <span className="brand-name">FleetFlow</span>
          </div>
          <p className="footer-desc">
            Vehicle reservations and fleet operations, brought together. Find your vehicle. Keep your plans in view.
          </p>
        </div>

        <div className="footer-links-col">
          <h4 className="footer-col-title">Platform</h4>
          <ul className="footer-links-list">
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('landing')}>Home</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('browse')}>Vehicles</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('login')}>Sign in</button></li>
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('register')}>Create an account</button></li>
          </ul>
        </div>

        <div className="footer-links-col">
          <h4 className="footer-col-title">Operations</h4>
          <ul className="footer-links-list">
            <li><button type="button" className="footer-link-btn" onClick={() => onNavigate('staff-login')}>Staff Portal</button></li>
            <li><span className="footer-link-muted">Vehicle inventory</span></li>
            <li><span className="footer-link-muted">Maintenance work orders</span></li>
            <li><span className="footer-link-muted">Fleet reports</span></li>
          </ul>
        </div>
      </div>

      <div className="footer-bottom">
        <div className="footer-bottom-container">
          <p className="copyright-text">
            © {new Date().getFullYear()} FleetFlow Mobility Systems. All rights reserved.
          </p>
          <a className="footer-signoff" href="/credits.html">Credits & licences</a>
        </div>
      </div>
    </footer>;
};
export default Footer;
