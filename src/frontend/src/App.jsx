import React, { useState } from 'react';
import RegisterPage from './pages/RegisterPage';
import LoginPage from './pages/LoginPage';
import { AuthProvider, useAuth } from './context/AuthContext';
import './App.css';

const ROLE_CONFIG = {
  CUSTOMER: {
    title: 'Customer',
    badgeClass: 'badge-customer',
    description: 'Personal account and booking access.',
  },
  FLEET_MANAGER: {
    title: 'Fleet Manager',
    badgeClass: 'badge-fleet-manager',
    description: 'Fleet management and vehicle operation privileges.',
  },
  ADMIN: {
    title: 'Administrator',
    badgeClass: 'badge-admin',
    description: 'System-wide administration privileges.',
  },
};

const getRoleDetails = (roles) => {
  const primaryRole = roles?.[0]?.toUpperCase();
  if (primaryRole && ROLE_CONFIG[primaryRole]) {
    return ROLE_CONFIG[primaryRole];
  }
  return {
    title: 'User',
    badgeClass: 'badge-unknown',
    description: 'Standard authenticated account.',
  };
};

function AppContent() {
  const { isAuthenticated, user, roles, logout, isLoading } = useAuth();
  const [view, setView] = useState('login');

  if (isLoading) {
    return (
      <main className="app-container">
        <div className="register-card" style={{ textAlign: 'center' }}>
          <p>Loading application state...</p>
        </div>
      </main>
    );
  }

  if (isAuthenticated) {
    const roleDetails = getRoleDetails(roles);

    return (
      <main className="app-container">
        <div className="register-card success-card">
          <div className="success-icon-container" style={{ background: 'rgba(170, 59, 255, 0.1)', color: 'var(--accent)' }}>
            <svg className="success-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>
          <h2 className="success-title">
            Welcome, {user?.username}!
            <span className={`role-badge ${roleDetails.badgeClass}`}>
              {roleDetails.title}
            </span>
          </h2>
          <div className="success-details">
            <p><strong>Email:</strong> {user?.email}</p>
            <p><strong>Role:</strong> {roleDetails.title}</p>
            <p><strong>Permissions:</strong> {roleDetails.description}</p>
          </div>
          <div className="success-actions">
            <button className="btn btn-primary btn-block" onClick={logout}>
              Log Out
            </button>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="app-container">
      {view === 'login' ? (
        <LoginPage onNavigateToRegister={() => setView('register')} />
      ) : (
        <RegisterPage onNavigateToLogin={() => setView('login')} />
      )}
    </main>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;

