import './Reports.css';

export function ReportShell({ title, report, children }) {
  return (
    <section className="reports-page" aria-label={title}>
      <div className="reports-heading"><div><h1>{title}</h1><p>Latest available persisted FleetFlow data.</p></div>
        <button type="button" className="btn btn-outline" onClick={report.refresh} disabled={report.loading}>Refresh</button>
      </div>
      {report.loading && <p role="status">Loading report…</p>}
      {report.error && <p role="alert">{report.error}</p>}
      {report.data && <><p className="reports-timestamp">Retrieved <time dateTime={report.data.generatedAt}>{new Date(report.data.generatedAt).toLocaleString()}</time></p>{children}</>}
    </section>
  );
}

export function ReportMetrics({ metrics }) {
  return <div className="reports-metrics">{metrics.map(({ label, value }) => (
    <div key={label} className="stat-card"><span className="stat-label">{label}</span><strong className="reports-value">{value}</strong></div>
  ))}</div>;
}

export function ReportTable({ label, columns, rows, rowKey, emptyMessage }) {
  if (rows.length === 0) return <p className="reports-empty" role="status">{emptyMessage}</p>;
  return <div className="reports-table-scroll"><table className="reports-table"><caption>{label}</caption>
    <thead><tr>{columns.map(column => <th scope="col" key={column.label}>{column.label}</th>)}</tr></thead>
    <tbody>{rows.map(row => <tr key={row[rowKey]}>{columns.map(column => <td key={column.label}>{column.render(row)}</td>)}</tr>)}</tbody>
  </table></div>;
}
