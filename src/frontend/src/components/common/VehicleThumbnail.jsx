import React, { useEffect, useState } from 'react';
import { CarProfileIcon } from '@phosphor-icons/react/dist/csr/CarProfile';
import { getVehicleImages } from '../../services/vehicleImageService';
import { vehiclePhoto } from '../../utils/vehiclePresentation';
import UploadedImage from './UploadedImage';

export default function VehicleThumbnail({ vehicle, token }) {
  const [result, setResult] = useState(null);
  const embedded = vehiclePhoto(vehicle);
  useEffect(() => {
    if (embedded || !vehicle.id) return;
    let active = true;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);
    getVehicleImages(vehicle.id, token, controller.signal).then(response => {
      if (active) setResult({ id: vehicle.id, images: response.success ? response.data : [] });
    }).finally(() => clearTimeout(timeout));
    return () => { active = false; controller.abort(); clearTimeout(timeout); };
  }, [vehicle.id, embedded, token]);
  const src = embedded || vehiclePhoto({ images: result?.id === vehicle.id ? result.images : [] });
  return <UploadedImage src={src} alt={`${vehicle.make} ${vehicle.model}`} className="inventory-thumbnail" fallback={<CarProfileIcon size={26} weight="regular" aria-hidden="true" />} />;
}
