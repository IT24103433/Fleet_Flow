import { getVehicleImageUrl } from './imageUrlUtils';

export function vehiclePhoto(vehicle) {
  const images = [...(vehicle?.images || [])].filter(image => image.relativeUrl);
  images.sort((a, b) => String(a.createdAt || '').localeCompare(String(b.createdAt || '')) || String(a.id || '').localeCompare(String(b.id || '')));
  return images.length ? getVehicleImageUrl(images[0].relativeUrl) : null;
}

export function bookingEstimate(start, end, rate) {
  const startMs = new Date(start).getTime();
  const endMs = new Date(end).getTime();
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) return { days: 0, cost: null };
  const days = Math.ceil((endMs - startMs) / 86400000);
  const cost = rate != null && Number.isFinite(Number(rate)) && Number(rate) >= 0 ? days * Number(rate) : null;
  return { days, cost };
}
