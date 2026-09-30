import { useEffect, useState } from 'react';
import Alert from '../components/Alert';
import Button from '../components/common/Button';
import EmptyState from '../components/common/EmptyState';
import { notificationClient } from '../services/notificationService';
import './NotificationsPage.css';

export default function NotificationsPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [pendingIds, setPendingIds] = useState([]);
  const [refreshKey, setRefreshKey] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    const load = async () => {
      try {
        const data = await notificationClient.getInbox(controller.signal);
        if (!controller.signal.aborted) {
          setItems(data);
          setError('');
        }
      } catch (e) {
        if (!controller.signal.aborted) setError(e.message);
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };
    load();
    const timer = setInterval(load, 30000);
    return () => { controller.abort(); clearInterval(timer); };
  }, [refreshKey]);

  const markRead = async (id) => {
    setPendingIds(ids => [...ids, id]);
    try {
      await notificationClient.markRead(id);
      setItems(current => current.map(item => item.id === id ? { ...item, isRead: true } : item));
      setError('');
      window.dispatchEvent(new Event('notifications-changed'));
    } catch (e) { setError(e.message); }
    finally { setPendingIds(ids => ids.filter(value => value !== id)); }
  };

  const refresh = () => {
    setError('');
    setLoading(true);
    setRefreshKey(key => key + 1);
  };

  return <NotificationInboxView items={items} loading={loading} error={error} pendingIds={pendingIds} onMarkRead={markRead} onRefresh={refresh} />;
}

export function NotificationInboxView({ items, loading, error, pendingIds, onMarkRead, onRefresh }) {
  return (
    <section className="notification-inbox" aria-labelledby="notification-title">
      <div className="notification-heading">
        <div><h1 id="notification-title">Notifications</h1><p>Booking, maintenance and account updates.</p></div>
        <Button variant="outline" onClick={onRefresh} disabled={loading}>Refresh</Button>
      </div>
      {error && <Alert type="error" title="Unable to Load Notifications" message={error} />}
      {loading ? <div className="reports-loading" role="status"><span className="spinner" aria-hidden="true" /> Loading notifications…</div> : (
        <>
          {!error && items.length === 0 && <EmptyState badge="Inbox" title="You're all caught up" description="Your notifications will appear here when there are updates." />}
          <ul className="notification-list">
            {items.map(item => (
              <li key={item.id} className={`notification-card ${item.isRead ? 'is-read' : 'is-unread'}`}>
                <div className="notification-heading">
                  <h2>{item.title}</h2><span>{item.isRead ? 'Read' : 'Unread'}</span>
                </div>
                <p>{item.message}</p>
                <div className="notification-heading">
                  <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString()}</time>
                  {!item.isRead && <Button variant="outline" disabled={pendingIds.includes(item.id)} onClick={() => onMarkRead(item.id)}>
                    {pendingIds.includes(item.id) ? 'Saving…' : 'Mark as read'}
                  </Button>}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
