import { pickupErrors } from './pickupValidation.js';
export function returnErrors(values, minimumMileage = 0) {
  const errors = pickupErrors(values, minimumMileage);
  if (!/^\d+(?:\.\d{1,2})?$/.test(String(values.extraCharges)) || Number(values.extraCharges) > 1000000000)
    errors.extraCharges = 'Enter a non-negative LKR amount with at most two decimal places.';
  if (Number(values.extraCharges) > 0 && (values.extraChargeReason?.trim().length || 0) < 3)
    errors.extraChargeReason = 'Explain the additional charges in at least three characters.';
  if ((values.extraChargeReason?.trim().length || 0) > 500) errors.extraChargeReason = 'Keep the reason within 500 characters.';
  return errors;
}
