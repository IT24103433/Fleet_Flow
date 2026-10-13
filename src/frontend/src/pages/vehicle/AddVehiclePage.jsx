import { CameraIcon } from '@phosphor-icons/react/dist/csr/Camera';
import { ArrowLeftIcon } from "@phosphor-icons/react/dist/csr/ArrowLeft";
import React, { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';
import { getCategories, createVehicle } from '../../services/vehicleService';
const HUB_OPTIONS = ['Colombo Fort Hub', 'Bandaranaike International Airport (CMB)', 'Colombo City - Galle Face', 'Colombo Port Logistics Terminal', 'Battaramulla Administrative Hub', 'Kandy Central Station', 'Jaffna Station'];
const TRANSMISSION_OPTIONS = ['Automatic', 'Manual', 'Single-Speed Fixed Gear'];
const FUEL_OPTIONS = ['100% Electric', 'Hybrid', 'Plug-in Hybrid', 'Gasoline', 'Diesel'];
const SEATING_OPTIONS = ['2 Passengers', '4 Passengers', '5 Passengers', '7 Passengers', '8+ Passengers'];
const AddVehiclePage = ({
  onNavigate
}) => {
  const {
    token,
    roles
  } = useAuth();
  const [categories, setCategories] = useState([]);
  const [isCategoriesLoading, setIsCategoriesLoading] = useState(true);
  const [formData, setFormData] = useState({
    vin: '',
    licensePlate: '',
    make: '',
    model: '',
    year: new Date().getFullYear().toString(),
    vehicleCategoryId: '',
    transmission: 'Automatic',
    fuelType: '100% Electric',
    seatingCapacity: '5 Passengers',
    dailyRate: '',
    hubLocation: 'Colombo Fort Hub',
    mileage: '0'
  });
  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Check RBAC permission for adding vehicles
  const userRoles = roles || [];
  const canAddVehicle = userRoles.some(r => ['FLEET_MANAGER', 'ADMIN'].includes(String(r).toUpperCase()));

  // Fetch Categories on Mount
  useEffect(() => {
    let isMounted = true;
    getCategories().then(result => {
      if (!isMounted) return;
      if (result.success && Array.isArray(result.data)) {
        setCategories(result.data);
        if (result.data.length > 0 && !formData.vehicleCategoryId) {
          setFormData(prev => ({
            ...prev,
            vehicleCategoryId: result.data[0].id
          }));
        }
      }
      setIsCategoriesLoading(false);
    });
    return () => {
      isMounted = false;
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleInputChange = e => {
    const {
      name,
      value
    } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    if (errors[name]) {
      setErrors(prev => ({
        ...prev,
        [name]: ''
      }));
    }
  };
  const validateForm = () => {
    const newErrors = {};
    const trimmedVin = formData.vin.trim().toUpperCase();
    if (!trimmedVin) {
      newErrors.vin = '17-character VIN is required.';
    } else if (trimmedVin.length !== 17 || !/^[A-Z0-9]{17}$/.test(trimmedVin)) {
      newErrors.vin = 'VIN must be exactly 17 alphanumeric characters.';
    }
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
  const handleFormSubmit = async e => {
    e.preventDefault();
    setNotice(null);
    const validationErrors = validateForm();
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
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
      mileage: parseInt(formData.mileage || '0', 10)
    };
    const result = await createVehicle(payload, token);
    setIsSubmitting(false);
    if (result.success) {
      setNotice({
        type: 'success',
        title: 'Vehicle added',
        message: `Inventory unit "${result.data.year} ${result.data.make} ${result.data.model}" (VIN: ${result.data.vin}) has been registered into the active fleet catalog.`
      });
      // Reset form fields while preserving defaults
      setFormData({
        vin: '',
        licensePlate: '',
        make: '',
        model: '',
        year: new Date().getFullYear().toString(),
        vehicleCategoryId: categories[0]?.id || '',
        transmission: 'Automatic',
        fuelType: '100% Electric',
        seatingCapacity: '5 Passengers',
        dailyRate: '',
        hubLocation: 'Colombo Fort Hub',
        mileage: '0'
      });
      setErrors({});
    } else {
      setNotice({
        type: 'error',
        title: result.status === 409 ? 'Vehicle already exists' : 'Could not add vehicle',
        message: result.message || 'An error occurred while creating the vehicle record.'
      });
    }
  };
  if (!canAddVehicle) {
    return <div className="add-vehicle-container">
        <div className="admin-page-header">
          <div>
            <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
              <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to Fleet Inventory
            </button>
            <h1 className="admin-page-title">Add a vehicle</h1>
          </div>
        </div>
        <div className="create-user-card view-b7dd1a6a">
          <Alert type="error" title="Access Restricted" message="Only authorized Fleet Managers and Administrators have permission to add new vehicles to the fleet catalog." />
          <div className="view-09342f6d">
            <Button variant="outline" onClick={() => onNavigate('manage-fleet')}>
              Return to Fleet Inventory
            </Button>
          </div>
        </div>
      </div>;
  }
  return <div className="add-vehicle-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            <ArrowLeftIcon size={18} weight="regular" aria-hidden="true" /> Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">Add a vehicle</h1>
          <p className="admin-page-subtitle">
            Enter the vehicle details, daily rate, and pickup location.
          </p>
        </div>
      </div>

      <div className="create-user-card view-2d2b9d4e">
        {notice && <div className="view-7257e5f1">
            <Alert type={notice.type} title={notice.title} message={notice.message} />
          </div>}

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          {/* Section 1: Identification */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">1. Vehicle Identification</h3>
            <div className="form-two-col">
              <InputField label="Vehicle Identification Number (VIN)" id="vin" name="vin" value={formData.vin} onChange={handleInputChange} error={errors.vin} placeholder="17-character alphanumeric VIN code" required disabled={isSubmitting} maxLength={17} />

              <InputField label="License Plate Number" id="licensePlate" name="licensePlate" value={formData.licensePlate} onChange={handleInputChange} error={errors.licensePlate} placeholder="e.g. WP-CAB-1001" required disabled={isSubmitting} />
            </div>

            <div className="form-three-col">
              <InputField label="Make / Manufacturer" id="make" name="make" value={formData.make} onChange={handleInputChange} error={errors.make} placeholder="e.g. Toyota, Honda, Nissan" required disabled={isSubmitting} />

              <InputField label="Model" id="model" name="model" value={formData.model} onChange={handleInputChange} error={errors.model} placeholder="e.g. Corolla" required disabled={isSubmitting} />

              <InputField label="Model Year" type="number" id="year" name="year" value={formData.year} onChange={handleInputChange} error={errors.year} required disabled={isSubmitting} min={1900} max={2100} />
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
                {isCategoriesLoading ? <div className="view-8477c082">
                    Loading vehicle categories…
                  </div> : <select id="vehicleCategoryId" name="vehicleCategoryId" value={formData.vehicleCategoryId} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isSubmitting}>
                    {categories.length === 0 && <option value="">No categories available</option>}
                    {categories.map(cat => <option key={cat.id} value={cat.id}>
                        {cat.name}
                      </option>)}
                  </select>}
                {errors.vehicleCategoryId && <span className="input-error-msg">{errors.vehicleCategoryId}</span>}
              </div>

              <InputField label="Daily Rental Rate (LKR / day)" type="number" id="dailyRate" name="dailyRate" value={formData.dailyRate} onChange={handleInputChange} error={errors.dailyRate} placeholder="e.g. 15000" required disabled={isSubmitting} step="0.01" min="0.01" />
            </div>

            <div className="form-three-col">
              <div className="form-group">
                <label htmlFor="fuelType" className="form-label">
                  Fuel type / Energy <span className="required-indicator">*</span>
                </label>
                <select id="fuelType" name="fuelType" value={formData.fuelType} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isSubmitting}>
                  {FUEL_OPTIONS.map(f => <option key={f} value={f}>{f}</option>)}
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="transmission" className="form-label">
                  Transmission <span className="required-indicator">*</span>
                </label>
                <select id="transmission" name="transmission" value={formData.transmission} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isSubmitting}>
                  {TRANSMISSION_OPTIONS.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="seatingCapacity" className="form-label">
                  Seating Capacity <span className="required-indicator">*</span>
                </label>
                <select id="seatingCapacity" name="seatingCapacity" value={formData.seatingCapacity} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isSubmitting}>
                  {SEATING_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>
            </div>
          </div>

          {/* Section 3: Operations & Assignment */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">3. Station Assignment & Odometer</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="hubLocation" className="form-label">
                  Assigned Location Hub <span className="required-indicator">*</span>
                </label>
                <select id="hubLocation" name="hubLocation" value={formData.hubLocation} onChange={handleInputChange} className="browse-select view-37ab52c9" disabled={isSubmitting}>
                  {HUB_OPTIONS.map(h => <option key={h} value={h}>{h}</option>)}
                </select>
              </div>

              <InputField label="Initial Odometer Reading (miles)" type="number" id="mileage" name="mileage" value={formData.mileage} onChange={handleInputChange} error={errors.mileage} disabled={isSubmitting} min="0" />
            </div>
          </div>

          {/* Section 4: Stitch Visual Treatment Notice */}
          <div className="form-section-block view-616e674a">
            <h3 className="section-subtitle-heading">4. Vehicle photographs</h3>
            <div className="view-fa74f59f">
              <div className="view-5dab92ee"><CameraIcon size={24} weight="regular" aria-hidden="true" /></div>
              <div>
                <strong className="view-0cee5651">
                  Add photographs after saving
                </strong>
                <p className="view-1b3281cb">
                  Save the vehicle, then open its photo gallery to upload clear photographs. Vehicles without uploaded photos display a photo unavailable placeholder.
                </p>
              </div>
            </div>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('manage-fleet')} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isSubmitting}>
              Add vehicle
            </Button>
          </div>
        </form>
      </div>
    </div>;
};
export default AddVehiclePage;
