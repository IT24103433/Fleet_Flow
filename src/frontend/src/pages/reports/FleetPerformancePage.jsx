import useReport from './useReport';
import { ReportShell, ReportMetrics } from './ReportComponents';
import { formatReportMetric } from '../../utils/reportPermissions';
import { formatLKR } from '../../utils/currencyUtils';

export default function FleetPerformancePage() {
  const report = useReport('operational-statistics');
  const { fleet, bookings, maintenance } = report.data || {};
  return <ReportShell title="Fleet performance & analytics" report={report}>
    {fleet && <>
      <h2>Fleet status</h2>
      <ReportMetrics metrics={[
        { label: 'Total vehicles', value: formatReportMetric(fleet.vehicles.total) },
        { label: 'Available', value: formatReportMetric(fleet.vehicles.available) },
        { label: 'In use', value: formatReportMetric(fleet.vehicles.inUse) },
        { label: 'In maintenance', value: formatReportMetric(fleet.vehicles.maintenance) },
        { label: 'Retired', value: formatReportMetric(fleet.vehicles.retired) },
        { label: 'Currently booked vehicles', value: formatReportMetric(fleet.currentlyBookedVehicles) },
        { label: 'Current booking utilization', value: formatReportMetric(fleet.currentBookingUtilizationPercent, '%') },
      ]} />
      <p>Utilization counts distinct non-retired vehicles with a confirmed booking covering the report timestamp, divided by all non-retired vehicles. It measures booking coverage. An empty eligible fleet has no utilization percentage.</p>
      {fleet.vehicles.total === 0 && <p className="reports-empty" role="status">No persisted vehicles found.</p>}
      <h2>Booking statistics</h2>
      <ReportMetrics metrics={[
        { label: 'Total bookings', value: formatReportMetric(bookings.total) },
        { label: 'Active (pending + confirmed)', value: formatReportMetric(bookings.active) },
        { label: 'Pending', value: formatReportMetric(bookings.pending) },
        { label: 'Confirmed', value: formatReportMetric(bookings.confirmed) },
        { label: 'Cancelled', value: formatReportMetric(bookings.cancelled) },
        { label: 'Completed', value: formatReportMetric(bookings.completed) },
        { label: 'Non-cancelled booking value', value: formatLKR(bookings.nonCancelledBookingValue) },
      ]} />
      <p>Booking value is the sum of recorded costs for pending, confirmed and completed bookings. It is not a payment or revenue total.</p>
      {bookings.total === 0 && <p className="reports-empty" role="status">No persisted bookings found.</p>}
      <h2>Maintenance statistics</h2>
      <ReportMetrics metrics={[
        { label: 'Vehicles in maintenance', value: formatReportMetric(maintenance.vehiclesInMaintenance) },
        { label: 'Recorded work orders', value: formatReportMetric(maintenance.workOrderCount) },
        { label: 'Work orders with recorded costs', value: formatReportMetric(maintenance.costedWorkOrderCount) },
        { label: 'Recorded maintenance costs', value: maintenance.recordedCostTotal == null ? 'Unavailable' : formatLKR(maintenance.recordedCostTotal) },
      ]} />
      {!maintenance.workOrderDataAvailable && <p role="status">{maintenance.unavailableReason}</p>}
      {maintenance.workOrderDataAvailable && <p>Maintenance costs include only work orders with a recorded cost.</p>}
    </>}
  </ReportShell>;
}
