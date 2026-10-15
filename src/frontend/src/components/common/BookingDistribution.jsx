import React from 'react';
import { BentoCard } from './BentoGrid';
const states = [['Pending','pending','#c6953b'],['Confirmed','confirmed','#7641b0'],['Completed','completed','#3d796d'],['Cancelled','cancelled','#a4a0ae']];
export default function BookingDistribution({ bookings, heading = true }) {
  if (!bookings || !states.every(([,key])=>Number.isFinite(bookings[key]))) return null;
  const total = states.reduce((sum,[,key])=>sum+bookings[key],0);
  return <BentoCard className="booking-distribution" aria-label="Booking status overview">{heading && <><h3>Booking activity</h3><p>All recorded reservations</p></>}
    <div className="booking-total"><strong>{total.toLocaleString()}</strong><span>bookings</span></div>
    <div className="booking-status-track" aria-hidden="true">{states.map(([label,key,color])=><span key={key} title={label} style={{width:`${total ? bookings[key]/total*100 : 0}%`,background:color}} />)}</div>
    <ul>{states.map(([label,key,color])=><li key={key}><span className="distribution-dot" style={{background:color}} aria-hidden="true" /><span>{label}</span><strong>{bookings[key].toLocaleString()}</strong></li>)}</ul>
    {total===0 && <p>No bookings have been recorded yet.</p>}
  </BentoCard>;
}
