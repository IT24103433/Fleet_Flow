export function pickupErrors(values, mileage = 0) {
  const errors = {};
  const odometer = Number(values.odometer);
  const fuel = Number(values.fuelPercentage);
  if (String(values.odometer).trim() === '' || !Number.isInteger(odometer) || odometer < Math.max(0, Number(mileage) || 0) || odometer > 2147483647)
    errors.odometer = `Enter a whole number at least ${Math.max(0, Number(mileage) || 0)} km.`;
  if (String(values.fuelPercentage).trim() === '' || !Number.isInteger(fuel) || fuel < 0 || fuel > 100)
    errors.fuelPercentage = 'Enter a whole percentage between 0 and 100.';
  if (!values.condition || values.condition.trim().length < 3 || values.condition.trim().length > 2000)
    errors.condition = 'Describe the vehicle condition in 3–2,000 characters.';
  return errors;
}
export function canStartPickup(booking, now = Date.now()) {
  return booking?.status === 'Confirmed' && !booking.rentalId && now >= new Date(booking.startDateTime).getTime() && now < new Date(booking.endDateTime).getTime();
}
