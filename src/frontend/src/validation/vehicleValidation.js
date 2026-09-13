export const validateEditVehicle = (formData = {}) => {
  const errors = {};

  const vinTrimmed = (formData.vin || '').trim();
  if (!vinTrimmed) {
    errors.vin = 'Vehicle Identification Number (VIN) is required.';
  } else if (vinTrimmed.length !== 17) {
    errors.vin = 'VIN must be exactly 17 characters.';
  }

  const plateTrimmed = (formData.licensePlate || '').trim();
  if (!plateTrimmed) {
    errors.licensePlate = 'License plate number is required.';
  } else if (plateTrimmed.length > 20) {
    errors.licensePlate = 'License plate cannot exceed 20 characters.';
  }

  const makeTrimmed = (formData.make || '').trim();
  if (!makeTrimmed) {
    errors.make = 'Manufacturer / Make is required.';
  } else if (makeTrimmed.length > 50) {
    errors.make = 'Make cannot exceed 50 characters.';
  }

  const modelTrimmed = (formData.model || '').trim();
  if (!modelTrimmed) {
    errors.model = 'Model name is required.';
  } else if (modelTrimmed.length > 50) {
    errors.model = 'Model cannot exceed 50 characters.';
  }

  const yearNum = parseInt(formData.year, 10);
  const maxYear = new Date().getFullYear() + 2;
  if (!formData.year || isNaN(yearNum) || yearNum < 1900 || yearNum > maxYear) {
    errors.year = `Please enter a valid year between 1900 and ${maxYear}.`;
  }

  if (!formData.vehicleCategoryId) {
    errors.vehicleCategoryId = 'Please select a vehicle category.';
  }

  const rateNum = parseFloat(formData.dailyRate);
  if (!formData.dailyRate || isNaN(rateNum) || rateNum <= 0) {
    errors.dailyRate = 'Valid daily rental rate greater than LKR 0 required.';
  }

  const mileageNum = parseInt(formData.mileage, 10);
  if (isNaN(mileageNum) || mileageNum < 0) {
    errors.mileage = 'Odometer mileage must be 0 or greater.';
  }

  if (!formData.transmission || !formData.transmission.trim()) {
    errors.transmission = 'Transmission selection is required.';
  }

  if (!formData.fuelType || !formData.fuelType.trim()) {
    errors.fuelType = 'Powertrain / Fuel type selection is required.';
  }

  if (!formData.seatingCapacity || !formData.seatingCapacity.trim()) {
    errors.seatingCapacity = 'Seating capacity selection is required.';
  }

  if (!formData.hubLocation || !formData.hubLocation.trim()) {
    errors.hubLocation = 'Assigned location hub is required.';
  }

  return errors;
};
