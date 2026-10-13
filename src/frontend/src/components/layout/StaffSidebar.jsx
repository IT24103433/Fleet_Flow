import { CarProfileIcon } from "@phosphor-icons/react/dist/csr/CarProfile";
import { CaretDoubleLeftIcon } from "@phosphor-icons/react/dist/csr/CaretDoubleLeft";
import { CaretDoubleRightIcon } from "@phosphor-icons/react/dist/csr/CaretDoubleRight";
import { ChartBarIcon } from "@phosphor-icons/react/dist/csr/ChartBar";
import { PlusCircleIcon } from "@phosphor-icons/react/dist/csr/PlusCircle";
import { SquaresFourIcon } from "@phosphor-icons/react/dist/csr/SquaresFour";
import { UserCircleIcon } from "@phosphor-icons/react/dist/csr/UserCircle";
import { UserPlusIcon } from "@phosphor-icons/react/dist/csr/UserPlus";
import { UsersIcon } from "@phosphor-icons/react/dist/csr/Users";
import { WrenchIcon } from "@phosphor-icons/react/dist/csr/Wrench";
import AccountPresentation from '../common/AccountPresentation';
import React from 'react';
import { useAuth } from '../../context/AuthContext';
import NotificationNavButton from '../NotificationNavButton';
import { canViewReport } from '../../utils/reportPermissions';
const StaffSidebar = ({
  currentView,
  onNavigate,
  sidebarCollapsed,
  onToggleCollapse
}) => {
  const {
    user,
    roles
  } = useAuth();
  const primaryRole = roles?.[0];
  const isAdmin = roles?.some(role => String(role).toUpperCase() === 'ADMIN');
  const userRoles = roles || [];
  const canAddVehicle = userRoles.some(r => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));
  return <aside className={`staff-sidebar ${sidebarCollapsed ? 'collapsed' : ''}`}>
      <div className="staff-sidebar-header">
        <button className="staff-brand-row" onClick={() => onNavigate(isAdmin ? 'admin-dashboard' : 'staff-dashboard')} type="button">
          <div className="brand-logo-mark small">
            <CarProfileIcon size={20} weight="regular" aria-hidden="true" />
          </div>
          {!sidebarCollapsed && <div className="staff-brand-text">
              <span className="brand-name">FleetFlow</span>
              <span className="portal-subtag">{isAdmin ? 'Admin Console' : 'Operations Portal'}</span>
            </div>}
        </button>

        <button type="button" className="sidebar-collapse-btn" onClick={onToggleCollapse} aria-label={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"} title={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}>
          {sidebarCollapsed ? <CaretDoubleRightIcon width="18" height="18" weight="regular" aria-hidden="true" /> : <CaretDoubleLeftIcon width="18" height="18" weight="regular" aria-hidden="true" />}
        </button>
      </div>

      {/* Main Staff Navigation */}
      <nav className="staff-sidebar-nav" aria-label="Staff Navigation">
        <div className="nav-section-label">{!sidebarCollapsed && 'OPERATIONS'}</div>

        <button type="button" className={`staff-nav-item ${currentView === 'staff-dashboard' || currentView === 'admin-dashboard' ? 'active' : ''}`} onClick={() => onNavigate(isAdmin ? 'admin-dashboard' : 'staff-dashboard')} title="Operations Dashboard">
          <SquaresFourIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
          {!sidebarCollapsed && <span className="nav-item-label">Dashboard</span>}
        </button>

        <button type="button" className={`staff-nav-item ${currentView === 'manage-fleet' ? 'active' : ''}`} onClick={() => onNavigate('manage-fleet')} title="Vehicles Inventory">
          <CarProfileIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
          {!sidebarCollapsed && <span className="nav-item-label">Vehicles</span>}
        </button>

        <button type="button" className={`staff-nav-item ${currentView === 'maintenance-dashboard' ? 'active' : ''}`} onClick={() => onNavigate('maintenance-dashboard')} title="Maintenance workspace">
          <WrenchIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
          {!sidebarCollapsed && <span className="nav-item-label">Maintenance</span>}
        </button>

        {canAddVehicle && <button type="button" className={`staff-nav-item ${currentView === 'add-vehicle' ? 'active' : ''}`} onClick={() => onNavigate('add-vehicle')} title="Add New Vehicle">
            <PlusCircleIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
            {!sidebarCollapsed && <span className="nav-item-label">Add Vehicle</span>}
          </button>}

        {isAdmin && <>
            <div className="nav-section-label">{!sidebarCollapsed && 'ADMINISTRATION'}</div>
            <button type="button" className={`staff-nav-item ${currentView === 'admin-users' || currentView === 'admin-user-details' ? 'active' : ''}`} onClick={() => onNavigate('admin-users')} title="User Directory & Roles">
              <UsersIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
              {!sidebarCollapsed && <span className="nav-item-label">User Management</span>}
            </button>

            <button type="button" className={`staff-nav-item ${currentView === 'admin-create-user' ? 'active' : ''}`} onClick={() => onNavigate('admin-create-user')} title="Create an account">
              <UserPlusIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
              {!sidebarCollapsed && <span className="nav-item-label">Create User</span>}
            </button>
        <button type="button" className={`staff-nav-item ${currentView === 'admin-notification-delivery' ? 'active' : ''}`}
          onClick={() => onNavigate('admin-notification-delivery')} title="Notification delivery" aria-label="Notification delivery">
          <ChartBarIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
          {!sidebarCollapsed && <span className="nav-item-label">Notification delivery</span>}
        </button>
          </>}

        <div className="nav-section-label">{!sidebarCollapsed && 'REPORTS'}</div>
        {[{
        view: 'fleet-performance',
        label: 'Fleet reports'
      }, {
        view: 'booking-reports',
        label: 'Booking Reports'
      }, {
        view: 'maintenance-reports',
        label: 'Maintenance Reports'
      }].filter(item => canViewReport(item.view, userRoles)).map(item => <button key={item.view} type="button" className={`staff-nav-item ${currentView === item.view ? 'active' : ''}`} onClick={() => onNavigate(item.view)} title={item.label} aria-label={item.label}>
            <ChartBarIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
            {!sidebarCollapsed && <span className="nav-item-label">{item.label}</span>}
          </button>)}


        <div className="nav-section-label">{!sidebarCollapsed && 'ACCOUNT'}</div>
        <NotificationNavButton className={`staff-nav-item ${currentView === 'staff-notifications' ? 'active' : ''}`} collapsed={sidebarCollapsed} onClick={() => onNavigate('staff-notifications')} />

        <button type="button" className={`staff-nav-item ${currentView === 'staff-profile' ? 'active' : ''}`} onClick={() => onNavigate('staff-profile')} title="Staff Account">
          <UserCircleIcon className="nav-item-icon" weight="regular" aria-hidden="true" />
          {!sidebarCollapsed && <span className="nav-item-label">Staff Profile</span>}
        </button>
      </nav>

      {/* Staff Profile & Logout */}
      <div className="staff-sidebar-footer">
        {!sidebarCollapsed && <button className="staff-user-card clickable" onClick={() => onNavigate('staff-profile')} title="Inspect Staff Profile" type="button">
            <AccountPresentation account={user} role={primaryRole} />
          </button>}
      </div>
    </aside>;
};
export default StaffSidebar;
