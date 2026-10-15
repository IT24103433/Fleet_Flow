import ReportExportButton from './ReportExportButton';
import useReport from './useReport';
import { ReportShell, ReportMetrics, ReportTable, ReportNavigation } from './ReportComponents';
import { useState } from 'react';
import StatusBadge from '../../components/common/StatusBadge';
import { formatLKR } from '../../utils/currencyUtils';
import { formatReportMetric } from '../../utils/reportPermissions';

export function MaintenanceCost({ cost }) {
  return cost == null ? 'Not recorded' : formatLKR(cost);
}

export default function MaintenanceReportsPage() {
  const report = useReport('maintenance');
  const [status,setStatus]=useState('');
  const data = report.data;
  const records=(data?.workOrders?.records||[]).filter(row=>!status||row.status===status);
  const statuses=[...new Set((data?.workOrders?.records||[]).map(row=>row.status))];
  const costed=records.filter(row=>row.cost!=null);
  return <ReportShell title="Maintenance reports" report={report} navigation={<ReportNavigation/>} action={<ReportExportButton path="maintenance/export" filename="fleetflow-maintenance.csv" disabled={report.loading || !data?.workOrders?.available} />}>
    {data && <>
      <ReportMetrics metrics={[{ label: 'Vehicles currently in maintenance', value: formatReportMetric(data.currentVehicles.length) },{label:'Activities in this view',value:data.workOrders.available?formatReportMetric(records.length):'Unavailable'},{label:'Recorded cost in this view',value:data.workOrders.available&&costed.length?formatLKR(costed.reduce((sum,row)=>sum+row.cost,0)):'Not recorded'}]} />
      <h2>Current vehicle maintenance state</h2>
      <p>Vehicles currently marked for maintenance. Review work orders below for their schedule and activity history.</p>
      <ReportTable label="Vehicles with persisted maintenance status" rows={data.currentVehicles} rowKey="vehicleId" emptyMessage="No vehicles currently have maintenance status." columns={[
        { label: 'Vehicle', render: row => <><strong>{row.make} {row.model}</strong><small>{row.licensePlate}</small></> },
        { label: 'Hub', render: row => row.hubLocation },
        { label: 'Status', render: row => <StatusBadge status={row.status} /> },
        { label: 'Mileage', render: row => formatReportMetric(row.mileage) },
        { label: 'Last vehicle update', render: row => row.lastVehicleUpdateAt ? new Date(row.lastVehicleUpdateAt).toLocaleString() : 'Not recorded' },
      ]} />
      <h2>Maintenance activities & history</h2>
      <div className="report-toolbar"><label className="reports-filter">Activity status<select value={status} onChange={event=>setStatus(event.target.value)}><option value="">All activities</option>{statuses.map(value=><option key={value} value={value}>{value.toLowerCase().replace(/_/g," " ).replace(/\b\w/g,letter=>letter.toUpperCase())}</option>)}</select></label><a href="#/maintenance-dashboard">Manage work orders</a></div>
      {!data.workOrders.available ? <p className="reports-empty" role="status">{data.workOrders.unavailableReason}</p> : (
        <ReportTable label="Maintenance activities" rows={records} rowKey="maintenanceId" emptyMessage="No activities match this status." columns={[
          { label: 'Activity reference', render: row => <span title={row.maintenanceId}>#{row.maintenanceId.slice(0,8).toUpperCase()}</span> },
          { label: 'Vehicle', render: row => row.licensePlate || row.vehicleId },
          { label: 'Activity', render: row => row.activity },
          { label: 'Status', render: row => <StatusBadge status={row.status} /> },
          { label: 'Assigned to', render: row => row.assignedStaffName || 'Unassigned' },
          { label: 'Planned window', render: row => <>{row.scheduledAt ? new Date(row.scheduledAt).toLocaleString() : 'Not recorded'}{row.plannedEndDateTime&&<small>to {new Date(row.plannedEndDateTime).toLocaleString()}</small>}</> },
          { label: 'Completed', render: row => row.completedAt ? new Date(row.completedAt).toLocaleString() : 'Not recorded' },
          { label: 'Recorded cost', render: row => <MaintenanceCost cost={row.cost} /> },
          { label: 'History', render: row => row.history.length === 0 ? 'No recorded history' : <details><summary>{row.history.length} updates</summary><ul>{row.history.map((entry, index) => <li key={`${entry.changedAt}:${index}`}>{new Date(entry.changedAt).toLocaleString()} — {entry.status}{entry.details && `: ${entry.details}`}</li>)}</ul></details> },
        ]} />
      )}
      <details className="report-method"><summary>About maintenance costs and exports</summary><p>Costs include only activities with a recorded amount, including recorded zero values. CSV export contains the complete maintenance report, including activities hidden by the on-screen status filter. Dates in the export use UTC.</p></details>
    </>}
  </ReportShell>;
}
