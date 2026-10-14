import { useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import useConfirmAction from './useConfirmAction';

export default function useLogoutConfirmation(onLoggedOut) {
  const { token, isAuthenticated, logout } = useAuth();
  const { requestConfirmation, confirmationDialog } = useConfirmAction();
  // A security sign-out or new sign-in invalidates any previous logout prompt.
  useEffect(() => { requestConfirmation(null); }, [token, requestConfirmation]);
  const requestLogout = () => {
    if (!isAuthenticated) return;
    const requestedToken = token;
    requestConfirmation({
      title: 'Log out of FleetFlow?',
      message: 'You can sign in again whenever you’re ready.',
      cancelLabel: 'Cancel',
      label: 'Log out',
      action: () => {
        if (window.sessionStorage.getItem('token') !== requestedToken) return;
        logout();
        onLoggedOut?.();
      },
    });
  };
  return { requestLogout, logoutDialog: isAuthenticated ? confirmationDialog : null };
}
