import React from 'react';
import Button from '../components/common/Button';
import { useAuth } from '../context/AuthContext';

const LandingPage = ({ onNavigate }) => {
  const { isAuthenticated } = useAuth();

  return (
    <div className="landing-page-container">
      {/* Hero Section */}
      <section className="hero-section">
        <div className="hero-content">
          <div className="hero-pill-tag">
            <span className="hero-pill-dot" aria-hidden="true" />
            <span>Modern Mobility Platform</span>
          </div>

          <h1 className="hero-title">
            Enterprise Fleet Intelligence & Premium Vehicle Access
          </h1>

          <p className="hero-subtitle">
            FleetFlow combines streamlined customer reservations with robust operational tools. Experience seamless vehicle access, real-time status tracking, and modern fleet management.
          </p>

          <div className="hero-cta-group">
            <Button
              variant="primary"
              size="lg"
              onClick={() => onNavigate('browse')}
            >
              Explore Vehicle Fleet
            </Button>

            {!isAuthenticated ? (
              <Button
                variant="outline"
                size="lg"
                onClick={() => onNavigate('register')}
              >
                Create Customer Account
              </Button>
            ) : (
              <Button
                variant="outline"
                size="lg"
                onClick={() => onNavigate('browse')}
              >
                Browse Fleet Catalog
              </Button>
            )}
          </div>
        </div>

        {/* Hero Decorative Vehicle Showcase Card */}
        <div className="hero-visual-card">
          <div className="visual-card-inner">
            <div className="visual-card-header">
              <div className="visual-card-badge">Fleet Category Showcase</div>
              <span className="visual-card-model">Fleet Mobility Spec</span>
            </div>

            <div className="visual-card-metrics">
              <div className="metric-item">
                <span className="metric-label">Access Model</span>
                <span className="metric-value">Self-Service</span>
              </div>
              <div className="metric-item">
                <span className="metric-label">Operational Roles</span>
                <span className="metric-value">4-Role RBAC</span>
              </div>
              <div className="metric-item">
                <span className="metric-label">Platform Status</span>
                <span className="metric-value status-active">Active</span>
              </div>
            </div>

            <div className="visual-card-footer">
              <div className="visual-indicator-bar">
                <span className="bar-segment active" />
                <span className="bar-segment active" />
                <span className="bar-segment" />
              </div>
              <span className="visual-note">Phase 1 Design Token Foundation</span>
            </div>
          </div>
        </div>
      </section>

      {/* Feature Pillar Cards */}
      <section className="features-section">
        <div className="section-header">
          <h2 className="section-title">Built for Modern Mobility Operations</h2>
          <p className="section-subtitle">Designed from the ground up to support both customers and operational staff teams.</p>
        </div>

        <div className="features-grid">
          <div className="feature-card">
            <div className="feature-icon-box">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.5 2.8C2.1 10.7 2 10.8 2 11v5c0 .6.4 1 1 1h2" />
                <circle cx="7" cy="17" r="2" />
                <path d="M9 17h6" />
                <circle cx="17" cy="17" r="2" />
              </svg>
            </div>
            <h3 className="feature-card-title">Comprehensive Fleet Catalog</h3>
            <p className="feature-card-text">
              Browse vehicle inventory with structured specifications including fuel types, passenger capacities, and live operational statuses.
            </p>
          </div>

          <div className="feature-card">
            <div className="feature-icon-box">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
            </div>
            <h3 className="feature-card-title">Four-Role RBAC Security</h3>
            <p className="feature-card-text">
              Strict identity separation protecting Customers, Fleet Managers, Maintenance Staff, and Administrators with verified JWT claims.
            </p>
          </div>

          <div className="feature-card">
            <div className="feature-icon-box">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <rect x="3" y="3" width="18" height="18" rx="2" />
                <line x1="3" y1="9" x2="21" y2="9" />
                <line x1="9" y1="21" x2="9" y2="9" />
              </svg>
            </div>
            <h3 className="feature-card-title">Dedicated Staff Portal</h3>
            <p className="feature-card-text">
              Operations personnel benefit from a tailored workspace for managing inventory, tracking maintenance, and monitoring fleet health.
            </p>
          </div>
        </div>
      </section>

      {/* Exploration Banner Section */}
      <section className="explore-banner-section">
        <div className="banner-box">
          <div className="banner-content">
            <h3 className="banner-title">Ready to Experience FleetFlow?</h3>
            <p className="banner-text">Sign up for a personal account or sign in to explore current vehicle offerings.</p>
          </div>
          <div className="banner-actions">
            <Button variant="primary" size="md" onClick={() => onNavigate('register')}>
              Get Started
            </Button>
            <Button variant="outline" size="md" onClick={() => onNavigate('login')}>
              Sign In
            </Button>
          </div>
        </div>
      </section>
    </div>
  );
};

export default LandingPage;
