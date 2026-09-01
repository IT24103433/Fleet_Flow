import React from 'react';
import StatusBadge from '../components/common/StatusBadge';
import Button from '../components/common/Button';
import { useAuth } from '../context/AuthContext';

const MOCK_PREVIEW_MODELS = [
  {
    id: 'sedan-1',
    name: 'Aero Executive Sedan',
    category: 'Executive Sedan',
    transmission: 'Automatic',
    fuel: 'Hybrid',
    seats: 5,
    status: 'AVAILABLE',
  },
  {
    id: 'suv-1',
    name: 'Summit Pro SUV',
    category: 'Full-Size SUV',
    transmission: 'AWD Automatic',
    fuel: 'Electric',
    seats: 7,
    status: 'IN_USE',
  },
  {
    id: 'van-1',
    name: 'TransCarrier Cargo Van',
    category: 'Commercial Cargo',
    transmission: 'Automatic',
    fuel: 'Turbo Diesel',
    seats: 2,
    status: 'MAINTENANCE',
  },
];

const BrowseFleetPage = ({ onNavigate }) => {
  const { isAuthenticated } = useAuth();

  return (
    <div className="browse-fleet-container">
      <div className="catalog-banner">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
          <div>
            <h2 className="section-title">Vehicle Fleet Catalog</h2>
            <p className="section-subtitle">
              Explore available vehicle models and specifications. Real-time booking and live vehicle inventory endpoints will be connected in Phase 2.
            </p>
          </div>
          <span className="data-source-badge">Catalog Preview (Phase 1)</span>
        </div>
      </div>

      <div className="catalog-grid">
        {MOCK_PREVIEW_MODELS.map((item) => (
          <div key={item.id} className="vehicle-preview-card">
            <div className="vehicle-card-img-placeholder">
              <span>{item.category} Visual Spec</span>
            </div>
            <div className="vehicle-card-body">
              <div className="vehicle-card-header">
                <span className="vehicle-model-name">{item.name}</span>
                <StatusBadge status={item.status} />
              </div>

              <div className="vehicle-specs-row">
                <span>{item.fuel}</span>
                <span>•</span>
                <span>{item.transmission}</span>
                <span>•</span>
                <span>{item.seats} Seats</span>
              </div>

              <div className="vehicle-card-footer">
                <span className="vehicle-price-tag">
                  Fleet Standard Spec
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    if (!isAuthenticated) {
                      onNavigate('login');
                    }
                  }}
                  disabled={isAuthenticated}
                >
                  {isAuthenticated ? 'Reservations (Phase 2)' : 'Sign In to Book'}
                </Button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default BrowseFleetPage;
