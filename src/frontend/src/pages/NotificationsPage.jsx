import { useEffect, useState } from 'react';
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

  return (
    <section className="notification-inbox" aria-labelledby="notification-title">
      <div className="notification-heading">
        <div><h1 id="notification-title">Notifications</h1><p>Booking, maintenance and account updates.</p></div>
        <button type="button" className="btn btn-outline" onClick={() => { setLoading(true); setRefreshKey(key => key + 1); }}>Refresh</button>
      </div>
      {error && <p role="alert">{error}</p>}
      {loading ? <p role="status">Loading notifications…</p> : (
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
                  {!item.isRead && <button type="button" className="btn btn-outline" disabled={pendingIds.includes(item.id)} onClick={() => markRead(item.id)}>
                    {pendingIds.includes(item.id) ? 'Saving…' : 'Mark as read'}
                  </button>}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
