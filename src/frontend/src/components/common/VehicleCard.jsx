import { ArrowUpRightIcon } from "@phosphor-icons/react/dist/csr/ArrowUpRight";
import { CarProfileIcon } from "@phosphor-icons/react/dist/csr/CarProfile";
import React, { useEffect, useState } from 'react';
import Button from './Button';
import StatusBadge from './StatusBadge';
import { formatDailyRate } from '../../utils/currencyUtils';
import { vehiclePhoto } from '../../utils/vehiclePresentation';
import { getVehicleImages } from '../../services/vehicleImageService';
export default function VehicleCard({
  vehicle,
  onSelect
}) {
  const [failed, setFailed] = useState(null);
  const [imageResult, setImageResult] = useState(null);
  const embeddedPhoto = vehiclePhoto(vehicle);
  useEffect(() => {
    let active = true;
    if (embeddedPhoto || !vehicle.id) return;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);
    getVehicleImages(vehicle.id, undefined, controller.signal).then(response => {
      if (active) setImageResult({
        id: vehicle.id,
        images: response.success ? response.data : []
      });
    }).finally(() => clearTimeout(timeout));
    return () => {
      active = false;
      controller.abort();
      clearTimeout(timeout);
    };
  }, [vehicle.id, embeddedPhoto]);
  const photo = embeddedPhoto || vehiclePhoto({
    images: imageResult?.id === vehicle.id ? imageResult.images : []
  });
  const loadingPhoto = !embeddedPhoto && imageResult?.id !== vehicle.id;
  const name = `${vehicle.make} ${vehicle.model}`;
  return <article className="vehicle-card">
      <div className="vehicle-card-media">
        {photo && failed !== photo ? <img src={photo} alt={name} loading="lazy" onError={() => setFailed(photo)} /> : <div className="vehicle-photo-empty"><CarProfileIcon size={20} weight="regular" aria-hidden="true" /><span>{loadingPhoto ? 'Loading photo…' : 'Photo unavailable'}</span></div>}
        <span className="vehicle-card-category">{vehicle.categoryName || vehicle.category || 'Vehicle'}</span>
      </div>
      <div className="vehicle-card-content">
        <div className="vehicle-card-title"><h3>{name}</h3><span>{vehicle.year}</span></div>
        <p className="vehicle-card-specs"><span>{vehicle.seatingCapacity || vehicle.seating || '—'} seats</span><span>{vehicle.transmission || '—'}</span><span>{vehicle.fuelType || vehicle.fuel || '—'}</span></p>
        <StatusBadge status={vehicle.status} />
        <div className="vehicle-card-bottom"><strong>{formatDailyRate(vehicle.dailyRate)}</strong><Button variant="outline" size="sm" onClick={() => onSelect(vehicle)}>View details <span aria-hidden="true"><ArrowUpRightIcon size={18} weight="regular" aria-hidden="true" /></span></Button></div>
      </div>
    </article>;
}
