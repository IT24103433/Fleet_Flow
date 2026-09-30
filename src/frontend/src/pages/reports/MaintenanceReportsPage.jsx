import useReport from './useReport';
import { ReportShell, ReportMetrics, ReportTable } from './ReportComponents';
import StatusBadge from '../../components/common/StatusBadge';
import { formatLKR } from '../../utils/currencyUtils';
import { formatReportMetric } from '../../utils/reportPermissions';

export function MaintenanceCost({ cost }) {
  return cost == null ? 'Not recorded' : formatLKR(cost);
}

export default function MaintenanceReportsPage() {
  const report = useReport('maintenance');
  const data = report.data;
  return <ReportShell title="Maintenance reports" report={report}>
    {data && <>
      <ReportMetrics metrics={[{ label: 'Vehicles currently in maintenance', value: formatReportMetric(data.currentVehicles.length) }]} />
      <h2>Current vehicle maintenance state</h2>
      <p>These rows describe current vehicle records. The last vehicle update does not establish when a maintenance activity started.</p>
      <ReportTable label="Vehicles with persisted maintenance status" rows={data.currentVehicles} rowKey="vehicleId" emptyMessage="No vehicles currently have maintenance status." columns={[
        { label: 'Vehicle', render: row => <>{row.licensePlate}<br />{row.make} {row.model}<br />{row.vehicleId}</> },
        { label: 'Hub', render: row => row.hubLocation },
        { label: 'Status', render: row => <StatusBadge status={row.status} /> },
        { label: 'Mileage', render: row => formatReportMetric(row.mileage) },
        { label: 'Last vehicle update', render: row => row.lastVehicleUpdateAt ? new Date(row.lastVehicleUpdateAt).toLocaleString() : 'Not recorded' },
      ]} />
      <h2>Maintenance activities & history</h2>
      {!data.workOrders.available ? <p className="reports-empty" role="status">{data.workOrders.unavailableReason}</p> : (
        <ReportTable label="Persisted maintenance activities" rows={data.workOrders.records} rowKey="maintenanceId" emptyMessage="No persisted maintenance activities found." columns={[
          { label: 'Activity reference', render: row => row.maintenanceId },
          { label: 'Vehicle', render: row => row.licensePlate || row.vehicleId },
          { label: 'Activity', render: row => row.activity },
          { label: 'Status', render: row => <StatusBadge status={row.status} /> },
          { label: 'Scheduled', render: row => row.scheduledAt ? new Date(row.scheduledAt).toLocaleString() : 'Not recorded' },
          { label: 'Completed', render: row => row.completedAt ? new Date(row.completedAt).toLocaleString() : 'Not recorded' },
          { label: 'Recorded cost', render: row => <MaintenanceCost cost={row.cost} /> },
          { label: 'History', render: row => row.history.length === 0 ? 'No recorded history' : <ul>{row.history.map((entry, index) => <li key={`${entry.changedAt}:${index}`}>{new Date(entry.changedAt).toLocaleString()} — {entry.status}{entry.details && `: ${entry.details}`}</li>)}</ul> },
        ]} />
      )}
    </>}
  </ReportShell>;
}
