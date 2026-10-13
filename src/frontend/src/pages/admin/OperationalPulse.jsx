import React from 'react';
import useReport from '../reports/useReport';
import BookingDistribution from '../../components/common/BookingDistribution';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
export default function OperationalPulse({ onNavigate }) {
  const report = useReport('operational-statistics');
  return <section className="operational-pulse" aria-label="Operational analysis"><div className="dashboard-section-header"><h2>Operational analysis</h2><Button variant="outline" size="sm" onClick={()=>onNavigate('fleet-performance')}>Open full report</Button></div>
    {report.loading ? <p role="status">Loading booking activity…</p> : report.error ? <Alert type="error" message={report.error} actionText="Retry" onAction={report.refresh} /> : <BookingDistribution bookings={report.data?.bookings} />}
  </section>;
}
