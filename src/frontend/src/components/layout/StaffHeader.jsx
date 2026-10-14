import { ListIcon } from "@phosphor-icons/react/dist/csr/List";
import { SignOutIcon } from '@phosphor-icons/react/dist/csr/SignOut';
import Button from '../common/Button';
import AccountPresentation from '../common/AccountPresentation';
import React from 'react';
import { useAuth } from '../../context/AuthContext';
import useLogoutConfirmation from '../common/useLogoutConfirmation';
const StaffHeader = ({
  title = 'Operations Dashboard',
  onToggleSidebar,
  onNavigate,
  sidebarOpen = false
}) => {
  const {
    user,
    roles
  } = useAuth();
  const { requestLogout, logoutDialog } = useLogoutConfirmation(() => onNavigate('staff-login'));
  const primaryRole = roles?.[0];
  const todayFormatted = new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  }).format(new Date());
  return <header className="staff-header">
      <div className="staff-header-left">
        <button type="button" className="staff-mobile-toggle-btn" onClick={onToggleSidebar} aria-label="Toggle navigation sidebar" aria-expanded={sidebarOpen} aria-controls="staff-navigation">
          <ListIcon width="22" height="22" weight="regular" aria-hidden="true" />
        </button>
        <div className="staff-title-group">
          <p className="staff-header-title"><span>Workspace</span><span aria-hidden="true"> / </span>{title}</p>
          <span className="staff-date-text">{todayFormatted}</span>
        </div>
      </div>

      <div className="staff-header-right">
        <AccountPresentation account={user} role={primaryRole} />
        <Button variant="outline" size="sm" className="header-logout" onClick={requestLogout}><SignOutIcon size={18} weight="regular" aria-hidden="true" />Log out</Button>
      </div>
      {logoutDialog}
    </header>;
};
export default StaffHeader;
