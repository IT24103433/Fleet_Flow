import { BellIcon } from "@phosphor-icons/react/dist/csr/Bell";
import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { notificationClient } from '../services/notificationService';
export default function NotificationNavButton({
  onClick,
  className = 'nav-link',
  collapsed = false
}) {
  const {
    token
  } = useAuth();
  const [unread, setUnread] = useState({
    token: null,
    count: null
  });
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController();
    const refresh = async () => {
      try {
        const result = await notificationClient.getUnreadCount(controller.signal);
        if (!controller.signal.aborted) setUnread({
          token,
          count: result.count
        });
      } catch {
        if (!controller.signal.aborted) setUnread({
          token,
          count: null
        });
      }
    };
    refresh();
    const timer = setInterval(refresh, 30000);
    window.addEventListener('notifications-changed', refresh);
    return () => {
      controller.abort();
      clearInterval(timer);
      window.removeEventListener('notifications-changed', refresh);
    };
  }, [token]);
  const count = unread.token === token ? unread.count : null;
  const label = `Notifications${count === null ? '' : ` (${count} unread)`}`;
  return <button type="button" className={className} onClick={onClick} title={label} aria-label={label}>
      <BellIcon width="20" height="20" weight="regular" aria-hidden="true" />
      {!collapsed && <span>Notifications{count > 0 && <span className="notification-count">{count}</span>}</span>}
    </button>;
}
