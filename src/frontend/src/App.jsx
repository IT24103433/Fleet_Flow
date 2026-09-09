import React, { useState, useEffect } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { extractRolesFromToken, getRoleDefaultView } from './utils/roleUtils';
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
import AdminEditUserPage from './pages/admin/AdminEditUserPage';
import AdminUserDetailsPage from './pages/admin/AdminUserDetailsPage';

// Vehicle Management Pages
import ManageFleetPage from './pages/vehicle/ManageFleetPage';
import AddVehiclePage from './pages/vehicle/AddVehiclePage';
import EditVehiclePage from './pages/vehicle/EditVehiclePage';
import StaffVehicleDetailsPage from './pages/vehicle/StaffVehicleDetailsPage';
import VehicleImageManagementPage from './pages/vehicle/VehicleImageManagementPage';

// Security Pages
import ForcePasswordChangePage from './pages/security/ForcePasswordChangePage';

import './App.css';

const STAFF_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];
const STAFF_VIEWS = [
  'staff-dashboard',
  'admin-dashboard',
  'admin-users',
  'admin-create-user',
  'admin-edit-user',
  'admin-user-details',
  'staff-profile',
  'manage-fleet',
  'add-vehicle',
  'edit-vehicle',
  'staff-vehicle-details',
  'vehicle-images',
];
const CUSTOMER_ONLY_VIEWS = ['customer-home', 'customer-profile'];

function AppContent() {
  const { isAuthenticated, user, roles, isLoading } = useAuth();
  const [currentView, setCurrentView] = useState('landing');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [selectedAdminUser, setSelectedAdminUser] = useState(null);
  const [selectedVehicle, setSelectedVehicle] = useState(null);

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

  const handleLoginSuccess = (token) => {
    const rolesFromToken = extractRolesFromToken(token);
    const destination = getRoleDefaultView(rolesFromToken);
    navigateTo(destination);
  };

  const canAddVehicle = isStaffUser && userRoles.some((r) => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
  const isAdminUser = isStaffUser && userRoles.some((r) => String(r).toUpperCase() === 'ADMIN');

  // Enforce portal separation and route protection
  let activeView = currentView;
  if (!isLoading) {
    if (isStaffUser && CUSTOMER_ONLY_VIEWS.includes(currentView)) {
      activeView = getRoleDefaultView(userRoles);
    } else if (!isAuthenticated && CUSTOMER_ONLY_VIEWS.includes(currentView)) {
      activeView = 'login';
    } else if (isAuthenticated && !isStaffUser && STAFF_VIEWS.includes(currentView)) {
      activeView = 'customer-home';
    } else if (isStaffUser && ['admin-dashboard', 'admin-users', 'admin-create-user', 'admin-edit-user', 'admin-user-details'].includes(currentView) && !isAdminUser) {
      activeView = 'staff-dashboard';
    } else if (isStaffUser && currentView === 'add-vehicle' && !canAddVehicle) {
      activeView = 'manage-fleet';
    } else if (isStaffUser && currentView === 'edit-vehicle' && !canAddVehicle) {
      activeView = 'manage-fleet';
    } else if (isStaffUser && currentView === 'vehicle-details') {
      activeView = 'staff-vehicle-details';
    }
  }

  // Synchronize hash with activeView redirect
  useEffect(() => {
    if (activeView !== currentView) {
      window.location.hash = `#/${activeView}`;
    }
  }, [activeView, currentView]);

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
  if (STAFF_VIEWS.includes(activeView)) {
    if (!isStaffUser) {
      if (isAuthenticated) {
        // Authenticated customer attempting to view staff console -> redirect to customer home
        return (
          <div className="app-shell">
            <CustomerNav currentView={activeView} onNavigate={navigateTo} />
            <main className="customer-main-content">
              <CustomerHomePage onNavigate={navigateTo} />
            </main>
            <Footer onNavigate={navigateTo} />
          </div>
        );
      }

      // Role Gate: Redirect unauthorized visitors to staff login
      return (
        <div className="app-shell">
          <CustomerNav currentView={activeView} onNavigate={navigateTo} />
          <main className="customer-main-content">
            <StaffLoginPage
              onNavigateToCustomerLogin={() => navigateTo('login')}
              onLoginSuccess={handleLoginSuccess}
            />
          </main>
          <Footer onNavigate={navigateTo} />
        </div>
      );
    }

    const renderStaffContent = () => {
      switch (activeView) {
        case 'admin-dashboard':
          return <AdminDashboardPage onNavigate={navigateTo} />;
        case 'admin-users':
          return <AdminUserListPage onNavigate={navigateTo} onSelectUser={setSelectedAdminUser} />;
        case 'admin-create-user':
          return <AdminCreateUserPage onNavigate={navigateTo} />;
        case 'admin-edit-user':
          return (
            <AdminEditUserPage
              selectedUser={selectedAdminUser}
              onNavigate={navigateTo}
              onUserUpdated={(updated) => setSelectedAdminUser(updated)}
            />
          );
        case 'admin-user-details':
          return <AdminUserDetailsPage selectedUser={selectedAdminUser} onNavigate={navigateTo} />;
        case 'staff-profile':
          return <StaffProfilePage onNavigate={navigateTo} />;
        case 'manage-fleet':
          return <ManageFleetPage onNavigate={navigateTo} onSelectVehicle={setSelectedVehicle} />;
        case 'add-vehicle':
          return <AddVehiclePage onNavigate={navigateTo} />;
        case 'edit-vehicle':
          return <EditVehiclePage selectedVehicle={selectedVehicle} onNavigate={navigateTo} />;
        case 'staff-vehicle-details':
          return <StaffVehicleDetailsPage selectedVehicle={selectedVehicle} onNavigate={navigateTo} onSelectVehicle={setSelectedVehicle} />;
        case 'vehicle-images':
          return <VehicleImageManagementPage selectedVehicle={selectedVehicle} onNavigate={navigateTo} />;
        case 'staff-dashboard':
        default:
          return <StaffDashboardPage onNavigate={navigateTo} />;
      }
    };

    const getStaffHeaderTitle = () => {
      switch (activeView) {
        case 'admin-dashboard':
          return 'Administrator Workspace';
        case 'admin-users':
          return 'User Directory & Roles';
        case 'admin-create-user':
          return 'Provision New Account';
        case 'admin-edit-user':
          return 'Modify User Account';
        case 'admin-user-details':
          return 'User Account Inspection';
        case 'staff-profile':
          return 'Staff Identity & Credentials';
        case 'manage-fleet':
          return 'Fleet Inventory & Operations';
        case 'add-vehicle':
          return 'Ingest New Fleet Unit';
        case 'edit-vehicle':
          return 'Modify Fleet Vehicle';
        case 'staff-vehicle-details':
          return 'Vehicle Specification Inspection';
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
            currentView={activeView}
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
  if (activeView === 'staff-login') {
    return (
      <div className="app-shell">
        <CustomerNav currentView={activeView} onNavigate={navigateTo} />
        <main className="customer-main-content">
          <StaffLoginPage
            onNavigateToCustomerLogin={() => navigateTo('login')}
            onLoginSuccess={handleLoginSuccess}
          />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>
    );
  }

  // Handle Force Password Change view
  if (activeView === 'force-password-change') {
    return (
      <div className="app-shell">
        <CustomerNav currentView={activeView} onNavigate={navigateTo} />
        <main className="customer-main-content">
          <ForcePasswordChangePage
            username={user?.username || 'User'}
            onComplete={() => navigateTo(getRoleDefaultView(userRoles))}
          />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>
    );
  }

  // Customer Portal Views
  const renderCustomerView = () => {
    switch (activeView) {
      case 'login':
        return (
          <LoginPage
            onNavigateToRegister={() => navigateTo('register')}
            onNavigateToStaffLogin={() => navigateTo('staff-login')}
            onLoginSuccess={handleLoginSuccess}
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
            onSelectVehicle={setSelectedVehicle}
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
      <CustomerNav currentView={activeView} onNavigate={navigateTo} />
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
