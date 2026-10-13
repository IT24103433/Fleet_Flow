import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import Button from '../../components/common/Button';
import StatusBadge from '../../components/common/StatusBadge';
import BookingModal from '../../components/BookingModal';
import { getVehicleImageUrl } from '../../utils/imageUrlUtils';
import { formatDailyRate } from '../../utils/currencyUtils';
import { routeHash } from '../../utils/navigation';
export default function VehicleDetailsPage({
  selectedVehicle: vehicle,
  onNavigate
}) {
  const {
    isAuthenticated
  } = useAuth();
  const [imageIndex, setImageIndex] = useState(0);
  const [failedImages, setFailedImages] = useState([]);
  const [bookingOpen, setBookingOpen] = useState(false);
  if (!vehicle) return <div className="record-empty"><h1>Choose a vehicle to continue</h1><Button onClick={() => onNavigate('browse')}>Browse vehicles</Button></div>;
  const name = `${vehicle.make} ${vehicle.model}`;
  const images = (vehicle.images || []).filter(image => image.relativeUrl);
  const image = images[imageIndex];
  const specs = [['Category', vehicle.categoryName], ['Seats', vehicle.seatingCapacity], ['Transmission', vehicle.transmission], ['Fuel type', vehicle.fuelType], ['Pickup location', vehicle.hubLocation], ['Model year', vehicle.year], ['Mileage', vehicle.mileage != null ? `${vehicle.mileage.toLocaleString()} mi` : null], ['Registration', vehicle.licensePlate]];
  const available = String(vehicle.status).toUpperCase() === 'AVAILABLE';
  const reserve = () => {
    if (!isAuthenticated) onNavigate('login', {
      returnTo: routeHash('vehicle-details', {
        vehicleId: vehicle.id
      })
    });else setBookingOpen(true);
  };
  return <div className="vehicle-details-container"><button type="button" className="back-link-btn" onClick={() => onNavigate('browse')}><ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to vehicles</button><div className="vehicle-main-grid"><section className="vehicle-gallery-col" aria-label="Vehicle photographs"><div className="hero-image-frame">{image && !failedImages.includes(image.id) ? <img src={getVehicleImageUrl(image.relativeUrl)} alt={`${vehicle.year} ${name}`} onError={() => setFailedImages(current => [...current, image.id])} /> : <div className="vehicle-photo-empty"><h2>{name}</h2><p>Photo unavailable</p></div>}</div>{images.length > 1 && <div className="thumbnail-gallery-strip">{images.map((photo, index) => <button type="button" key={photo.id} className={`thumb-btn ${index === imageIndex ? 'active' : ''}`} aria-label={`View photo ${index + 1}`} aria-pressed={index === imageIndex} onClick={() => setImageIndex(index)}><img src={getVehicleImageUrl(photo.relativeUrl)} alt={`${name}, view ${index + 1}`} loading="lazy" /></button>)}</div>}<div className="vehicle-details-note"><h2>Know what you’re booking.</h2><p>Review the recorded specifications and daily rate. Choose your rental dates to check availability and see an estimate.</p></div></section><section className="vehicle-summary-col"><div className="summary-card"><p className="eyebrow">{vehicle.categoryName || 'FleetFlow vehicle'}</p><h1>{name}</h1><p className="vehicle-year">{vehicle.year} · {vehicle.hubLocation || 'Location not recorded'}</p><StatusBadge status={vehicle.status} /><div className="daily-pricing-box"><strong>{formatDailyRate(vehicle.dailyRate)}</strong><span>Your estimate depends on your rental dates.</span></div><Button size="lg" fullWidth disabled={!available || vehicle.dailyRate == null} onClick={reserve}>{available ? isAuthenticated ? 'Choose dates & book' : 'Sign in to book' : 'Currently unavailable'}</Button><p className="reservation-note">Pending and confirmed bookings can be cancelled from My bookings.</p><dl className="vehicle-specifications">{specs.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? 'Not recorded'}</dd></div>)}</dl></div></section></div><BookingModal key={vehicle.id} isOpen={bookingOpen} onClose={() => setBookingOpen(false)} vehicle={vehicle} onNavigate={onNavigate} /></div>;
}
