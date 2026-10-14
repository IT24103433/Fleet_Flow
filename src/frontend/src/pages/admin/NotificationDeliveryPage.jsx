import { useEffect, useRef, useState } from 'react';
import { ArrowsClockwiseIcon } from '@phosphor-icons/react/dist/csr/ArrowsClockwise';
import { WarningCircleIcon } from '@phosphor-icons/react/dist/csr/WarningCircle';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import Modal from '../../components/common/Modal';
import EmptyState from '../../components/common/EmptyState';
import { BentoGrid, BentoCard } from '../../components/common/BentoGrid';
import { deliverySources } from '../../services/notificationOutboxService';
import './NotificationDeliveryPage.css';
const formatDate = value => value ? new Date(value).toLocaleString() : '—';
const eventLabel = value => ({ RentalCompletedEvent: 'Rental completed', RentalStartedEvent: 'Rental started', BookingCreatedEvent: 'Booking created', BookingCancelledEvent: 'Booking cancelled',
  MaintenanceScheduledEvent: 'Maintenance scheduled', MaintenanceStatusChangedEvent: 'Maintenance updated' })[value] || value;
export default function NotificationDeliveryPage() {
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState('');
  const [source, setSource] = useState(deliverySources[0].value);
  const client = deliverySources.find(item => item.value === source).client;
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const controller = new AbortController();
    client.getEvents(status, controller.signal).then(data => {
      if (!controller.signal.aborted) { setRows(data); setError(''); }
    }).catch(e => { if (!controller.signal.aborted) { setRows([]); setError(e.message); } })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [status, revision, client]);
  const refresh = (clearMessage = false) => { if (clearMessage) setSuccess(''); setLoading(true); setRevision(value => value + 1); };
  const replay = async () => {
    if (!selected || submittingRef.current) return;
    submittingRef.current = true; setSubmitting(true); setDialogError(''); setSuccess('');
    try {
      await client.replay(selected.id);
      if (mounted.current) { setSelected(null); setSuccess('Event queued for another delivery attempt.'); refresh(); }
    } catch (e) { if (mounted.current) setDialogError(e.message); }
    finally { submittingRef.current = false; if (mounted.current) setSubmitting(false); }
  };
  const metrics = [['Awaiting delivery', rows.filter(row => ['Pending', 'Retry'].includes(row.status)).length],
    ['Needs review', rows.filter(row => row.status === 'Failed').length], ['Delivered', rows.filter(row => row.status === 'Delivered').length]];
  return <section className="delivery-workspace" aria-labelledby="delivery-title">
    <div className="account-heading"><div><h1 id="delivery-title">Notification delivery</h1>
      <p>Review queued events, inspect delivery failures and safely retry an eligible event.</p></div>
      <Button variant="outline" onClick={() => refresh(true)} disabled={loading || submitting}>
        <ArrowsClockwiseIcon size={18} weight="regular" aria-hidden="true" /> Refresh</Button></div>
    <Alert type="error" message={error} /><Alert type="success" message={success} />
    <BentoGrid className="delivery-metrics" aria-label="Counts in the current view">
      {metrics.map(([label, value]) => <BentoCard key={label}><span className="stat-label">{label}</span>
        <strong className="workspace-metric">{loading || error ? '—' : value}</strong></BentoCard>)}
    </BentoGrid>
    <div className="delivery-table-panel"><div className="delivery-toolbar"><div><h2>Delivery queue</h2>
      <p>Latest 100 events. Counts reflect the selected source and current view.</p></div>
        <div className="delivery-filters">{deliverySources.length > 1 && <label htmlFor="delivery-source">Source
          <select id="delivery-source" value={source} disabled={submitting} onChange={e => {
            setLoading(true); setRows([]); setError(''); setSuccess(''); setSelected(null); setSource(e.target.value);
          }}>{deliverySources.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select>
        </label>}<label htmlFor="delivery-filter">Status
        <select id="delivery-filter" value={status} disabled={submitting} onChange={e => { setLoading(true); setSuccess(''); setStatus(e.target.value); }}>
          <option value="">All statuses</option>{['Pending', 'Retry', 'Failed', 'Delivered'].map(value => <option key={value}>{value}</option>)}
        </select></label></div></div>
      {loading ? <p className="delivery-loading" role="status">Loading delivery records…</p> : error ?
        <EmptyState title="Delivery records unavailable" description="Refresh when the service is available. No stale records are displayed." /> : !rows.length ?
          <EmptyState title="No delivery events in this view" description="New booking and maintenance events will appear after their business transaction is saved." /> :
          <div className="delivery-table-scroll"><table><thead><tr><th scope="col">Event</th><th scope="col">Status</th>
            <th scope="col">Attempts</th><th scope="col">Delivery details</th><th scope="col">Next attempt</th><th scope="col">Action</th></tr></thead>
            <tbody>{rows.map(row => <tr key={row.id}><td><strong>{eventLabel(row.eventType)}</strong><code title={row.id}>{row.id.slice(0, 8)}</code>
              <small>{formatDate(row.createdAt)}</small></td><td><span className={`delivery-status delivery-status-${row.status.toLowerCase()}`}>{row.status}</span></td>
              <td>{row.attempts}<small>{row.replayCount ? `${row.replayCount} replay${row.replayCount === 1 ? '' : 's'}` : ''}</small></td>
              <td><span>{row.inboxDeliveredAt ? 'Inbox saved' : 'Inbox pending'}</span><small>{row.lastErrorCode === 'InvalidEvent' ? 'Event needs investigation'
                : row.lastErrorCode ? 'Delivery unavailable' : row.kafkaPublishedAt ? 'Kafka acknowledged' : 'No delivery error recorded'}</small></td>
              <td>{['Pending', 'Retry'].includes(row.status) ? formatDate(row.nextAttemptAt) : '—'}</td><td>
                {row.status === 'Failed' ? <Button size="sm" variant="outline" disabled={submitting} onClick={() => { setSelected(row); setDialogError(''); }}
                  aria-label={`Replay ${eventLabel(row.eventType)} ${row.id.slice(0, 8)}`}>Replay</Button> : <span className="delivery-no-action">No action</span>}</td></tr>)}</tbody>
          </table></div>}
    </div>
    <Modal isOpen={!!selected} onClose={() => { if (!submitting) setSelected(null); }} title="Replay this notification?"
      subtitle="The worker will retry this saved event. Existing inbox entries and read states are preserved."
      footer={<><Button variant="outline" disabled={submitting} data-autofocus onClick={() => setSelected(null)}>Cancel</Button>
        <Button disabled={submitting} onClick={replay}>{submitting ? 'Queueing…' : 'Queue another attempt'}</Button></>}>
      <div className="delivery-replay-context"><WarningCircleIcon size={24} weight="regular" aria-hidden="true" />
        <div><strong>{selected && eventLabel(selected.eventType)}</strong><p>Only failed events without an active worker lease can be replayed.</p></div></div>
      {dialogError && <Alert type="error" message={dialogError} />}
    </Modal>
  </section>;
}
