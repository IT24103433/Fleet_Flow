import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../common/RoleBadge';
import Button from '../common/Button';

const CustomerNav = ({ currentView, onNavigate }) => {
  const { isAuthenticated, user, roles, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const handleNavClick = (view) => {
    setMobileMenuOpen(false);
    onNavigate(view);
  };

  const primaryRole = roles?.[0] || (isAuthenticated ? 'CUSTOMER' : null);

  return (
    <header className="customer-nav-header">
      <div className="customer-nav-container">
        {/* Brand Identity */}
        <div className="customer-nav-brand" onClick={() => handleNavClick('landing')} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && handleNavClick('landing')}>
          <div className="brand-logo-mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
              <circle cx="7" cy="17" r="2" />
              <path d="M9 17h6" />
              <circle cx="17" cy="17" r="2" />
            </svg>
          </div>
          <span className="brand-name">FleetFlow</span>
        </div>

        {/* Desktop Navigation Links */}
        <nav className="customer-nav-links" aria-label="Main Navigation">
          <button
            type="button"
            className={`nav-link ${currentView === 'landing' ? 'active' : ''}`}
            onClick={() => handleNavClick('landing')}
          >
            Home
          </button>
          <button
            type="button"
            className={`nav-link ${currentView === 'browse' ? 'active' : ''}`}
            onClick={() => handleNavClick('browse')}
          >
            Explore Fleet
          </button>
          <button
            type="button"
            className="nav-link nav-link-muted"
            onClick={() => handleNavClick('landing')}
            title="Feature planned for upcoming phase"
          >
            About
          </button>
        </nav>

        {/* Desktop Auth & Switcher Actions */}
        <div className="customer-nav-actions">
          <button
            type="button"
            className="staff-portal-pill-btn"
            onClick={() => handleNavClick('staff-login')}
            title="Access internal staff and management operations"
          >
            <svg viewBox="0 0 20 20" fill="currentColor" width="16" height="16" aria-hidden="true">
              <path fillRule="evenodd" d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z" clipRule="evenodd" />
            </svg>
            <span>Staff Portal</span>
          </button>

          {isAuthenticated ? (
            <div className="user-profile-menu">
              <div className="user-info-chip">
                <span className="user-greeting">Hi, <strong>{user?.username}</strong></span>
                {primaryRole && <RoleBadge role={primaryRole} />}
              </div>
              <Button variant="outline" size="sm" onClick={logout}>
                Log Out
              </Button>
            </div>
          ) : (
            <div className="auth-btn-group">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleNavClick('login')}
              >
                Sign In
              </Button>
              <Button
                variant="primary"
                size="sm"
                onClick={() => handleNavClick('register')}
              >
                Register
              </Button>
            </div>
          )}
        </div>

        {/* Mobile Menu Toggle Button */}
        <button
          type="button"
          className="mobile-menu-toggle"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-expanded={mobileMenuOpen}
          aria-label="Toggle navigation menu"
        >
          {mobileMenuOpen ? (
            <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          ) : (
            <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none">
              <path d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          )}
        </button>
      </div>

      {/* Mobile Menu Drawer */}
      {mobileMenuOpen && (
        <div className="customer-mobile-drawer" role="dialog" aria-modal="true">
          <nav className="mobile-nav-list">
            <button
              type="button"
              className={`mobile-nav-link ${currentView === 'landing' ? 'active' : ''}`}
              onClick={() => handleNavClick('landing')}
            >
              Home
            </button>
            <button
              type="button"
              className={`mobile-nav-link ${currentView === 'browse' ? 'active' : ''}`}
              onClick={() => handleNavClick('browse')}
            >
              Explore Fleet
            </button>
            <button
              type="button"
              className="mobile-nav-link staff-link"
              onClick={() => handleNavClick('staff-login')}
            >
              Staff Portal Entry
            </button>

            <div className="mobile-drawer-auth">
              {isAuthenticated ? (
                <div className="mobile-auth-status">
                  <div className="mobile-user-card">
                    <p className="mobile-user-name">Signed in as <strong>{user?.username}</strong></p>
                    {primaryRole && <RoleBadge role={primaryRole} />}
                  </div>
                  <Button variant="outline" fullWidth onClick={() => { logout(); setMobileMenuOpen(false); }}>
                    Log Out
                  </Button>
                </div>
              ) : (
                <div className="mobile-auth-actions">
                  <Button variant="primary" fullWidth onClick={() => handleNavClick('login')}>
                    Sign In
                  </Button>
                  <Button variant="outline" fullWidth onClick={() => handleNavClick('register')}>
                    Create Customer Account
                  </Button>
                </div>
              )}
            </div>
          </nav>
        </div>
      )}
    </header>
  );
};

export default CustomerNav;
