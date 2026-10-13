import { useEffect, useState } from 'react';
import { ArrowUpRightIcon } from '@phosphor-icons/react/dist/csr/ArrowUpRight';
import { KeyIcon } from '@phosphor-icons/react/dist/csr/Key';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import { BentoGrid, BentoCard } from '../../components/common/BentoGrid';
import { rentalClient } from '../../services/rentalService';
import { formatLKR } from '../../utils/currencyUtils';
import './Rentals.css';
const states = [['AwaitingPickup', 'Awaiting pickup'], ['Active', 'Active rentals'], ['PickupPending', 'Awaiting confirmation']];
export default function RentalQueuePage({ onNavigate }) {
  const [filter, setFilter] = useState({ state: 'AwaitingPickup', page: 1 });
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState(null);
  const key = `${filter.state}:${filter.page}:${revision}`;
  useEffect(() => {
    const controller = new AbortController();
    rentalClient.queue(filter.state, filter.page, controller.signal)
      .then(data => { if (!controller.signal.aborted) setResult({ key, data }); })
      .catch(error => { if (!controller.signal.aborted) setResult({ key, error: error.message }); });
    return () => controller.abort();
  }, [filter.state, filter.page, key]);
  const current = result?.key === key ? result : null;
  return <div className="rental-workspace">
    <div className="rental-page-heading"><div><p className="eyebrow">Vehicle handovers</p><h1>Every drive, accounted for.</h1><p>Record pickup details and keep active rentals in view.</p></div><Button variant="outline" disabled={!current} onClick={() => setRevision(r => r + 1)}>Refresh queue</Button></div>
    <BentoGrid className="rental-summary"><BentoCard><KeyIcon size={24} weight="regular" aria-hidden="true" /><div><span className="eyebrow">{states.find(s => s[0] === filter.state)[1]}</span><strong>{current?.data?.totalCount ?? '—'}</strong></div></BentoCard><BentoCard><div><h2>Before handing over the keys</h2><p>Confirm the booked dates, odometer, fuel and condition. Wait for a confirmed pickup before releasing the vehicle.</p></div></BentoCard></BentoGrid>
    <section className="rental-panel" aria-label="Rental queue"><div className="rental-toolbar"><label>Show<select value={filter.state} onChange={e => setFilter({ state: e.target.value, page: 1 })}>{states.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><span className="rental-meta">Times shown in your local timezone</span></div>
      {!current ? <p role="status" className="state-panel">Loading rental queue…</p> : current.error ? <Alert type="error" title="Rental queue unavailable" message={current.error} /> : !current.data.items.length ? <div className="state-panel"><h2>No rentals in this view.</h2><p>Confirmed reservations will appear under Awaiting pickup.</p></div> : <div className="rental-table-wrap"><table className="rental-table"><thead><tr><th>Vehicle / booking</th><th>Customer reference</th><th>Booked pickup</th><th>Booked return</th><th>Reserved amount</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{current.data.items.map(({ booking, rental }) => <tr key={booking.id}><td><strong>{booking.vehicle ? `${booking.vehicle.make} ${booking.vehicle.model}` : 'Vehicle details unavailable'}</strong><span className="rental-meta">{booking.vehicle?.licensePlate || 'Plate not recorded'} · #{booking.id.slice(0, 8)}</span></td><td className="rental-meta">#{booking.customerId.slice(0, 8)}</td><td>{new Date(booking.startDateTime).toLocaleString('en-GB')}</td><td>{new Date(booking.endDateTime).toLocaleString('en-GB')}</td><td>{formatLKR(booking.totalCost)}</td><td><Button variant="outline" size="sm" onClick={() => onNavigate('staff-rental-record', rental ? { rentalId: rental.id } : { bookingId: booking.id })}>Open record <ArrowUpRightIcon size={16} weight="regular" aria-hidden="true" /></Button></td></tr>)}</tbody></table></div>}
      {current?.data?.totalPages > 1 && <div className="rental-pagination"><Button variant="outline" size="sm" disabled={filter.page <= 1} onClick={() => setFilter(f => ({ ...f, page: f.page - 1 }))}>Previous</Button><span>Page {filter.page} of {current.data.totalPages}</span><Button variant="outline" size="sm" disabled={filter.page >= current.data.totalPages} onClick={() => setFilter(f => ({ ...f, page: f.page + 1 }))}>Next</Button></div>}
    </section>
  </div>;
}
