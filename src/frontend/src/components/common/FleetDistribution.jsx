import React from 'react';
import { BentoCard } from './BentoGrid';
const segments = [['Available', 'available', '#7540bf'], ['In use', 'inUse', '#20232b'], ['Maintenance', 'undergoingMaintenance', '#d18a21'], ['Retired', 'retired', '#c7c8ce']];
export default function FleetDistribution({ counts, heading = true }) {
  if (!counts || !segments.every(([,key]) => Number.isFinite(counts[key]))) return null;
  const total = segments.reduce((sum, [,key]) => sum + counts[key], 0);
  const arcs = segments.map(([label,key,color], index) => {
    const percent = total ? counts[key] / total * 100 : 0;
    const preceding = segments.slice(0,index).reduce((sum,[,previous])=>sum+counts[previous],0);
    return { label, value: counts[key], color, percent, offset: total ? preceding/total*100 : 0 };
  });
  return <BentoCard className="fleet-distribution" aria-label="Current fleet status distribution">
    {heading && <div className="dashboard-section-header"><div><h3>Fleet readiness</h3><p>Current vehicle status</p></div><span className="live-data-label">Live overview</span></div>}
    <div className="distribution-body"><div className="distribution-chart">
      <svg viewBox="0 0 120 120" role="img" aria-label={arcs.map(a => `${a.label}: ${a.value}`).join(', ')}><circle cx="60" cy="60" r="46" fill="none" stroke="#efeff2" strokeWidth="12" />{arcs.filter(a=>a.value>0).map(a=><circle key={a.label} cx="60" cy="60" r="46" fill="none" stroke={a.color} strokeWidth="12" pathLength="100" strokeDasharray={`${a.percent} ${100-a.percent}`} strokeDashoffset={-a.offset} transform="rotate(-90 60 60)" />)}</svg>
      <div className="distribution-total"><strong>{total}</strong><span>vehicles</span></div></div>
      <ul>{arcs.map(a=><li key={a.label}><span className="distribution-dot" style={{background:a.color}} aria-hidden="true" /><span>{a.label}</span><strong>{a.value}</strong><span>{total ? Math.round(a.percent) : 0}%</span></li>)}</ul>
    </div>
    <p className="distribution-note">{heading ? 'Status distribution includes retired records. Booking utilisation is available in fleet reports.' : 'Retired vehicles remain in historical records and are included in this distribution.'}</p>
  </BentoCard>;
}
