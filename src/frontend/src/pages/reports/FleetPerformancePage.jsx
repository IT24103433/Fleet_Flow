import useReport from './useReport';
import { ReportShell, ReportMetrics, ReportNavigation } from './ReportComponents';
import { formatReportMetric } from '../../utils/reportPermissions';
import { formatLKR } from '../../utils/currencyUtils';
import FleetDistribution from '../../components/common/FleetDistribution';
import BookingDistribution from '../../components/common/BookingDistribution';

export default function FleetPerformancePage(){
  const report=useReport('operational-statistics');const {fleet,bookings,maintenance}=report.data||{};
  return <ReportShell title="Fleet performance & analytics" report={report} navigation={<ReportNavigation/>}>
    {fleet&&<><ReportMetrics metrics={[{label:'Vehicles available now',value:formatReportMetric(fleet.vehicles.available)},{label:'Confirmed bookings',value:formatReportMetric(bookings.confirmed)},{label:'Current booking utilization',value:formatReportMetric(fleet.currentBookingUtilizationPercent,'%')},{label:'Vehicles in maintenance',value:formatReportMetric(maintenance.vehiclesInMaintenance)}]}/>
      <div className="report-analysis-grid"><section className="report-analysis-panel"><div className="report-panel-heading"><div><h2>Fleet availability</h2><p>Current status across {fleet.vehicles.total} vehicles.</p></div><a href="#/manage-fleet">Open inventory</a></div><FleetDistribution heading={false} counts={{totalVehicles:fleet.vehicles.total,available:fleet.vehicles.available,inUse:fleet.vehicles.inUse,undergoingMaintenance:fleet.vehicles.maintenance,retired:fleet.vehicles.retired}}/></section>
      <section className="report-analysis-panel"><div className="report-panel-heading"><div><h2>Booking activity</h2><p>All recorded reservations, grouped by current status.</p></div><a href="#/booking-reports">Open bookings</a></div><BookingDistribution heading={false} bookings={bookings}/></section></div>
      <section className="report-financial-summary"><div><h2>Recorded values</h2><p>Reservation estimates and documented maintenance costs. These figures do not represent payments collected.</p></div><ReportMetrics metrics={[{label:'Non-cancelled booking value',value:formatLKR(bookings.nonCancelledBookingValue)},{label:'Recorded maintenance costs',value:maintenance.recordedCostTotal==null?'Unavailable':formatLKR(maintenance.recordedCostTotal)},{label:'Work orders with a recorded cost',value:formatReportMetric(maintenance.costedWorkOrderCount)}]}/></section>
      <div className="report-next-actions"><a href="#/booking-reports?status=Pending"><strong>{formatReportMetric(bookings.pending)} pending bookings</strong><span>Review reservation records</span></a><a href="#/maintenance-reports"><strong>{formatReportMetric(maintenance.workOrderCount)} maintenance activities</strong><span>Explore schedules, assignments and history</span></a><a href="#/manage-fleet"><strong>{formatReportMetric(fleet.vehicles.retired)} retired vehicles</strong><span>Review inventory status</span></a></div>
      {!maintenance.workOrderDataAvailable&&<p role="status">{maintenance.unavailableReason}</p>}
      <details className="report-method"><summary>How these figures are calculated</summary><p>Booking utilization counts non-retired vehicles covered by a confirmed booking at the report timestamp, divided by all non-retired vehicles. It does not measure engine running time. No eligible vehicles means the percentage is unavailable.</p><p>Non-cancelled booking value includes pending, confirmed and completed reservation estimates. It is not revenue. Maintenance totals include only activities with a recorded cost; missing costs are not treated as zero.</p></details>
    </>}
  </ReportShell>;
}
