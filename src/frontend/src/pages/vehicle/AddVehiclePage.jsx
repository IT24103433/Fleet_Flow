import React, { useState } from 'react';
import InputField from '../../components/InputField';
import Alert from '../../components/Alert';
import Button from '../../components/common/Button';

const CATEGORY_OPTIONS = [
  'Executive Sedan',
  'Full-Size SUV',
  'Commercial Cargo',
  'Compact EV',
  'Premium Coupe',
];

const HUB_OPTIONS = [
  'Metro Hub - Terminal A',
  'Uptown Station - Bay 12',
  'Logistics Depot - Gate 3',
  'Central Service Depot',
  'Airport Express Terminal',
];

const AddVehiclePage = ({ onNavigate }) => {
  const [formData, setFormData] = useState({
    vin: '',
    plate: '',
    make: '',
    model: '',
    year: '2025',
    category: 'Executive Sedan',
    transmission: 'Automatic',
    fuel: 'Plug-in Hybrid',
    seating: '5 Passengers',
    dailyRate: '85',
    hub: 'Metro Hub - Terminal A',
    mileage: '0',
    status: 'AVAILABLE',
  });

  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: '' }));
    }
  };

  const handleFormSubmit = (e) => {
    e.preventDefault();
    const newErrors = {};

    if (!formData.vin.trim()) newErrors.vin = '17-character VIN is required.';
    else if (formData.vin.trim().length !== 17) newErrors.vin = 'VIN must be exactly 17 characters.';

    if (!formData.plate.trim()) newErrors.plate = 'License plate number is required.';
    if (!formData.make.trim()) newErrors.make = 'Manufacturer / Make is required.';
    if (!formData.model.trim()) newErrors.model = 'Model name is required.';
    if (!formData.dailyRate || Number(formData.dailyRate) <= 0) newErrors.dailyRate = 'Valid positive daily rate required.';

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    setIsLoading(true);

    setTimeout(() => {
      setIsLoading(false);
      setNotice({
        type: 'info',
        title: 'Vehicle Ingestion Validated',
        message: `Inventory unit "${formData.year} ${formData.make} ${formData.model}" (VIN: ${formData.vin}) client parameters verified. POST /api/vehicles endpoint will connect to FleetService in Sprint 2.`,
      });
      setFormData({
        vin: '',
        plate: '',
        make: '',
        model: '',
        year: '2025',
        category: 'Executive Sedan',
        transmission: 'Automatic',
        fuel: 'Plug-in Hybrid',
        seating: '5 Passengers',
        dailyRate: '85',
        hub: 'Metro Hub - Terminal A',
        mileage: '0',
        status: 'AVAILABLE',
      });
    }, 600);
  };

  return (
    <div className="add-vehicle-container">
      <div className="admin-page-header">
        <div>
          <button type="button" className="back-link-btn" onClick={() => onNavigate('manage-fleet')}>
            ← Back to Fleet Inventory
          </button>
          <h1 className="admin-page-title">Ingest New Fleet Unit</h1>
          <p className="admin-page-subtitle">
            Register a new vehicle into the FleetFlow mobility management catalog.
          </p>
        </div>
      </div>

      <div className="create-user-card" style={{ maxWidth: '900px' }}>
        {notice && <Alert type={notice.type} title={notice.title} message={notice.message} />}

        <form onSubmit={handleFormSubmit} noValidate className="create-user-form">
          {/* Section 1: Identification */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">1. Vehicle Identification</h3>
            <div className="form-two-col">
              <InputField
                label="Vehicle Identification Number (VIN)"
                id="vin"
                name="vin"
                value={formData.vin}
                onChange={handleInputChange}
                error={errors.vin}
                placeholder="17-character VIN code"
                required
                disabled={isLoading}
              />

              <InputField
                label="License Plate Number"
                id="plate"
                name="plate"
                value={formData.plate}
                onChange={handleInputChange}
                error={errors.plate}
                placeholder="e.g. FL-902-XP"
                required
                disabled={isLoading}
              />
            </div>

            <div className="form-three-col">
              <InputField
                label="Make / Brand"
                id="make"
                name="make"
                value={formData.make}
                onChange={handleInputChange}
                error={errors.make}
                placeholder="e.g. Aero, Summit"
                required
                disabled={isLoading}
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
                disabled={isLoading}
              />

              <InputField
                label="Model Year"
                type="number"
                id="year"
                name="year"
                value={formData.year}
                onChange={handleInputChange}
                required
                disabled={isLoading}
              />
            </div>
          </div>

          {/* Section 2: Classification & Rates */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">2. Classification & Pricing</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="category" className="form-label">
                  Vehicle Category <span className="required-indicator">*</span>
                </label>
                <select
                  id="category"
                  name="category"
                  value={formData.category}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                >
                  {CATEGORY_OPTIONS.map((cat) => (
                    <option key={cat} value={cat}>{cat}</option>
                  ))}
                </select>
              </div>

              <InputField
                label="Daily Rental Rate ($ USD)"
                type="number"
                id="dailyRate"
                name="dailyRate"
                value={formData.dailyRate}
                onChange={handleInputChange}
                error={errors.dailyRate}
                placeholder="e.g. 85"
                required
                disabled={isLoading}
              />
            </div>

            <div className="form-two-col">
              <InputField
                label="Powertrain / Fuel Type"
                id="fuel"
                name="fuel"
                value={formData.fuel}
                onChange={handleInputChange}
                placeholder="e.g. 100% Electric, Hybrid"
                required
                disabled={isLoading}
              />

              <InputField
                label="Transmission"
                id="transmission"
                name="transmission"
                value={formData.transmission}
                onChange={handleInputChange}
                placeholder="e.g. Automatic, Single-Speed"
                required
                disabled={isLoading}
              />
            </div>
          </div>

          {/* Section 3: Operations & Assignment */}
          <div className="form-section-block">
            <h3 className="section-subtitle-heading">3. Station Assignment & Odometer</h3>
            <div className="form-two-col">
              <div className="form-group">
                <label htmlFor="hub" className="form-label">
                  Assigned Location Hub <span className="required-indicator">*</span>
                </label>
                <select
                  id="hub"
                  name="hub"
                  value={formData.hub}
                  onChange={handleInputChange}
                  className="browse-select"
                  style={{ width: '100%' }}
                >
                  {HUB_OPTIONS.map((h) => (
                    <option key={h} value={h}>{h}</option>
                  ))}
                </select>
              </div>

              <InputField
                label="Initial Odometer Reading (miles)"
                type="number"
                id="mileage"
                name="mileage"
                value={formData.mileage}
                onChange={handleInputChange}
                disabled={isLoading}
              />
            </div>
          </div>

          <div className="form-actions-bar">
            <Button variant="outline" onClick={() => onNavigate('manage-fleet')} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" isLoading={isLoading}>
              Ingest Vehicle Record
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AddVehiclePage;
