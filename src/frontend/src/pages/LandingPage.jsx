import React, { useEffect, useRef, useState } from 'react';
import { ArrowUpRightIcon } from '@phosphor-icons/react/dist/csr/ArrowUpRight';
import { ArrowRightIcon } from '@phosphor-icons/react/dist/csr/ArrowRight';
import { CarProfileIcon } from '@phosphor-icons/react/dist/csr/CarProfile';
import { CalendarDotsIcon } from '@phosphor-icons/react/dist/csr/CalendarDots';
import { TicketIcon } from '@phosphor-icons/react/dist/csr/Ticket';
import { ArrowDownIcon } from '@phosphor-icons/react/dist/csr/ArrowDown';
import ShimmerButton from '../components/aura/ShimmerButton';
import { BentoGrid } from '../components/common/BentoGrid';
import Button from '../components/common/Button';
import VehicleCard from '../components/common/VehicleCard';
import Alert from '../components/Alert';
import HeroMedia from '../components/aura/HeroMedia';
import useAuraMotion from '../components/aura/useAuraMotion';
import { useAuth } from '../context/AuthContext';
import { getRoleDefaultView } from '../utils/roleUtils';
import { getVehicles } from '../services/vehicleService';
import SupportEnquiryForm from '../components/onboarding/SupportEnquiryForm';

const steps = [
  { title: 'Choose your vehicle.', copy: 'Compare seating, transmission, fuel type, and daily rates.', Icon: CarProfileIcon },
  { title: 'Set your dates.', copy: 'Select pickup and return times. We check availability before you book.', Icon: CalendarDotsIcon },
  { title: 'Keep it in view.', copy: 'Find reservation details and eligible cancellation options in My bookings.', Icon: TicketIcon },
];

export default function LandingPage({ onNavigate }) {
  const scope = useRef(null);
  const { isAuthenticated, roles } = useAuth();
  const [fleet, setFleet] = useState(null);
  useAuraMotion(scope, Boolean(fleet));
  useEffect(() => {
    let active = true;
    getVehicles({ status: 'Available', pageSize: 3, sortBy: 'createdAt', sortOrder: 'desc' }).then(result => { if (active) setFleet(result); });
    return () => { active = false; };
  }, []);
  const accountView = isAuthenticated ? getRoleDefaultView(roles) : 'login';
  return <div ref={scope} className="landing-page cinematic-landing">
    <section className="cinematic-hero" aria-labelledby="landing-title">
      <HeroMedia />
      <div className="cinematic-intro">
        <p className="hero-editorial-note">A different perspective on every journey.</p>
        <h1 id="landing-title">Your next drive.<br /><span>Make it yours.</span></h1>
        <p>Find your vehicle. Choose your dates.<br />Keep every booking detail in view.</p>
        <div className="action-row">
          <ShimmerButton onClick={() => onNavigate('browse')}>Explore the fleet <ArrowUpRightIcon size={20} weight="regular" aria-hidden="true" /></ShimmerButton>
          <Button size="lg" variant="outline" onClick={() => onNavigate(accountView)}>{isAuthenticated ? (accountView === 'customer-home' ? 'My bookings' : 'Open workspace') : 'Sign in'}</Button>
        </div>
      </div>
      <div className="hero-scroll-cue"><ArrowDownIcon size={18} weight="regular" aria-hidden="true" /><span>Scroll to explore</span></div>
    </section>
    <section className="landing-section cinematic-guide" aria-labelledby="guide-title"><div className="section-heading-row"><h2 id="guide-title">Good journeys start<br />with a clear plan.</h2><p>Choose your vehicle first.<br />Bring the rest of your plans together.</p></div><BentoGrid className="cinematic-step-grid" data-marketing-entrance>{steps.map((step,index) => <article key={step.title}><span className="booking-step-number">0{index+1}</span><step.Icon size={28} weight="regular" aria-hidden="true" /><h3>{step.title}</h3><p>{step.copy}</p></article>)}</BentoGrid></section>
    <section className="landing-section aura-fleet" aria-labelledby="fleet-title">
      <div className="section-heading-row"><div><p className="eyebrow">Explore the fleet</p><h2 id="fleet-title">Find a vehicle that<br />fits your plans.</h2><p>Compare the details that matter before you book.</p></div><Button variant="outline" onClick={() => onNavigate('browse')}>View all vehicles <ArrowUpRightIcon size={18} weight="regular" aria-hidden="true" /></Button></div>
      {!fleet ? <p role="status" className="state-panel">Loading the fleet…</p> : !fleet.success ? <Alert type="error" title="Fleet unavailable" message="We couldn’t load vehicles right now. Open the fleet catalogue to try again." actionText="Browse vehicles" onAction={() => onNavigate('browse')} /> : fleet.data.length ? <div className="vehicle-grid" data-marketing-entrance>{fleet.data.slice(0, 3).map(vehicle => <VehicleCard key={vehicle.id} vehicle={vehicle} onSelect={selected => onNavigate('vehicle-details', { vehicle: selected })} />)}</div> : <div className="state-panel"><h3>No available vehicles right now.</h3><p>Check the catalogue again later.</p><Button variant="outline" onClick={() => onNavigate('browse')}>Open catalogue</Button></div>}
    </section>
    <section className="landing-section aura-benefits" aria-labelledby="benefits-title"><div><p className="eyebrow">Clarity at every step</p><h2 id="benefits-title">Less searching.<br />More certainty.</h2></div><div className="aura-benefit-rows"><article><span className="aura-feature-icon"><CarProfileIcon size={26} weight="regular" aria-hidden="true" /></span><div><h3>Compare the details</h3><p>Vehicle specifications and LKR daily rates, together in one place.</p></div></article><article><span className="aura-feature-icon"><CalendarDotsIcon size={26} weight="regular" aria-hidden="true" /></span><div><h3>Check your dates</h3><p>Review the estimated cost and availability before you submit.</p></div></article><article><span className="aura-feature-icon"><TicketIcon size={26} weight="regular" aria-hidden="true" /></span><div><h3>Manage your bookings</h3><p>Your reference, dates, and status stay together in your account.</p></div></article></div></section>
    <section className="landing-section faq-section" aria-labelledby="faq-title"><div><p className="eyebrow">Before your next drive</p><h2 id="faq-title">A few useful<br />answers.</h2></div><div className="faq-list"><details><summary>How do I book a vehicle?</summary><p>Choose a vehicle from the catalogue, select pickup and return dates, and sign in to submit your booking. We check availability for that vehicle and rental period.</p></details><details><summary>How is the rental estimate calculated?</summary><p>The estimate uses the vehicle’s daily rate and billable rental days. A partial day counts as a full day. Review the estimate before you submit.</p></details><details><summary>Can I cancel my booking?</summary><p>Pending and confirmed bookings can be cancelled from My bookings. Cancelled reservations remain in your history.</p></details></div></section>
    <SupportEnquiryForm />
    <section className="final-cta cinematic-final"><div><h2>Ready for your<br />next journey?</h2><p>Start with the vehicle. Keep the details in view.</p></div><Button size="lg" onClick={() => onNavigate('browse')}>Explore the fleet <ArrowRightIcon size={20} weight="regular" aria-hidden="true" /></Button></section>
  </div>;
}
