import React, { useState, useEffect } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import CustomerNav from './components/layout/CustomerNav';
import Footer from './components/layout/Footer';
import StaffSidebar from './components/layout/StaffSidebar';
import StaffHeader from './components/layout/StaffHeader';
import LandingPage from './pages/LandingPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import StaffLoginPage from './pages/StaffLoginPage';
import StaffDashboardPage from './pages/StaffDashboardPage';
import BrowseFleetPage from './pages/BrowseFleetPage';
import './App.css';

const STAFF_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];

function AppContent() {
  const { isAuthenticated, roles, isLoading } = useAuth();
  const [currentView, setCurrentView] = useState('landing');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  // Sync hash routing with view state
  useEffect(() => {
    const handleHashChange = () => {
      const hash = window.location.hash.replace(/^#\/?/, '').trim();
      if (hash) {
        setCurrentView(hash);
      }
    };

    handleHashChange();
    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  const navigateTo = (viewName) => {
    setCurrentView(viewName);
    window.location.hash = `#/${viewName}`;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const userRoles = roles || [];
  const isStaffUser = isAuthenticated && userRoles.some((r) => STAFF_ROLES.includes(String(r).toUpperCase()));

  if (isLoading) {
    return (
      <div className="app-shell" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
        <div style={{ textAlign: 'center', color: 'var(--color-text-secondary)' }}>
          <div className="spinner" style={{ width: '28px', height: '28px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
          <p>Initializing FleetFlow...</p>
        </div>
      </div>
    );
  }

  // If in Staff View and authenticated as Staff
  if (currentView === 'staff-dashboard') {
    if (!isStaffUser) {
      // Redirect unauthenticated or customer users to staff login
      return (
        <div className="app-shell">
          <StaffLoginPage
            onNavigateToCustomerLogin={() => navigateTo('login')}
            onLoginSuccess={() => navigateTo('staff-dashboard')}
          />
        </div>
      );
    }

    return (
      <div className="staff-layout-container">
        <div className={`staff-sidebar-wrapper ${mobileSidebarOpen ? 'mobile-open' : ''}`}>
          <StaffSidebar
            currentView={currentView}
            onNavigate={navigateTo}
            sidebarCollapsed={sidebarCollapsed}
            onToggleCollapse={() => setSidebarCollapsed(!sidebarCollapsed)}
          />
        </div>

        <div className="staff-main-canvas">
          <StaffHeader
            title="Operations Dashboard"
            onToggleSidebar={() => setMobileSidebarOpen(!mobileSidebarOpen)}
          />
          <main tabIndex={-1}>
            <StaffDashboardPage onNavigate={navigateTo} />
          </main>
        </div>
      </div>
    );
  }

  // If in Staff Login view
  if (currentView === 'staff-login') {
    return (
      <div className="app-shell">
        <CustomerNav currentView={currentView} onNavigate={navigateTo} />
        <main className="customer-main-content">
          <StaffLoginPage
            onNavigateToCustomerLogin={() => navigateTo('login')}
            onLoginSuccess={() => navigateTo('staff-dashboard')}
          />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>
    );
  }

  // Customer Portal Views (Landing, Login, Register, Browse, Customer Dashboard)
  const renderCustomerView = () => {
    switch (currentView) {
      case 'login':
        return (
          <LoginPage
            onNavigateToRegister={() => navigateTo('register')}
            onNavigateToStaffLogin={() => navigateTo('staff-login')}
          />
        );
      case 'register':
        return (
          <RegisterPage
            onNavigateToLogin={() => navigateTo('login')}
          />
        );
      case 'browse':
        return (
          <BrowseFleetPage
            onNavigate={navigateTo}
          />
        );
      case 'landing':
      default:
        return (
          <LandingPage
            onNavigate={navigateTo}
          />
        );
    }
  };

  return (
    <div className="app-shell">
      <CustomerNav currentView={currentView} onNavigate={navigateTo} />
      <main className="customer-main-content">
        {renderCustomerView()}
      </main>
      <Footer onNavigate={navigateTo} />
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;
