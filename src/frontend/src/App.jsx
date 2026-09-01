import React, { useState, useEffect } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import CustomerNav from './components/layout/CustomerNav';
import Footer from './components/layout/Footer';
import StaffSidebar from './components/layout/StaffSidebar';
import StaffHeader from './components/layout/StaffHeader';

// Customer Pages
import LandingPage from './pages/LandingPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import BrowseFleetPage from './pages/BrowseFleetPage';
import VehicleDetailsPage from './pages/customer/VehicleDetailsPage';
import CustomerHomePage from './pages/customer/CustomerHomePage';
import CustomerProfilePage from './pages/customer/CustomerProfilePage';

// Staff & Admin Pages
import StaffLoginPage from './pages/StaffLoginPage';
import StaffDashboardPage from './pages/StaffDashboardPage';
import StaffProfilePage from './pages/staff/StaffProfilePage';
import AdminDashboardPage from './pages/admin/AdminDashboardPage';
import AdminUserListPage from './pages/admin/AdminUserListPage';
import AdminCreateUserPage from './pages/admin/AdminCreateUserPage';
import AdminUserDetailsPage from './pages/admin/AdminUserDetailsPage';

// Vehicle Management Pages
import ManageFleetPage from './pages/vehicle/ManageFleetPage';
import AddVehiclePage from './pages/vehicle/AddVehiclePage';
import VehicleImageManagementPage from './pages/vehicle/VehicleImageManagementPage';

// Security Pages
import ForcePasswordChangePage from './pages/security/ForcePasswordChangePage';

import { INITIAL_VEHICLES } from './data/vehicleData';
import './App.css';

const STAFF_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];
const STAFF_VIEWS = [
  'staff-dashboard',
  'admin-dashboard',
  'admin-users',
  'admin-create-user',
  'admin-user-details',
  'staff-profile',
  'manage-fleet',
  'add-vehicle',
  'vehicle-images',
];

function AppContent() {
  const { isAuthenticated, user, roles, isLoading } = useAuth();
  const [currentView, setCurrentView] = useState('landing');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [selectedAdminUser, setSelectedAdminUser] = useState(null);
  const [selectedVehicle, setSelectedVehicle] = useState(INITIAL_VEHICLES[0]);

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

  const navigateTo = (viewName, params) => {
    if (params?.user) {
      setSelectedAdminUser(params.user);
    }
    if (params?.vehicle) {
      setSelectedVehicle(params.vehicle);
    }
    setCurrentView(viewName);
    window.location.hash = `#/${viewName}`;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const userRoles = roles || [];
  const isStaffUser = isAuthenticated && userRoles.some((r) => STAFF_ROLES.includes(String(r).toUpperCase()));
  const isAdminUser = isAuthenticated && userRoles.includes('ADMIN');

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

  // Handle Staff & Admin Views
  if (STAFF_VIEWS.includes(currentView)) {
    if (!isStaffUser) {
      // Role Gate: Redirect unauthorized customers/visitors to staff login
      return (
        <div className="app-shell">
          <StaffLoginPage
            onNavigateToCustomerLogin={() => navigateTo('login')}
            onLoginSuccess={() => navigateTo(isAdminUser ? 'admin-dashboard' : 'staff-dashboard')}
          />
        </div>
      );
    }

    const renderStaffContent = () => {
      switch (currentView) {
        case 'admin-dashboard':
          return <AdminDashboardPage onNavigate={navigateTo} />;
        case 'admin-users':
          return <AdminUserListPage onNavigate={navigateTo} onSelectUser={setSelectedAdminUser} />;
        case 'admin-create-user':
          return <AdminCreateUserPage onNavigate={navigateTo} />;
        case 'admin-user-details':
          return <AdminUserDetailsPage selectedUser={selectedAdminUser} onNavigate={navigateTo} />;
        case 'staff-profile':
          return <StaffProfilePage onNavigate={navigateTo} />;
        case 'manage-fleet':
          return <ManageFleetPage onNavigate={navigateTo} onSelectVehicle={setSelectedVehicle} />;
        case 'add-vehicle':
          return <AddVehiclePage onNavigate={navigateTo} />;
        case 'vehicle-images':
          return <VehicleImageManagementPage selectedVehicle={selectedVehicle} onNavigate={navigateTo} />;
        case 'staff-dashboard':
        default:
          return <StaffDashboardPage onNavigate={navigateTo} />;
      }
    };

    const getStaffHeaderTitle = () => {
      switch (currentView) {
        case 'admin-dashboard':
          return 'Administrator Workspace';
        case 'admin-users':
          return 'User Directory & Roles';
        case 'admin-create-user':
          return 'Provision New Account';
        case 'admin-user-details':
          return 'User Account Inspection';
        case 'staff-profile':
          return 'Staff Identity & Credentials';
        case 'manage-fleet':
          return 'Fleet Inventory & Operations';
        case 'add-vehicle':
          return 'Ingest New Fleet Unit';
        case 'vehicle-images':
          return 'Vehicle Photo Asset Workspace';
        case 'staff-dashboard':
        default:
          return 'Operations Workspace';
      }
    };

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
            title={getStaffHeaderTitle()}
            onToggleSidebar={() => setMobileSidebarOpen(!mobileSidebarOpen)}
          />
          <main tabIndex={-1}>
            {renderStaffContent()}
          </main>
        </div>
      </div>
    );
  }

  // Handle Staff Login view
  if (currentView === 'staff-login') {
    return (
      <div className="app-shell">
        <CustomerNav currentView={currentView} onNavigate={navigateTo} />
        <main className="customer-main-content">
          <StaffLoginPage
            onNavigateToCustomerLogin={() => navigateTo('login')}
            onLoginSuccess={() => navigateTo(isAdminUser ? 'admin-dashboard' : 'staff-dashboard')}
          />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>
    );
  }

  // Handle Force Password Change view
  if (currentView === 'force-password-change') {
    return (
      <div className="app-shell">
        <CustomerNav currentView={currentView} onNavigate={navigateTo} />
        <main className="customer-main-content">
          <ForcePasswordChangePage
            username={user?.username || 'User'}
            onComplete={() => navigateTo(isStaffUser ? 'staff-dashboard' : 'customer-home')}
          />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>
    );
  }

  // Customer Portal Views
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
            onSelectVehicle={setSelectedVehicle}
          />
        );
      case 'vehicle-details':
        return (
          <VehicleDetailsPage
            selectedVehicle={selectedVehicle}
            onNavigate={navigateTo}
          />
        );
      case 'customer-home':
        return (
          <CustomerHomePage
            onNavigate={navigateTo}
          />
        );
      case 'customer-profile':
        return (
          <CustomerProfilePage
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
