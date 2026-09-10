/**
 * Sri Lankan Rupee (LKR) Currency Formatting Utilities
 *
 * Provides consistent currency formatting for vehicle rental pricing
 * across the FleetFlow platform.
 */

/**
 * Formats a numeric amount as Sri Lankan Rupees with thousands separators.
 * Example: formatLKR(15000) => "LKR 15,000"
 * Example: formatLKR(85) => "LKR 85"
 *
 * @param {number|string|null|undefined} amount
 * @returns {string} Formatted LKR currency string or "—" if missing/invalid
 */
export const formatLKR = (amount) => {
  if (amount === null || amount === undefined || amount === '') {
    return '—';
  }

  // If already formatted as LKR, return as-is
  if (typeof amount === 'string' && amount.trim().startsWith('LKR')) {
    return amount.trim();
  }

  const cleanVal = typeof amount === 'string' ? amount.replace(/[^0-9.-]+/g, '') : amount;
  const num = Number(cleanVal);
  if (isNaN(num)) {
    return '—';
  }

  return `LKR ${num.toLocaleString('en-US')}`;
};

/**
 * Formats a daily rental rate in Sri Lankan Rupees per day.
 * Preferred display format: "LKR 15,000/day"
 * Example: formatDailyRate(15000) => "LKR 15,000/day"
 * Example: formatDailyRate(85) => "LKR 85/day"
 *
 * @param {number|string|null|undefined} amount
 * @returns {string} Formatted daily rate string or "—" if missing/invalid
 */
export const formatDailyRate = (amount) => {
  if (amount === null || amount === undefined || amount === '') {
    return '—';
  }

  // If already formatted with LKR and /day, return as-is
  if (typeof amount === 'string' && amount.trim().startsWith('LKR') && amount.includes('/day')) {
    return amount.trim();
  }

  const cleanVal = typeof amount === 'string' ? amount.replace(/[^0-9.-]+/g, '') : amount;
  const num = Number(cleanVal);
  if (isNaN(num)) {
    return '—';
  }

  return `LKR ${num.toLocaleString('en-US')}/day`;
};

/**
 * Formats a numeric value with thousands separator.
 * Example: formatPriceNumber(15000) => "15,000"
 * Example: formatPriceNumber(85) => "85"
 *
 * @param {number|string|null|undefined} amount
 * @returns {string} Formatted number string or "—" if missing/invalid
 */
export const formatPriceNumber = (amount) => {
  if (amount === null || amount === undefined || amount === '') {
    return '—';
  }

  const cleanVal = typeof amount === 'string' ? amount.replace(/[^0-9.-]+/g, '') : amount;
  const num = Number(cleanVal);
  if (isNaN(num)) {
    return '—';
  }

  return num.toLocaleString('en-US');
};
