import React, { useState, useEffect, useRef } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { extractRolesFromToken, getRoleDefaultView } from './utils/roleUtils';
import CustomerNav from './components/layout/CustomerNav';
import Footer from './components/layout/Footer';
import StaffSidebar from './components/layout/StaffSidebar';
import StaffHeader from './components/layout/StaffHeader';
import RecordPage from './components/common/RecordPage';
import { parseRoute, routeHash, safeCustomerReturn } from './utils/navigation';

// Customer Pages
const LandingPage = React.lazy(() => import('./pages/LandingPage'));
const LoginPage = React.lazy(() => import('./pages/LoginPage'));
const RegisterPage = React.lazy(() => import('./pages/RegisterPage'));
const BrowseFleetPage = React.lazy(() => import('./pages/BrowseFleetPage'));
const VehicleDetailsPage = React.lazy(() => import('./pages/customer/VehicleDetailsPage'));
const CustomerHomePage = React.lazy(() => import('./pages/customer/CustomerHomePage'));
const CustomerProfilePage = React.lazy(() => import('./pages/customer/CustomerProfilePage'));

const RentalQueuePage = React.lazy(() => import('./pages/rental/RentalQueuePage'));
const RentalRecordPage = React.lazy(() => import('./pages/rental/RentalRecordPage'));

// Staff & Admin Pages
const StaffLoginPage = React.lazy(() => import('./pages/StaffLoginPage'));
const StaffDashboardPage = React.lazy(() => import('./pages/StaffDashboardPage'));
const StaffProfilePage = React.lazy(() => import('./pages/staff/StaffProfilePage'));
const AdminDashboardPage = React.lazy(() => import('./pages/admin/AdminDashboardPage'));
const AdminUserListPage = React.lazy(() => import('./pages/admin/AdminUserListPage'));
const AdminCreateUserPage = React.lazy(() => import('./pages/admin/AdminCreateUserPage'));
const AdminEditUserPage = React.lazy(() => import('./pages/admin/AdminEditUserPage'));
const AdminUserDetailsPage = React.lazy(() => import('./pages/admin/AdminUserDetailsPage'));

// Vehicle Management Pages
const ManageFleetPage = React.lazy(() => import('./pages/vehicle/ManageFleetPage'));
const AddVehiclePage = React.lazy(() => import('./pages/vehicle/AddVehiclePage'));
const EditVehiclePage = React.lazy(() => import('./pages/vehicle/EditVehiclePage'));
const StaffVehicleDetailsPage = React.lazy(() => import('./pages/vehicle/StaffVehicleDetailsPage'));
const VehicleImageManagementPage = React.lazy(() => import('./pages/vehicle/VehicleImageManagementPage'));
const MaintenanceDashboardPage = React.lazy(() => import('./pages/maintenance/MaintenanceDashboardPage'));

// Security Pages
import ForcePasswordChangePage from './pages/security/ForcePasswordChangePage';
import './App.css';
const DriverReviewsPage = React.lazy(() => import('./pages/onboarding/DriverReviewsPage'));
const AccountAdministrationPage = React.lazy(() => import('./pages/onboarding/AccountAdministrationPage'));
const NotificationDeliveryPage = React.lazy(() => import('./pages/admin/NotificationDeliveryPage'));
const NotificationsPage = React.lazy(() => import('./pages/NotificationsPage'));
import { REPORT_VIEWS, canViewReport } from './utils/reportPermissions';
const FleetPerformancePage = React.lazy(() => import('./pages/reports/FleetPerformancePage'));
const BookingReportsPage = React.lazy(() => import('./pages/reports/BookingReportsPage'));
const MaintenanceReportsPage = React.lazy(() => import('./pages/reports/MaintenanceReportsPage'));
const STAFF_ROLES = ['FLEET_MANAGER', 'MAINTENANCE_STAFF', 'ADMIN'];
const STAFF_VIEWS = ['admin-account-security', 'admin-driver-reviews', 'rental-queue', 'staff-rental-record', ...REPORT_VIEWS, 'staff-dashboard', 'staff-notifications', 'admin-dashboard', 'admin-notification-delivery', 'admin-users', 'admin-create-user', 'admin-edit-user', 'admin-user-details', 'staff-profile', 'manage-fleet', 'maintenance-dashboard', 'add-vehicle', 'edit-vehicle', 'staff-vehicle-details', 'vehicle-images'];
const CUSTOMER_ONLY_VIEWS = ['rental-record', 'customer-home', 'customer-profile', 'notifications'];
function AppContent() {
  const {
    isAuthenticated,
    user,
    roles,
    isLoading
  } = useAuth();
  const [route, setRoute] = useState(() => parseRoute(window.location.hash));
  const currentView = route.view;
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [selectedAdminUser, setSelectedAdminUser] = useState(null);
  const [selectedVehicle, setSelectedVehicle] = useState(null);
  const selection = useRef({});
  const lastBrowse = useRef(route.view === 'browse' ? route.params : {});
  const selectVehicle = vehicle => {
    selection.current.vehicle = vehicle;
    setSelectedVehicle(vehicle);
  };
  const selectUser = account => {
    selection.current.user = account;
    setSelectedAdminUser(account);
  };
  useEffect(() => {
    if (!mobileSidebarOpen) return;
    const opener = document.activeElement;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const desktopLayout = window.matchMedia('(min-width: 768px)');
    const closeOnDesktop = event => {
      if (event.matches) setMobileSidebarOpen(false);
    };
    desktopLayout.addEventListener('change', closeOnDesktop);
    document.querySelector('.staff-mobile-close')?.focus();
    const closeOnEscape = event => {
      if (event.key === 'Escape') setMobileSidebarOpen(false);
      if (event.key === 'Tab') {
        const items = [...document.querySelectorAll('#staff-navigation button, #staff-navigation [tabindex="0"]')].filter(item => !item.disabled && item.getClientRects().length);
        const first = items[0];
        const last = items.at(-1);
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => {
      window.removeEventListener('keydown', closeOnEscape);
      document.body.style.overflow = previousOverflow;
      desktopLayout.removeEventListener('change', closeOnDesktop);
      if (opener?.isConnected) opener.focus();
    };
  }, [mobileSidebarOpen]);

  // Sync hash routing with view state
  useEffect(() => {
    const handleHashChange = () => {
      const next = parseRoute(window.location.hash);
      if (next.view === 'browse') lastBrowse.current = next.params;
      setRoute(next);
    };
    handleHashChange();
    window.addEventListener('hashchange', handleHashChange);
    window.addEventListener('popstate', handleHashChange);
    return () => { window.removeEventListener('hashchange', handleHashChange); window.removeEventListener('popstate', handleHashChange); };
  }, []);
  const navigateTo = (viewName, params = {}, replace = false) => {
    setMobileSidebarOpen(false);
    if (params?.user) {
      selectUser(params.user);
    }
    if (params?.vehicle) {
      selectVehicle(params.vehicle);
    }
    const hash = routeHash(viewName, {
      ...(viewName === 'browse' && !Object.keys(params).length ? lastBrowse.current : {}),
      ...params,
      vehicleId: params.vehicleId || params.vehicle?.id || route.params.vehicleId || selection.current.vehicle?.id || selectedVehicle?.id,
      userId: params.userId || params.user?.id || route.params.userId || selection.current.user?.id || selectedAdminUser?.id
    });
    setRoute(parseRoute(hash));
    if (replace) window.history.replaceState(null, '', hash);
    else if (window.location.hash !== hash) window.history.pushState(null, '', hash);
    window.scrollTo({
      top: 0,
      behavior: 'auto'
    });
  };
  const userRoles = roles || [];
  const isStaffUser = isAuthenticated && userRoles.some(r => STAFF_ROLES.includes(String(r).toUpperCase()));
  const handleLoginSuccess = (token, loginUserObj) => {
    if (loginUserObj?.mustChangePassword) {
      navigateTo('force-password-change', {
        returnTo: route.params.returnTo
      }, true);
      return;
    }
    const rolesFromToken = extractRolesFromToken(token);
    const returnHash = getRoleDefaultView(rolesFromToken) === 'customer-home' ? safeCustomerReturn(route.params.returnTo) : null;
    const destination = returnHash ? parseRoute(returnHash) : {
      view: getRoleDefaultView(rolesFromToken),
      params: {}
    };
    navigateTo(destination.view, destination.params, true);
  };
  const canAddVehicle = isStaffUser && userRoles.some(r => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
  const isAdminUser = isStaffUser && userRoles.some(r => String(r).toUpperCase() === 'ADMIN');

  // Enforce portal separation and route protection
  let activeView = currentView;
  if (!isLoading) {
    if (isAuthenticated && user?.mustChangePassword) {
      activeView = 'force-password-change';
    } else if (currentView === 'force-password-change') {
      activeView = isAuthenticated ? getRoleDefaultView(userRoles) : 'login';
    } else if (isStaffUser && CUSTOMER_ONLY_VIEWS.includes(currentView)) {
      activeView = getRoleDefaultView(userRoles);
    } else if (!isAuthenticated && CUSTOMER_ONLY_VIEWS.includes(currentView)) {
      activeView = 'login';
    } else if (isAuthenticated && !isStaffUser && STAFF_VIEWS.includes(currentView)) {
      activeView = 'customer-home';
    } else if (isStaffUser && REPORT_VIEWS.includes(currentView) && !canViewReport(currentView, userRoles)) {
      activeView = 'staff-dashboard';
    } else if (isStaffUser && ['admin-account-security', 'admin-driver-reviews', 'admin-dashboard', 'admin-notification-delivery', 'admin-users', 'admin-create-user', 'admin-edit-user', 'admin-user-details'].includes(currentView) && !isAdminUser) {
      activeView = 'staff-dashboard';
    } else if (isStaffUser && ['rental-queue', 'staff-rental-record'].includes(currentView) && !canAddVehicle) {
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
      window.location.replace(routeHash(activeView, { ...route.params, returnTo: route.params.returnTo || safeCustomerReturn(routeHash(currentView, route.params)) }));
    }
  }, [activeView, currentView, route.params]);
  if (isLoading) {
    return <div className="app-shell view-60f79f9f">
        <div className="view-b08766ee">
          <div className="spinner view-7e678f08" />
          <p>Initializing FleetFlow...</p>
        </div>
      </div>;
  }

  // Handle Forced Password Change for any user (Staff or Customer)
  if (activeView === 'force-password-change') {
    return <div className="app-shell">
        <CustomerNav currentView={activeView} onNavigate={navigateTo} />
        <main tabIndex={-1} className="customer-main-content">
          <ForcePasswordChangePage onComplete={() => {
          const returnHash = getRoleDefaultView(userRoles) === 'customer-home' ? safeCustomerReturn(route.params.returnTo) : null;
          const destination = returnHash ? parseRoute(returnHash) : {
            view: getRoleDefaultView(userRoles),
            params: {}
          };
          navigateTo(destination.view, destination.params);
        }} />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>;
  }

  // Handle Staff & Admin Views
  if (STAFF_VIEWS.includes(activeView)) {
    if (!isStaffUser) {
      if (isAuthenticated) {
        // Authenticated customer attempting to view staff console -> redirect to customer home
        return <div className="app-shell">
            <CustomerNav currentView={activeView} onNavigate={navigateTo} />
            <main tabIndex={-1} className="customer-main-content">
              <React.Suspense fallback={<p className="page-loading" role="status">Loading your workspace…</p>}><CustomerHomePage onNavigate={navigateTo} /></React.Suspense>
            </main>
            <Footer onNavigate={navigateTo} />
          </div>;
      }

      // Role Gate: Redirect unauthorized visitors to staff login
      return <div className="app-shell">
          <CustomerNav currentView={activeView} onNavigate={navigateTo} />
          <main tabIndex={-1} className="customer-main-content">
            <StaffLoginPage onNavigateToCustomerLogin={() => navigateTo('login')} onLoginSuccess={handleLoginSuccess} />
          </main>
          <Footer onNavigate={navigateTo} />
        </div>;
    }
    const renderStaffContent = () => {
      switch (activeView) {
        case 'rental-queue':
          return <RentalQueuePage key={user?.id} onNavigate={navigateTo} />;
        case 'staff-rental-record':
          return <RentalRecordPage key={`${user?.id}:${route.params.bookingId || route.params.rentalId}`} staff bookingId={route.params.bookingId} rentalId={route.params.rentalId} onNavigate={navigateTo} />;
        case 'admin-notification-delivery':
          return <NotificationDeliveryPage key={user?.id} />;
        case 'staff-notifications':
          return <NotificationsPage key={user?.id} />;
        case 'fleet-performance':
          return <React.Suspense fallback={<p role="status">Loading report…</p>}><FleetPerformancePage key={user?.id} /></React.Suspense>;
        case 'booking-reports':
          return <React.Suspense fallback={<p role="status">Loading report…</p>}><BookingReportsPage key={user?.id} /></React.Suspense>;
        case 'maintenance-reports':
          return <React.Suspense fallback={<p role="status">Loading report…</p>}><MaintenanceReportsPage key={user?.id} /></React.Suspense>;
        case 'admin-dashboard':
          return <AdminDashboardPage onNavigate={navigateTo} />;
        case 'admin-driver-reviews':
          return <DriverReviewsPage />;
        case 'admin-account-security':
          return <AccountAdministrationPage />;
        case 'admin-users':
          return <AdminUserListPage onNavigate={navigateTo} onSelectUser={selectUser} />;
        case 'admin-create-user':
          return <AdminCreateUserPage onNavigate={navigateTo} />;
        case 'admin-edit-user':
          return <RecordPage kind="user" id={route.params.userId} onNavigate={navigateTo}>{account => <AdminEditUserPage selectedUser={account} onNavigate={navigateTo} onUserUpdated={selectUser} />}</RecordPage>;
        case 'admin-user-details':
          return <RecordPage kind="user" id={route.params.userId} onNavigate={navigateTo}>{account => <AdminUserDetailsPage selectedUser={account} onNavigate={navigateTo} />}</RecordPage>;
        case 'staff-profile':
          return <StaffProfilePage onNavigate={navigateTo} />;
        case 'manage-fleet':
          return <ManageFleetPage onNavigate={navigateTo} onSelectVehicle={selectVehicle} />;
        case 'maintenance-dashboard':
          return <MaintenanceDashboardPage onNavigate={navigateTo} onSelectVehicle={selectVehicle} />;
        case 'add-vehicle':
          return <AddVehiclePage onNavigate={navigateTo} />;
        case 'edit-vehicle':
          return <RecordPage kind="vehicle" id={route.params.vehicleId} onNavigate={navigateTo}>{vehicle => <EditVehiclePage selectedVehicle={vehicle} onNavigate={navigateTo} onVehicleUpdated={selectVehicle} />}</RecordPage>;
        case 'staff-vehicle-details':
          return <RecordPage kind="vehicle" id={route.params.vehicleId} onNavigate={navigateTo}>{vehicle => <StaffVehicleDetailsPage selectedVehicle={vehicle} onNavigate={navigateTo} onSelectVehicle={selectVehicle} />}</RecordPage>;
        case 'vehicle-images':
          return <RecordPage kind="vehicle" id={route.params.vehicleId} onNavigate={navigateTo}>{vehicle => <VehicleImageManagementPage selectedVehicle={vehicle} onNavigate={navigateTo} />}</RecordPage>;
        case 'staff-dashboard':
        default:
          return <StaffDashboardPage onNavigate={navigateTo} />;
      }
    };
    const getStaffHeaderTitle = () => {
      switch (activeView) {
        case 'rental-queue':
          return 'Rental handovers';
        case 'staff-rental-record':
          return 'Rental record';
        case 'admin-notification-delivery':
          return 'Notification operations';
        case 'staff-notifications':
          return 'Notifications';
        case 'fleet-performance':
          return 'Fleet performance';
        case 'booking-reports':
          return 'Booking Reports';
        case 'maintenance-reports':
          return 'Maintenance Reports';
        case 'admin-dashboard':
          return 'Administrator Workspace';
        case 'admin-driver-reviews':
          return 'Driver verification';
        case 'admin-account-security':
          return 'Account administration';
        case 'admin-users':
          return 'User directory';
        case 'admin-create-user':
          return 'Create account';
        case 'admin-edit-user':
          return 'Edit account';
        case 'admin-user-details':
          return 'Account details';
        case 'staff-profile':
          return 'Staff profile';
        case 'manage-fleet':
          return 'Fleet inventory';
        case 'maintenance-dashboard':
          return 'Maintenance';
        case 'add-vehicle':
          return 'Add vehicle';
        case 'edit-vehicle':
          return 'Edit vehicle';
        case 'staff-vehicle-details':
          return 'Vehicle details';
        case 'vehicle-images':
          return 'Vehicle photos';
        case 'staff-dashboard':
        default:
          return 'Operations Workspace';
      }
    };
    return <div className="staff-layout-container">
        {mobileSidebarOpen && <button className="staff-nav-backdrop" aria-label="Close staff navigation" onClick={() => setMobileSidebarOpen(false)} />}
        <div className={`staff-sidebar-wrapper ${mobileSidebarOpen ? 'mobile-open' : ''}`} id="staff-navigation">
          <button type="button" className="staff-mobile-close" onClick={() => setMobileSidebarOpen(false)}>Close navigation <span aria-hidden="true">Ã—</span></button>
          <StaffSidebar currentView={activeView} onNavigate={navigateTo} sidebarCollapsed={mobileSidebarOpen ? false : sidebarCollapsed} onToggleCollapse={() => setSidebarCollapsed(!sidebarCollapsed)} />
        </div>

        <div className="staff-main-canvas" inert={mobileSidebarOpen}>
          <StaffHeader title={getStaffHeaderTitle()} onNavigate={navigateTo} onToggleSidebar={() => setMobileSidebarOpen(!mobileSidebarOpen)} sidebarOpen={mobileSidebarOpen} />
          <main tabIndex={-1}>
            <React.Suspense fallback={<p className="page-loading" role="status">Loading workspace…</p>}>{renderStaffContent()}</React.Suspense>
          </main>
        </div>
      </div>;
  }

  // Handle Staff Login view
  if (activeView === 'staff-login') {
    return <div className="app-shell">
        <CustomerNav currentView={activeView} onNavigate={navigateTo} />
        <main tabIndex={-1} className="customer-main-content">
          <React.Suspense fallback={<p className="page-loading" role="status">Loading sign in…</p>}><StaffLoginPage onNavigateToCustomerLogin={() => navigateTo('login')} onLoginSuccess={handleLoginSuccess} /></React.Suspense>
        </main>
        <Footer onNavigate={navigateTo} />
      </div>;
  }

  // Handle Force Password Change view
  if (activeView === 'force-password-change') {
    return <div className="app-shell">
        <CustomerNav currentView={activeView} onNavigate={navigateTo} />
        <main tabIndex={-1} className="customer-main-content">
          <ForcePasswordChangePage username={user?.username || 'User'} onComplete={() => navigateTo(getRoleDefaultView(userRoles))} />
        </main>
        <Footer onNavigate={navigateTo} />
      </div>;
  }

  // Customer Portal Views
  const renderCustomerView = () => {
    switch (activeView) {
      case 'rental-record':
        return <RentalRecordPage key={`${user?.id}:${route.params.rentalId || route.params.bookingId}`} bookingId={route.params.bookingId} rentalId={route.params.rentalId} onNavigate={navigateTo} />;
      case 'notifications':
        return <NotificationsPage key={user?.id} />;
      case 'login':
        return <LoginPage onNavigateToRegister={() => navigateTo('register', {
          returnTo: route.params.returnTo
        })} onNavigateToStaffLogin={() => navigateTo('staff-login')} onLoginSuccess={handleLoginSuccess} />;
      case 'register':
        return <RegisterPage onNavigateToLogin={() => navigateTo('login', {
          returnTo: route.params.returnTo
        })} />;
      case 'browse':
        return <BrowseFleetPage onNavigate={navigateTo} onSelectVehicle={selectVehicle} initialFilters={route.params} onFiltersChange={filters => {
          const hash = routeHash('browse', filters);
          lastBrowse.current = filters;
          window.history.replaceState(null, '', hash);
          setRoute(parseRoute(hash));
        }} />;
      case 'vehicle-details':
        return <RecordPage kind="vehicle" id={route.params.vehicleId} onNavigate={navigateTo} fallbackView="browse">{vehicle => <VehicleDetailsPage selectedVehicle={vehicle} onNavigate={navigateTo} />}</RecordPage>;
      case 'customer-home':
        return <CustomerHomePage onNavigate={navigateTo} onSelectVehicle={selectVehicle} />;
      case 'customer-profile':
        return <CustomerProfilePage onNavigate={navigateTo} />;
      case 'landing':
      default:
        return <LandingPage onNavigate={navigateTo} />;
    }
  };
  return <div className="app-shell">
      <CustomerNav currentView={activeView} onNavigate={navigateTo} />
      <main tabIndex={-1} className="customer-main-content">
        <React.Suspense fallback={<p className="page-loading" role="status">Loading page…</p>}>{renderCustomerView()}</React.Suspense>
      </main>
      <Footer onNavigate={navigateTo} />
    </div>;
}
function App() {
  return <AuthProvider>
      <a className="skip-link" href="#main-content" onClick={event => {
      event.preventDefault();
      document.querySelector('main')?.focus();
    }}>Skip to content</a>
      <AppContent />
    </AuthProvider>;
}
export default App;
