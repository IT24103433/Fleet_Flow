import { useState } from 'react';
import useReport from './useReport';
import { ReportShell, ReportTable } from './ReportComponents';
import StatusBadge from '../../components/common/StatusBadge';
import { formatLKR } from '../../utils/currencyUtils';

export default function BookingReportsPage() {
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const query = new URLSearchParams({ page: String(page), pageSize: '25' });
  if (status) query.set('status', status);
  const report = useReport(`bookings?${query}`);
  return <ReportShell title="Booking reports" report={report}>
    <label className="reports-filter">Booking status <select value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}>
      <option value="">All statuses</option>
      {['Pending', 'Confirmed', 'Cancelled', 'Completed'].map(value => <option key={value} value={value}>{value}</option>)}
    </select></label>
    {report.data && <>
      <p>{report.data.totalCount.toLocaleString()} matching persisted bookings. Customer identifiers are shown to administrators; personal profile details are not included.</p>
      <ReportTable label="Persisted bookings" rows={report.data.items} rowKey="bookingId" emptyMessage="No bookings match this report." columns={[
        { label: 'Booking reference', render: row => row.bookingId },
        { label: 'Customer ID', render: row => row.customerId },
        { label: 'Vehicle', render: row => <>{row.licensePlate || row.vehicleId}<br />{[row.make, row.model].filter(Boolean).join(' ')}</> },
        { label: 'Status', render: row => <StatusBadge status={row.status} /> },
        { label: 'Starts', render: row => new Date(row.startDateTime).toLocaleString() },
        { label: 'Ends', render: row => new Date(row.endDateTime).toLocaleString() },
        { label: 'Recorded cost', render: row => formatLKR(row.totalCost) },
      ]} />
      <div className="reports-pagination">
        <button type="button" className="btn btn-outline" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Previous</button>
        <span>{report.data.totalPages === 0 ? 'No pages' : `Page ${report.data.page} of ${report.data.totalPages}`}</span>
        <button type="button" className="btn btn-outline" disabled={page >= report.data.totalPages} onClick={() => setPage(value => value + 1)}>Next</button>
      </div>
    </>}
  </ReportShell>;
}
