import React, { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import { getCategories, getVehicleById, updateVehicle } from '../../services/vehicleService';

const HUB_OPTIONS = [
  'Colombo Fort Hub',
  'Bandaranaike International Airport (CMB)',
  'Colombo City - Galle Face',
  'Colombo Port Logistics Terminal',
  'Battaramulla Administrative Hub',
  'Kandy Central Station',
  'Jaffna Station',
];

const TRANSMISSION_OPTIONS = ['Automatic', 'Manual', 'Single-Speed Fixed Gear'];
const FUEL_OPTIONS = ['100% Electric', 'Hybrid', 'Plug-in Hybrid', 'Gasoline', 'Diesel'];
const SEATING_OPTIONS = ['2 Passengers', '4 Passengers', '5 Passengers', '7 Passengers', '8+ Passengers'];

const EditVehiclePage = ({ selectedVehicle, onNavigate }) => {
  const { token, roles } = useAuth();
  const [categories, setCategories] = useState([]);
  const [isCategoriesLoading, setIsCategoriesLoading] = useState(true);
  const [isLoadingVehicle, setIsLoadingVehicle] = useState(false);

  const [formData, setFormData] = useState({
    vin: selectedVehicle?.vin || '',
    licensePlate: selectedVehicle?.licensePlate || '',
    make: selectedVehicle?.make || '',
    model: selectedVehicle?.model || '',
    year: selectedVehicle?.year?.toString() || new Date().getFullYear().toString(),
    vehicleCategoryId: selectedVehicle?.vehicleCategoryId || '',
    transmission: selectedVehicle?.transmission || 'Automatic',
    fuelType: selectedVehicle?.fuelType || '100% Electric',
    seatingCapacity: selectedVehicle?.seatingCapacity || '5 Passengers',
    dailyRate: selectedVehicle?.dailyRate?.toString() || '85',
    hubLocation: selectedVehicle?.hubLocation || 'Colombo Fort Hub',
    mileage: selectedVehicle?.mileage?.toString() || '0',
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Check RBAC permission for editing vehicles
  const userRoles = roles || [];
  const canEditVehicle = userRoles.some((r) => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));

  // Fetch Categories on Mount
  useEffect(() => {
    let isMounted = true;
    getCategories().then((result) => {
      if (!isMounted) return;
      if (result.success && Array.isArray(result.data)) {
        setCategories(result.data);
      }
      setIsCategoriesLoading(false);
    });
    return () => {
      isMounted = false;
    };
  }, []);

  // Fetch fresh vehicle data on mount
  useEffect(() => {
    let isMounted = true;
    if (selectedVehicle?.id) {
      getVehicleById(selectedVehicle.id).then((result) => {
        if (!isMounted) return;
        setIsLoadingVehicle(false);
        if (result.success && result.data) {
          const v = result.data;
          setFormData({
            vin: v.vin || '',
            licensePlate: v.licensePlate || '',
            make: v.make || '',
            model: v.model || '',
            year: v.year?.toString() || '',
            vehicleCategoryId: v.vehicleCategoryId || '',
            transmission: v.transmission || 'Automatic',
            fuelType: v.fuelType || '100% Electric',
            seatingCapacity: v.seatingCapacity || '5 Passengers',
            dailyRate: v.dailyRate != null ? v.dailyRate.toString() : '',
            hubLocation: v.hubLocation || 'Colombo Fort Hub',
            mileage: v.mileage != null ? v.mileage.toString() : '0',
          });
        }
      });
    }
    return () => {
      isMounted = false;
    };
  }, [selectedVehicle]);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: '' }));
    }
  };

  const validateForm = () => {
    const newErrors = {};

    if (!formData.licensePlate.trim()) {
      newErrors.licensePlate = 'License plate number is required.';
    }

    if (!formData.make.trim()) {
      newErrors.make = 'Manufacturer / Make is required.';
    }

    if (!formData.model.trim()) {
      newErrors.model = 'Model name is required.';
    }

    const yearNum = parseInt(formData.year, 10);
    if (!formData.year || isNaN(yearNum) || yearNum < 1900 || yearNum > 2100) {
      newErrors.year = 'Please enter a valid year between 1900 and 2100.';
    }

    if (!formData.vehicleCategoryId) {
      newErrors.vehicleCategoryId = 'Please select a vehicle category.';
    }

    const rateNum = parseFloat(formData.dailyRate);
    if (!formData.dailyRate || isNaN(rateNum) || rateNum <= 0) {
      newErrors.dailyRate = 'Valid daily rental rate greater than LKR 0 required.';
    }

    const mileageNum = parseInt(formData.mileage, 10);
    if (isNaN(mileageNum) || mileageNum < 0) {
      newErrors.mileage = 'Odometer mileage must be 0 or greater.';
    }

    return newErrors;
  };

  const handleFormSubmit = async (e) => {
    e.preventDefault();
    setNotice(null);

    const validationErrors = validateForm();
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }

    if (!selectedVehicle?.id) {
      setNotice({
        type: 'error',
        title: 'Vehicle Reference Error',
        message: 'No active vehicle reference selected for modification.',
      });
      return;
    }

    setIsSubmitting(true);

    const payload = {
      vin: formData.vin.trim().toUpperCase(),
      licensePlate: formData.licensePlate.trim().toUpperCase(),
      make: formData.make.trim(),
      model: formData.model.trim(),
      year: parseInt(formData.year, 10),
      vehicleCategoryId: formData.vehicleCategoryId,
      dailyRate: parseFloat(formData.dailyRate),
      transmission: formData.transmission,
      fuelType: formData.fuelType,
      seatingCapacity: formData.seatingCapacity,
      hubLocation: formData.hubLocation,
      mileage: parseInt(formData.mileage || '0', 10),
    };

    const result = await updateVehicle(selectedVehicle.id, payload, token);

    setIsSubmitting(false);

    if (result.success) {
      setNotice({
        type: 'success',
        title: 'Vehicle Specifications Updated',
        message: `Inventory unit "${result.data.year} ${result.data.make} ${result.data.model}" (VIN: ${result.data.vin}) has been successfully updated.`,
      });
    } else {
      setNotice({
        type: 'error',
        title: result.status === 409 ? 'Duplicate Record Conflict' : 'Failed to Update Vehicle',
        message: result.message || 'An error occurred while updating the vehicle record.',
      });
    }
  };

  if (!canEditVehicle) {
    return (
      <div className="add-vehicle-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              ← Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Edit Fleet Unit</h1>
          </div>
        </div>
        <div className="create-user-card" style={{ maxWidth: '800px' }}>
          <Alert
            type="error"
            title="Access Restricted"
            message="Only authorized Fleet Managers and Administrators have permission to edit fleet vehicle records."
          />
          <div style={{ marginTop: 'var(--space-4)' }}>
            <Button variant="outline" onClick={() => onNavigate('manage-fleet')}>
              Return to Fleet Inventory
            </Button>
          </div>
        </div>
      </div>
    );
  }

  if (isLoadingVehicle) {
    return (
      <div className="add-vehicle-container" style={{ textAlign: 'center', padding: 'var(--space-12) 0' }}>
        <div className="spinner" style={{ width: '32px', height: '32px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 12px' }} />
        <p>Retrieving vehicle record from FleetService...</p>
      </div>
    );
  }

  if (!selectedVehicle) {
    return (
      <div className="add-vehicle-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              ← Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Edit Fleet Unit</h1>
          </div>
        </div>
        <div className="create-user-card" style={{ maxWidth: '800px', textAlign: 'center', padding: 'var(--space-8)' }}>
          <h3>No Vehicle Selected</h3>
          <p style={{ color: 'var(--color-text-secondary)', marginBottom: '16px' }}>
            Please select a vehicle from the fleet inventory table to modify its specifications.
          </p>
          <Button variant="primary" onClick={() => onNavigate('manage-fleet')}>
            Return to Fleet Inventory
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="add-vehicle-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            ← Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">
            Edit Vehicle: {formData.year} {formData.make} {formData.model}
          </h1>
          <p className="admin-page-subtitle">
            Update operational specifications, assigned station hubs, or rental pricing tiers.
          </p>
        </div>
      </div>

      <div className="create-user-card" style={{ maxWidth: '900px' }}>
        {notice && (
          <div style={{ marginBottom: 'var(--space-6)' }}>
            <Alert type={notice.type} title={notice.title} message={notice.message} />
          </div>
        )}

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          {/* Section 1: Identification */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">1. Vehicle Identification</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="vin" className="form-label">
                  Vehicle Identification Number (VIN) <span style={{ fontSize: '11px', color: 'var(--color-text-muted)' }}>(Immutable)</span>
                </label>
                <input
                  type="text"
                  id="vin"
                  name="vin"
                  value={formData.vin}
                  readOnly
                  disabled
                  className="filter-search-input font-mono"
                  style={{ backgroundColor: 'var(--color-surface-hover)', cursor: 'not-allowed', width: '100%' }}
                />
              </div>

              <InputField
                label="License Plate Number"
                id="licensePlate"
                name="licensePlate"
                value={formData.licensePlate}
                onChange={handleInputChange}
                error={errors.licensePlate}
                placeholder="e.g. WP-CAB-1001"
                required
                disabled={isSubmitting}
              />
            </div>

            <div className="form-three-col">
              <InputField
                label="Make / Manufacturer"
                id="make"
                name="make"
                value={formData.make}
                onChange={handleInputChange}
                error={errors.make}
                placeholder="e.g. Aero, Summit, Tesla"
                required
                disabled={isSubmitting}
              />

              <InputField
                label="Model"
                id="model"
                name="model"
                value={formData.model}
                onChange={handleInputChange}
                error={errors.model}
                placeholder="e.g. Apex Executive"
                required
                disabled={isSubmitting}
              />

              <InputField
                label="Model Year"
                type="number"
                id="year"
                name="year"
                value={formData.year}
                onChange={handleInputChange}
                error={errors.year}
                required
                disabled={isSubmitting}
                min={1900}
                max={2100}
              />
            </div>
          </div>

          {/* Section 2: Classification & Rates */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">2. Classification & Pricing</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="vehicleCategoryId" className="form-label">
                  Vehicle Category <span className="required-indicator">*</span>
                </label>
                {isCategoriesLoading ? (
                  <div style={{ fontSize: '13px', color: 'var(--color-text-muted)', padding: '10px 0' }}>
                    Loading categories from FleetService...
                  </div>
                ) : (
                  <select
                    id="vehicleCategoryId"
                    name="vehicleCategoryId"
                    value={formData.vehicleCategoryId}
                    onChange={handleInputChange}
                    className="browse-select"
                    style={{ width: '100%' }}
                    disabled={isSubmitting}
                  >
                    {categories.length === 0 && <option value="">No categories available</option>}
                    {categories.map((cat) => (
                      <option key={cat.id} value={cat.id}>
                        {cat.name}
                      </option>
                    ))}
                  </select>
                )}
                {errors.vehicleCategoryId && (
                  <span className="input-error-msg">{errors.vehicleCategoryId}</span>
                )}
              </div>

              <InputField
                label="Daily Rental Rate (LKR / day)"
                type="number"
                id="dailyRate"
                name="dailyRate"
                value={formData.dailyRate}
                onChange={handleInputChange}
                error={errors.dailyRate}
                placeholder="e.g. 18500"
                required
                disabled={isSubmitting}
                step="0.01"
                min="0.01"
              />
            </div>

            <div className="form-three-col">
              <div className="form-group">
                <label htmlFor="fuelType" className="form-label">
                  Powertrain / Energy <span className="required-indicator">*</span>
                </label>
                <select
                  id="fuelType"
                  name="fuelType"
                  value={formData.fuelType}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                  disabled={isSubmitting}
                >
                  {FUEL_OPTIONS.map((f) => (
                    <option key={f} value={f}>{f}</option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="transmission" className="form-label">
                  Transmission <span className="required-indicator">*</span>
                </label>
                <select
                  id="transmission"
                  name="transmission"
                  value={formData.transmission}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                  disabled={isSubmitting}
                >
                  {TRANSMISSION_OPTIONS.map((t) => (
                    <option key={t} value={t}>{t}</option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="seatingCapacity" className="form-label">
                  Seating Capacity <span className="required-indicator">*</span>
                </label>
                <select
                  id="seatingCapacity"
                  name="seatingCapacity"
                  value={formData.seatingCapacity}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                  disabled={isSubmitting}
                >
                  {SEATING_OPTIONS.map((s) => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {/* Section 3: Operations & Assignment */}
          <div className="form-section-block" style={{ borderBottom: 'none' }}>
            <h3 className="section-subtitle-heading">3. Station Assignment & Odometer</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="hubLocation" className="form-label">
                  Assigned Location Hub <span className="required-indicator">*</span>
                </label>
                <select
                  id="hubLocation"
                  name="hubLocation"
                  value={formData.hubLocation}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                  disabled={isSubmitting}
                >
                  {HUB_OPTIONS.map((h) => (
                    <option key={h} value={h}>{h}</option>
                  ))}
                </select>
              </div>

              <InputField
                label="Current Odometer Reading (miles)"
                type="number"
                id="mileage"
                name="mileage"
                value={formData.mileage}
                onChange={handleInputChange}
                error={errors.mileage}
                disabled={isSubmitting}
                min="0"
              />
            </div>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('manage-fleet')} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isSubmitting}>
              Save Vehicle Changes
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default EditVehiclePage;
