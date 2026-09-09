import React, { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import RoleBadge from '../../components/common/RoleBadge';
import StatusBadge from '../../components/common/StatusBadge';
import Button from '../../components/common/Button';
import { getVehicles } from '../../services/vehicleService';
import { formatDailyRate } from '../../utils/currencyUtils';

const CustomerHomePage = ({ onNavigate, onSelectVehicle }) => {
  const { user, roles } = useAuth();
  const primaryRole = roles?.[0] || 'CUSTOMER';

  const [recommendedVehicles, setRecommendedVehicles] = useState([]);
  const [isLoadingVehicles, setIsLoadingVehicles] = useState(true);

  useEffect(() => {
    let isMounted = true;
    getVehicles({ status: 'Available', pageSize: 4 }).then((result) => {
      if (!isMounted) return;
      if (result.success && Array.isArray(result.data)) {
        setRecommendedVehicles(result.data);
      } else {
        setRecommendedVehicles([]);
      }
      setIsLoadingVehicles(false);
    });
    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <div className="customer-home-container">
      {/* Customer Header Banner */}
      <div className="customer-hero-card">
        <div className="customer-hero-left">
          <div className="customer-status-pill">
            <span className="live-dot" aria-hidden="true" />
            <span>Active Customer Session</span>
          </div>
          <h1 className="customer-welcome-title">Welcome, {user?.username}!</h1>
          <p className="customer-welcome-subtitle">
            Explore our curated vehicle catalog, manage active reservations, or update your account details.
          </p>
          <div className="customer-meta-strip">
            <RoleBadge role={primaryRole} />
            <span className="customer-email-tag">{user?.email || 'Registered Customer'}</span>
          </div>
        </div>

        <div className="customer-hero-actions">
          <Button variant="primary" size="lg" onClick={() => onNavigate('browse')}>
            Browse Available Fleet
          </Button>
          <Button variant="outline" size="lg" onClick={() => onNavigate('customer-profile')}>
            Account Profile & Security
          </Button>
        </div>
      </div>

      {/* Quick Operations Shortcuts */}
      <div className="home-shortcuts-grid">
        <div className="home-shortcut-card" onClick={() => onNavigate('browse')} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && onNavigate('browse')}>
          <div className="shortcut-icon-mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
              <circle cx="7" cy="17" r="2" />
              <path d="M9 17h6" />
              <circle cx="17" cy="17" r="2" />
            </svg>
          </div>
          <div className="shortcut-card-text">
            <h4>Explore Vehicle Fleet</h4>
            <p>Browse full catalog, filter by category, and inspect detailed vehicle specifications.</p>
          </div>
        </div>

        <div className="home-shortcut-card" onClick={() => onNavigate('customer-profile')} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && onNavigate('customer-profile')}>
          <div className="shortcut-icon-mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
          </div>
          <div className="shortcut-card-text">
            <h4>Profile & Password Security</h4>
            <p>Manage personal details, upload profile photo preview, and update credentials.</p>
          </div>
        </div>
      </div>

      {/* Active Reservations Section (Awaiting Booking API) */}
      <section className="home-section">
        <div className="section-header-row">
          <div>
            <h2 className="section-heading">Active & Upcoming Reservations</h2>
            <p className="section-subtext">Real-time booking and dispatch status</p>
          </div>
          <span className="data-source-badge">Awaiting Booking API (Sprint 2)</span>
        </div>

        <div className="empty-reservations-box">
          <div className="empty-icon-circle">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
              <line x1="16" y1="2" x2="16" y2="6" />
              <line x1="8" y1="2" x2="8" y2="6" />
              <line x1="3" y1="10" x2="21" y2="10" />
            </svg>
          </div>
          <h3>No Active Bookings Found</h3>
          <p>
            You do not currently have any active vehicle reservations. Browse our vehicle fleet catalog to select an executive model.
          </p>
          <Button variant="primary" size="md" onClick={() => onNavigate('browse')}>
            Reserve a Vehicle
          </Button>
        </div>
      </section>

      {/* Featured Fleet Recommendations */}
      <section className="home-section">
        <div className="section-header-row">
          <div>
            <h2 className="section-heading">Recommended Vehicles</h2>
            <p className="section-subtext">Featured executive and high-efficiency models</p>
          </div>
          <button type="button" className="link-btn-subtle" onClick={() => onNavigate('browse')}>
            View All Vehicles →
          </button>
        </div>

        {isLoadingVehicles && (
          <div style={{ textAlign: 'center', padding: 'var(--space-6) 0', color: 'var(--color-text-secondary)' }}>
            <div className="spinner" style={{ width: '24px', height: '24px', borderTopColor: 'var(--color-primary)', borderRightColor: 'var(--color-primary)', margin: '0 auto 8px' }} />
            <p style={{ fontSize: '13px' }}>Loading fleet recommendations...</p>
          </div>
        )}

        {!isLoadingVehicles && recommendedVehicles.length === 0 && (
          <div className="empty-state-card" style={{ padding: 'var(--space-6)', textAlign: 'center' }}>
            <p style={{ color: 'var(--color-text-secondary)', marginBottom: '12px' }}>
              Browse our complete vehicle catalog to discover and reserve available mobility units.
            </p>
            <Button variant="primary" size="sm" onClick={() => onNavigate('browse')}>
              Browse Vehicle Catalog
            </Button>
          </div>
        )}

        {!isLoadingVehicles && recommendedVehicles.length > 0 && (
          <div className="recommended-grid">
            {recommendedVehicles.map((vehicle) => {
              const vehicleName = `${vehicle.year} ${vehicle.make} ${vehicle.model}`;
              const categoryName = vehicle.categoryName || vehicle.category || '—';
              const fuel = vehicle.fuelType || vehicle.fuel || '—';
              const transmission = vehicle.transmission || '—';

              return (
                <div key={vehicle.id} className="recommended-card">
                  <div className="rec-img-placeholder">
                    <span>{categoryName} Visual Model</span>
                  </div>
                  <div className="rec-body">
                    <div className="rec-title-row">
                      <h4 className="rec-name">{vehicleName}</h4>
                      <StatusBadge status={vehicle.status} />
                    </div>
                    <div className="rec-specs">
                      <span>{fuel}</span>
                      <span>•</span>
                      <span>{transmission}</span>
                    </div>
                    <div className="rec-footer">
                      <span className="rec-rate">{formatDailyRate(vehicle.dailyRate)}</span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          if (onSelectVehicle) {
                            onSelectVehicle(vehicle);
                          }
                          onNavigate('vehicle-details');
                        }}
                      >
                        View Details
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
};

export default CustomerHomePage;
