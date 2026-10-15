import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import EmptyState from '../../components/common/EmptyState';
import './Reports.css';
import { useAuth } from '../../context/AuthContext';
import { canViewReport } from '../../utils/reportPermissions';

export function ReportShell({ title, report, children, action, navigation }) {
  const description = title.includes('Booking') ? 'Find reservations, review their status and open the handover record.' : title.includes('Maintenance') ? 'See which vehicles need attention and review recorded work and costs.' : 'Understand fleet availability, booking activity and maintenance in one view.';
  return (
    <section className="reports-page" aria-label={title}>
      <div className="reports-heading"><div><h1>{title}</h1><p>{description}</p></div>
        <div className="reports-heading-actions"><Button variant="outline" onClick={report.refresh} disabled={report.loading}>Refresh</Button>{action}</div>
      </div>
      {navigation}
      {report.loading && <div className="reports-loading" role="status"><span className="spinner" aria-hidden="true" /> Loading report…</div>}
      {report.error && <Alert type="error" title="Unable to Load Report" message={report.error} />}
      {report.data && <><p className="reports-timestamp">Retrieved <time dateTime={report.data.generatedAt}>{new Date(report.data.generatedAt).toLocaleString()}</time></p>{children}</>}
    </section>
  );
}

export function ReportNavigation() {
  const { roles } = useAuth();
  const view = window.location.hash.split('?')[0].replace('#/','');
  return <nav className="report-navigation" aria-label="Report views">{[['fleet-performance','Fleet overview'],['booking-reports','Bookings'],['maintenance-reports','Maintenance']].filter(([route])=>canViewReport(route,roles)).map(([route,label])=><a key={route} href={`#/${route}`} aria-current={view===route?'page':undefined}>{label}</a>)}</nav>;
}

export function ReportReference({ id, view, parameter, label }) {
  return <a className="report-record-link" href={`#/${view}?${parameter}=${encodeURIComponent(id)}`} title={id}>{label || `#${String(id).slice(0,8).toUpperCase()}`}</a>;
}

export function ReportMetrics({ metrics }) {
  return <div className="reports-metrics">{metrics.map(({ label, value }) => (
    <div key={label} className="stat-card"><span className="stat-label">{label}</span><strong className="reports-value">{value}</strong></div>
  ))}</div>;
}

export function ReportTable({ label, columns, rows, rowKey, emptyMessage }) {
  if (rows.length === 0) return <div className="reports-empty" role="status"><EmptyState badge="Report" title="No matching records" description={emptyMessage} /></div>;
  return <div className="reports-table-scroll"><table className="reports-table"><caption>{label}</caption>
    <thead><tr>{columns.map(column => <th scope="col" key={column.label}>{column.label}</th>)}</tr></thead>
    <tbody>{rows.map(row => <tr key={row[rowKey]}>{columns.map(column => <td key={column.label}>{column.render(row)}</td>)}</tr>)}</tbody>
  </table></div>;
}
