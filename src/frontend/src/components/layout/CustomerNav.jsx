import { CarProfileIcon } from "@phosphor-icons/react/dist/csr/CarProfile";
import { CaretDownIcon } from "@phosphor-icons/react/dist/csr/CaretDown";
import { ListIcon } from "@phosphor-icons/react/dist/csr/List";
import { XIcon } from "@phosphor-icons/react/dist/csr/X";
import AccountPresentation from '../common/AccountPresentation';
import React, { useState, useEffect, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../common/RoleBadge';
import Button from '../common/Button';
import NotificationNavButton from '../NotificationNavButton';
const CustomerNav = ({
  currentView,
  onNavigate
}) => {
  const {
    isAuthenticated,
    user,
    roles,
    logout
  } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const menuToggle = useRef(null);
  useEffect(() => {
    if (!mobileMenuOpen) return;
    const closeOnEscape = event => {
      if (event.key === 'Escape') {
        setMobileMenuOpen(false);
        menuToggle.current?.focus();
      }
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [mobileMenuOpen]);
  const handleNavClick = view => {
    setMobileMenuOpen(false);
    onNavigate(view);
  };
  const staffRoles = ['ADMIN', 'FLEET_MANAGER', 'MAINTENANCE_STAFF'];
  const isStaff = roles?.some(r => staffRoles.includes(String(r).toUpperCase()));
  const isAdmin = roles?.some(r => String(r).toUpperCase() === 'ADMIN');
  const staffHome = isAdmin ? 'admin-dashboard' : 'staff-dashboard';
  const authenticatedHome = isStaff ? staffHome : 'customer-home';
  const authenticatedProfile = isStaff ? 'staff-profile' : 'customer-profile';
  const primaryRole = roles?.[0];
  const isForcePasswordChange = currentView === 'force-password-change' || user?.mustChangePassword;
  return <header className="customer-nav-header">
      <div className="customer-nav-container">
        {/* Brand Identity */}
        <button type="button" className="customer-nav-brand" onClick={() => {
        if (!isForcePasswordChange) handleNavClick(isAuthenticated ? authenticatedHome : 'landing');
      }} disabled={isForcePasswordChange}>
          <div className="brand-logo-mark">
            <CarProfileIcon size={20} weight="regular" aria-hidden="true" />
          </div>
          <span className="brand-name">FleetFlow</span>
        </button>

        {/* Desktop Navigation Links */}
        {!isForcePasswordChange && <nav className="customer-nav-links" aria-label="Main Navigation">
            <button type="button" className={`nav-link ${currentView === 'landing' || currentView === authenticatedHome ? 'active' : ''}`} onClick={() => handleNavClick(isAuthenticated ? authenticatedHome : 'landing')}>
              {!isAuthenticated ? 'Home' : isStaff ? isAdmin ? 'Admin Console' : 'Staff Console' : 'My bookings'}
            </button>
            <button type="button" className={`nav-link ${currentView === 'browse' || currentView === 'vehicle-details' ? 'active' : ''}`} onClick={() => handleNavClick('browse')}>
              Vehicles
            </button>
            {isAuthenticated && <button type="button" className={`nav-link ${currentView === authenticatedProfile ? 'active' : ''}`} onClick={() => handleNavClick(authenticatedProfile)}>
                Account
              </button>}
          </nav>}

        {/* Desktop Auth & Switcher Actions */}
        <div className="customer-nav-actions">
          {isAuthenticated && !isForcePasswordChange && <NotificationNavButton onClick={() => handleNavClick(isStaff ? 'staff-notifications' : 'notifications')} />}
          {isForcePasswordChange ? <div className="user-profile-menu">
              <span className="view-7d0d107c">
                Password Update Required
              </span>
              <Button variant="outline" size="sm" onClick={logout}>
                Log Out
              </Button>
            </div> : <>
              <button type="button" className="staff-portal-pill-btn" onClick={() => handleNavClick(isStaff ? staffHome : 'staff-login')} title={isStaff ? "Go to your Operations Console" : "Access internal staff and management operations"}>
                <CaretDownIcon width="16" height="16" weight="regular" aria-hidden="true" />
                <span>{isStaff ? isAdmin ? 'Admin Console' : 'Staff Console' : 'Staff Portal'}</span>
              </button>

              {isAuthenticated ? <div className="user-profile-menu">
                  <button className="user-info-chip clickable" onClick={() => handleNavClick(authenticatedProfile)} title="View Profile Details" type="button">
                    <AccountPresentation account={user} role={primaryRole} />
                  </button>
                  <Button variant="outline" size="sm" onClick={logout}>
                    Log Out
                  </Button>
                </div> : <div className="auth-btn-group">
                  <Button variant="ghost" size="sm" onClick={() => handleNavClick('login')}>
                    Sign In
                  </Button>
                  <Button variant="primary" size="sm" onClick={() => handleNavClick('register')}>Create account</Button>
                </div>}
            </>}
        </div>

        {/* Mobile Menu Toggle Button */}
        {!isForcePasswordChange && <button type="button" className="mobile-menu-toggle" ref={menuToggle} aria-controls="customer-mobile-menu" onClick={() => setMobileMenuOpen(!mobileMenuOpen)} aria-expanded={mobileMenuOpen} aria-label="Toggle navigation menu">
            {mobileMenuOpen ? <XIcon width="24" height="24" weight="regular" aria-hidden="true" /> : <ListIcon width="24" height="24" weight="regular" aria-hidden="true" />}
          </button>}
      </div>

      {/* Mobile Menu Drawer */}
      {mobileMenuOpen && <div className="customer-mobile-drawer" id="customer-mobile-menu">
          <nav className="mobile-nav-list" aria-label="Mobile navigation">
            <button type="button" className={`mobile-nav-link ${currentView === 'landing' || currentView === authenticatedHome ? 'active' : ''}`} onClick={() => handleNavClick(isAuthenticated ? authenticatedHome : 'landing')}>
              {!isAuthenticated ? 'Home' : isStaff ? isAdmin ? 'Admin Console' : 'Staff Console' : 'My bookings'}
            </button>
            <button type="button" className={`mobile-nav-link ${currentView === 'browse' ? 'active' : ''}`} onClick={() => handleNavClick('browse')}>
              Vehicles
            </button>
            {isAuthenticated && <button type="button" className={`mobile-nav-link ${currentView === authenticatedProfile ? 'active' : ''}`} onClick={() => handleNavClick(authenticatedProfile)}>
                Account
              </button>}
            <button type="button" className="mobile-nav-link staff-link" onClick={() => handleNavClick(isStaff ? staffHome : 'staff-login')}>
              {isStaff ? isAdmin ? 'Administration' : 'Workspace' : 'Staff sign in'}
            </button>

            <div className="mobile-drawer-auth">
              {isAuthenticated ? <div className="mobile-auth-status">
                  <button className="mobile-user-card" onClick={() => handleNavClick(authenticatedProfile)} type="button">
                    <p className="mobile-user-name">Signed in as <strong>{user?.username}</strong></p>
                    {primaryRole && <RoleBadge role={primaryRole} />}
                  </button>
                  <Button variant="outline" fullWidth onClick={() => {
              logout();
              setMobileMenuOpen(false);
            }}>
                    Log Out
                  </Button>
                </div> : <div className="mobile-auth-actions">
                  <Button variant="primary" fullWidth onClick={() => handleNavClick('login')}>
                    Sign In
                  </Button>
                  <Button variant="outline" fullWidth onClick={() => handleNavClick('register')}>
                    Create account
                  </Button>
                </div>}
            </div>
          </nav>
        </div>}
    </header>;
};
export default CustomerNav;
