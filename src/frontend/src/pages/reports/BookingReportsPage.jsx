import ReportExportButton from './ReportExportButton';
import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getAdminUsers } from '../../services/adminUserService';
import useReport from './useReport';
import { ReportShell, ReportTable, ReportMetrics, ReportNavigation, ReportReference } from './ReportComponents';
import StatusBadge from '../../components/common/StatusBadge';
import { formatLKR } from '../../utils/currencyUtils';

export default function BookingReportsPage() {
  const {token}=useAuth();const [directory,setDirectory]=useState(null);
  useEffect(()=>{let active=true;getAdminUsers(token).then(result=>{if(active)setDirectory({token,users:result.success?result.data:[]});});return()=>{active=false;};},[token]);
  const accounts=directory?.token===token?directory.users:[];
  const [status,setStatus]=useState(()=>{const value=new URLSearchParams(window.location.hash.split('?')[1]||'').get('status');return ['Pending','Confirmed','Cancelled','Completed'].includes(value)?value:'';});const [page,setPage]=useState(1);
  const query=new URLSearchParams({page:String(page),pageSize:'25'});if(status)query.set('status',status);
  const report=useReport(`bookings?${query}`);const overview=useReport('operational-statistics');const totals=overview.data?.bookings;
  return <ReportShell title="Booking reports" report={report} navigation={<ReportNavigation/>} action={<ReportExportButton path={`bookings/export${status?`?status=${encodeURIComponent(status)}`:''}`} filename="fleetflow-bookings.csv" disabled={report.loading||!report.data}/>}>
    {totals&&<section className="report-overview-strip" aria-label="All booking summary"><p>Across all bookings</p><ReportMetrics metrics={[{label:'Total bookings',value:totals.total.toLocaleString()},{label:'Confirmed',value:totals.confirmed.toLocaleString()},{label:'Completed',value:totals.completed.toLocaleString()},{label:'Cancelled',value:totals.cancelled.toLocaleString()}]}/></section>}
    <div className="report-toolbar"><div className="report-status-filters" aria-label="Filter bookings by status">{['','Pending','Confirmed','Cancelled','Completed'].map(value=><button key={value} type="button" aria-pressed={status===value} onClick={()=>{setStatus(value);setPage(1);}}>{value||'All bookings'}</button>)}</div><p>{report.data?.totalCount.toLocaleString()} matching {status?status.toLowerCase()+' ':''}bookings</p></div>
    <p className="report-help">Open a booking to review pickup, return and its timeline. Customer and vehicle links open their own records.</p>
    {report.data&&<><ReportTable label="Booking records" rows={report.data.items} rowKey="bookingId" emptyMessage="No bookings match this status. Choose another filter." columns={[
      {label:'Booking',render:row=><><ReportReference id={row.bookingId} view="staff-rental-record" parameter="bookingId"/><small>Created {new Date(row.createdAt).toLocaleDateString()}</small></>},
      {label:'Customer',render:row=>{const account=accounts.find(user=>user.id===row.customerId);return <><ReportReference id={row.customerId} view="admin-user-details" parameter="userId" label={account?.fullName||account?.username||`Customer ${String(row.customerId).slice(0,8).toUpperCase()}`}/>{account?.username&&<small>{account.username}</small>}</>; }},
      {label:'Vehicle',render:row=><><ReportReference id={row.vehicleId} view="staff-vehicle-details" parameter="vehicleId" label={[row.make,row.model].filter(Boolean).join(' ')||'View vehicle'}/><small>{row.licensePlate||'Plate not recorded'}</small></>},
      {label:'Status',render:row=><StatusBadge status={row.status}/>},
      {label:'Rental dates',render:row=><><span>{new Date(row.startDateTime).toLocaleString()}</span><small>to {new Date(row.endDateTime).toLocaleString()}</small></>},
      {label:'Booking estimate',render:row=><strong>{formatLKR(row.totalCost)}</strong>}
    ]}/><div className="reports-pagination"><span>Showing {report.data.items.length} of {report.data.totalCount} matching bookings</span><button type="button" className="btn btn-outline" disabled={page<=1} onClick={()=>setPage(n=>n-1)}>Previous</button><span>{report.data.totalPages?`Page ${page} of ${report.data.totalPages}`:'No pages'}</span><button type="button" className="btn btn-outline" disabled={page>=report.data.totalPages} onClick={()=>setPage(n=>n+1)}>Next</button></div><details className="report-method"><summary>About these amounts and exports</summary><p>Booking estimates are recorded reservation values, not money collected. Final return charges are shown in the rental record. CSV exports include all bookings matching the status across every page, with UTC timestamps and LKR amounts.</p></details></>}
  </ReportShell>;
}
