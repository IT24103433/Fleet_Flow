import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getMaintenanceDashboard } from '../../services/maintenanceService';
import { updateVehicleStatus } from '../../services/vehicleService';
import {
  validateStatusChange,
  getAvailableStatusTransitionsForRoles,
} from '../../validation/vehicleStatusValidation';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import Modal from '../../components/common/Modal';

const HUB_LOCATIONS = [
  'All Hubs',
  'Colombo Fort Hub',
  'Kandy Central Hub',
  'Galle Coastal Hub',
  'Negombo Airport Hub',
];

const MaintenanceDashboardPage = ({ onNavigate, onSelectVehicle }) => {
  const { token, roles } = useAuth();

  const [dashboardData, setDashboardData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);
  const [selectedHub, setSelectedHub] = useState('All Hubs');
  const [tableSearchTerm, setTableSearchTerm] = useState('');

  // Status Change Modal State
  const [statusModalVehicle, setStatusModalVehicle] = useState(null);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);

  const fetchDashboard = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);

    const params = {};
    if (selectedHub && selectedHub !== 'All Hubs') {
      params.hub = selectedHub;
    }

    const result = await getMaintenanceDashboard(token, params);
    setIsLoading(false);

    if (result.success && result.data) {
      setDashboardData(result.data);
    } else {
      setErrorMessage(result.message || 'Failed to retrieve maintenance data.');
    }
  }, [token, selectedHub]);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  // Role-filtered status options for modal
  const allowedStatusOptions = useMemo(() => {
    return getAvailableStatusTransitionsForRoles(roles);
  }, [roles]);

  const selectedStatusDesc = useMemo(() => {
    return allowedStatusOptions.find((o) => o.value === targetStatus)?.description || '';
  }, [allowedStatusOptions, targetStatus]);

  const handleOpenStatusModal = (vehicle) => {
    setStatusModalVehicle(vehicle);
    const nextDefault = allowedStatusOptions.find((s) => s.value !== vehicle.status)?.value || allowedStatusOptions[0]?.value || '';
    setTargetStatus(nextDefault);
    setStatusModalError(null);
  };

  const handleCloseStatusModal = () => {
    if (isUpdatingStatus) return;
    setStatusModalVehicle(null);
    setTargetStatus('');
    setStatusModalError(null);
  };

  const handleConfirmStatusChange = async () => {
    if (!statusModalVehicle) return;

    const validation = validateStatusChange(targetStatus, statusModalVehicle.status, roles);
    if (!validation.isValid) {
      setStatusModalError(validation.error);
      return;
    }

    setIsUpdatingStatus(true);
    setStatusModalError(null);

    const result = await updateVehicleStatus(statusModalVehicle.id, targetStatus, token);
    setIsUpdatingStatus(false);

    if (result.success) {
      setStatusSuccessMessage(
        `Vehicle ${statusModalVehicle.licensePlate || statusModalVehicle.vin} status updated to "${targetStatus}".`
      );
      setStatusModalVehicle(null);
      setTimeout(() => setStatusSuccessMessage(null), 5000);
      // Immediately refresh live persisted counts and attention queue
      fetchDashboard();
    } else {
      setStatusModalError(result.message || 'Failed to update vehicle status.');
    }
  };

  // Filter maintenance vehicles table
  const filteredMaintenanceVehicles = useMemo(() => {
    const list = dashboardData?.maintenanceVehicles || [];
    if (!tableSearchTerm.trim()) return list;

    const term = tableSearchTerm.toLowerCase().trim();
    return list.filter((v) =>
      (v.make && v.make.toLowerCase().includes(term)) ||
      (v.model && v.model.toLowerCase().includes(term)) ||
      (v.vin && v.vin.toLowerCase().includes(term)) ||
      (v.licensePlate && v.licensePlate.toLowerCase().includes(term)) ||
      (v.hubLocation && v.hubLocation.toLowerCase().includes(term))
    );
  }, [dashboardData, tableSearchTerm]);

  const counts = dashboardData?.statusCounts || {
    totalVehicles: 0,
    undergoingMaintenance: 0,
    available: 0,
    inUse: 0,
    retired: 0,
  };

  const attentionItems = dashboardData?.attentionItems || [];

  return (
    <div className="manage-fleet-container" style={{ paddingBottom: '3rem' }}>
      {/* Top Header */}
      <div className="admin-page-header" style={{ marginBottom: '1.5rem' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '3px 8px',
                borderRadius: '12px',
                backgroundColor: 'rgba(234, 179, 8, 0.12)',
                color: '#b45309',
                fontSize: '11px',
                fontWeight: 600,
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
              }}
            >
              <span
                style={{
                  width: '6px',
                  height: '6px',
                  borderRadius: '50%',
                  backgroundColor: '#eab308',
                }}
              />
              Fleet Telematics & Service
            </span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
              Live Persisted Data
            </span>
          </div>
          <h1 className="admin-page-title">Maintenance & Operational Health Dashboard</h1>
          <p className="admin-page-subtitle">
            Monitor vehicles undergoing or awaiting maintenance, review units requiring operational attention, and release serviced inventory back to active status.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <Button variant="outline" size="sm" onClick={fetchDashboard} disabled={isLoading}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              width="14"
              height="14"
              style={{ marginRight: '6px' }}
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh Data
          </Button>
          <Button variant="primary" size="sm" onClick={() => onNavigate('manage-fleet')}>
            All Fleet Inventory
          </Button>
        </div>
      </div>

      {statusSuccessMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert type="success" title="Operational Status Updated" message={statusSuccessMessage} />
        </div>
      )}

      {errorMessage && (
        <div style={{ marginBottom: '16px' }}>
          <Alert
            type="error"
            title="Failed to Load Maintenance Data"
            message={errorMessage}
            actionText="Retry"
            onAction={fetchDashboard}
          />
        </div>
      )}

      {/* Hub Location Filter Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
          padding: '12px 16px',
          backgroundColor: 'var(--color-surface, #ffffff)',
          borderRadius: 'var(--radius-md, 8px)',
          border: '1px solid var(--color-border, #e2e8f0)',
          marginBottom: '20px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--color-text-secondary, #475569)' }}>
            Filter Hub Location:
          </span>
          <select
            value={selectedHub}
            onChange={(e) => setSelectedHub(e.target.value)}
            className="filter-select-input"
            style={{ minWidth: '180px' }}
            aria-label="Filter maintenance by hub"
          >
            {HUB_LOCATIONS.map((hub) => (
              <option key={hub} value={hub}>{hub}</option>
            ))}
          </select>
        </div>

        {dashboardData?.generatedAt && (
          <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #94a3b8)' }}>
            Report timestamp: {new Date(dashboardData.generatedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
          </span>
        )}
      </div>

      {/* 1. Persisted Operational Status Summaries (Zero Fabricated Metrics) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '16px',
          marginBottom: '28px',
        }}
      >
        {/* Card 1: Undergoing Maintenance */}
        <div
          className="stat-card"
          style={{
            borderLeft: '4px solid #f59e0b',
            padding: '16px 20px',
            backgroundColor: 'var(--color-surface, #ffffff)',
            borderRadius: '8px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
            border: '1px solid var(--color-border, #e2e8f0)',
            borderLeftWidth: '4px',
            borderLeftColor: '#f59e0b',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--color-text-secondary, #64748b)' }}>
              Undergoing / Awaiting Maintenance
            </span>
            <span
              style={{
                width: '28px',
                height: '28px',
                borderRadius: '6px',
                backgroundColor: 'rgba(245, 158, 11, 0.15)',
                color: '#d97706',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              🔧
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '32px', fontWeight: 700, color: '#b45309', lineHeight: 1 }}>
              {isLoading ? '...' : counts.undergoingMaintenance}
            </span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
              active units
            </span>
          </div>
          <div style={{ marginTop: '8px', fontSize: '12px', color: '#b45309' }}>
            Requires technician inspection or repair
          </div>
        </div>

        {/* Card 2: Operational / Available */}
        <div
          className="stat-card"
          style={{
            padding: '16px 20px',
            backgroundColor: 'var(--color-surface, #ffffff)',
            borderRadius: '8px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
            border: '1px solid var(--color-border, #e2e8f0)',
            borderLeftWidth: '4px',
            borderLeftColor: '#10b981',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--color-text-secondary, #64748b)' }}>
              Operational & Available
            </span>
            <span
              style={{
                width: '28px',
                height: '28px',
                borderRadius: '6px',
                backgroundColor: 'rgba(16, 185, 129, 0.15)',
                color: '#059669',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              ✓
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '32px', fontWeight: 700, color: '#047857', lineHeight: 1 }}>
              {isLoading ? '...' : counts.available}
            </span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
              units ready
            </span>
          </div>
          <div style={{ marginTop: '8px', fontSize: '12px', color: '#047857' }}>
            Available for customer reservations
          </div>
        </div>

        {/* Card 3: In Use / Active Rentals */}
        <div
          className="stat-card"
          style={{
            padding: '16px 20px',
            backgroundColor: 'var(--color-surface, #ffffff)',
            borderRadius: '8px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
            border: '1px solid var(--color-border, #e2e8f0)',
            borderLeftWidth: '4px',
            borderLeftColor: '#3b82f6',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--color-text-secondary, #64748b)' }}>
              Vehicles In-Use
            </span>
            <span
              style={{
                width: '28px',
                height: '28px',
                borderRadius: '6px',
                backgroundColor: 'rgba(59, 130, 246, 0.15)',
                color: '#2563eb',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              🚗
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '32px', fontWeight: 700, color: '#1d4ed8', lineHeight: 1 }}>
              {isLoading ? '...' : counts.inUse}
            </span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
              on road
            </span>
          </div>
          <div style={{ marginTop: '8px', fontSize: '12px', color: '#1d4ed8' }}>
            Currently dispatched with customers
          </div>
        </div>

        {/* Card 4: Total Registered Fleet */}
        <div
          className="stat-card"
          style={{
            padding: '16px 20px',
            backgroundColor: 'var(--color-surface, #ffffff)',
            borderRadius: '8px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
            border: '1px solid var(--color-border, #e2e8f0)',
            borderLeftWidth: '4px',
            borderLeftColor: '#6b7280',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--color-text-secondary, #64748b)' }}>
              Total Fleet Inventory
            </span>
            <span
              style={{
                width: '28px',
                height: '28px',
                borderRadius: '6px',
                backgroundColor: 'rgba(107, 114, 128, 0.15)',
                color: '#4b5563',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              📊
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '32px', fontWeight: 700, color: '#374151', lineHeight: 1 }}>
              {isLoading ? '...' : counts.totalVehicles}
            </span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
              total units
            </span>
          </div>
          <div style={{ marginTop: '8px', fontSize: '12px', color: '#6b7280' }}>
            Includes {counts.retired} decommissioned units
          </div>
        </div>
      </div>

      {/* 2. Vehicles Requiring Attention Section */}
      <div
        style={{
          backgroundColor: 'var(--color-surface, #ffffff)',
          borderRadius: '8px',
          border: '1px solid var(--color-border, #e2e8f0)',
          padding: '20px',
          marginBottom: '28px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0 }}>
                Vehicles Requiring Operational Attention
              </h2>
              <span
                style={{
                  backgroundColor: attentionItems.length > 0 ? '#ef4444' : '#10b981',
                  color: '#ffffff',
                  fontSize: '11px',
                  fontWeight: 700,
                  padding: '2px 8px',
                  borderRadius: '12px',
                }}
              >
                {attentionItems.length} {attentionItems.length === 1 ? 'Unit' : 'Units'}
              </span>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', marginTop: '4px', marginBottom: 0 }}>
              Flagged based on active maintenance state, prolonged workshop duration, or high mileage inspection intervals.
            </p>
          </div>
        </div>

        {isLoading ? (
          <div style={{ padding: '30px', textAlign: 'center', color: 'var(--color-text-muted, #94a3b8)' }}>
            <div className="spinner" style={{ margin: '0 auto 12px' }} />
            <span>Scanning fleet catalog for attention items...</span>
          </div>
        ) : attentionItems.length === 0 ? (
          <div
            style={{
              padding: '32px 20px',
              textAlign: 'center',
              backgroundColor: 'var(--color-background, #f8fafc)',
              borderRadius: '6px',
              border: '1px dashed var(--color-border, #cbd5e1)',
            }}
          >
            <div style={{ fontSize: '24px', marginBottom: '8px' }}>🎉</div>
            <div style={{ fontSize: '15px', fontWeight: 600, color: 'var(--color-text-primary, #0f172a)' }}>
              No Vehicles Currently Require Attention
            </div>
            <div style={{ fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', marginTop: '4px' }}>
              All active fleet vehicles are operational, within expected mileage thresholds, and ready for service.
            </div>
          </div>
        ) : (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
              gap: '14px',
            }}
          >
            {attentionItems.map((item) => {
              const isHigh = item.urgency === 'High';
              return (
                <div
                  key={item.id}
                  style={{
                    padding: '14px 16px',
                    borderRadius: '8px',
                    backgroundColor: isHigh ? 'rgba(239, 68, 68, 0.04)' : 'rgba(245, 158, 11, 0.04)',
                    border: `1px solid ${isHigh ? 'rgba(239, 68, 68, 0.3)' : 'rgba(245, 158, 11, 0.3)'}`,
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                  }}
                >
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '6px' }}>
                      <span
                        style={{
                          fontSize: '11px',
                          fontWeight: 700,
                          textTransform: 'uppercase',
                          letterSpacing: '0.04em',
                          padding: '2px 6px',
                          borderRadius: '4px',
                          backgroundColor: isHigh ? '#fee2e2' : '#fef3c7',
                          color: isHigh ? '#b91c1c' : '#b45309',
                        }}
                      >
                        {item.urgency} Priority
                      </span>
                      <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--color-text-secondary, #475569)' }}>
                        {item.hubLocation || 'Unassigned Hub'}
                      </span>
                    </div>

                    <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--color-text-primary, #0f172a)', marginBottom: '2px' }}>
                      {item.year} {item.make} {item.model}
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)', fontFamily: 'monospace', marginBottom: '8px' }}>
                      {item.licensePlate || 'NO PLATE'} • VIN: {item.vin}
                    </div>

                    <div
                      style={{
                        fontSize: '12px',
                        padding: '6px 8px',
                        borderRadius: '4px',
                        backgroundColor: isHigh ? 'rgba(239, 68, 68, 0.08)' : 'rgba(245, 158, 11, 0.08)',
                        color: isHigh ? '#991b1b' : '#92400e',
                        marginBottom: '10px',
                        fontWeight: 500,
                      }}
                    >
                      ⚠️ {item.attentionReason}
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--color-text-secondary, #64748b)', marginBottom: '12px' }}>
                      <span>Odometer: <strong>{Number(item.mileage || 0).toLocaleString()} mi</strong></span>
                      {item.daysInMaintenance > 0 && (
                        <span style={{ marginLeft: '12px' }}>
                          In Service: <strong>{item.daysInMaintenance} days</strong>
                        </span>
                      )}
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '8px', marginTop: 'auto' }}>
                    <Button
                      variant="outline"
                      size="sm"
                      style={{ flex: 1 }}
                      onClick={() => handleOpenStatusModal(item)}
                    >
                      Update Status
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        if (onSelectVehicle) onSelectVehicle(item);
                        onNavigate('staff-vehicle-details', { vehicle: item });
                      }}
                    >
                      Inspect
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* 3. Active Maintenance Work Queue Table */}
      <div
        style={{
          backgroundColor: 'var(--color-surface, #ffffff)',
          borderRadius: '8px',
          border: '1px solid var(--color-border, #e2e8f0)',
          padding: '20px',
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '12px',
            marginBottom: '16px',
          }}
        >
          <div>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0 }}>
              Active Maintenance Work Queue
            </h2>
            <p style={{ fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', marginTop: '4px', marginBottom: 0 }}>
              All vehicles currently marked with "Maintenance" operational status.
            </p>
          </div>

          <div className="search-box-wrapper" style={{ minWidth: '240px' }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="search-icon" aria-hidden="true">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="text"
              placeholder="Search queue by plate, VIN, make..."
              value={tableSearchTerm}
              onChange={(e) => setTableSearchTerm(e.target.value)}
              className="filter-search-input"
              aria-label="Filter maintenance queue"
            />
          </div>
        </div>

        {isLoading ? (
          <div style={{ padding: '40px', textAlign: 'center', color: 'var(--color-text-muted, #94a3b8)' }}>
            <div className="spinner" style={{ margin: '0 auto 12px' }} />
            <span>Loading active maintenance work queue...</span>
          </div>
        ) : filteredMaintenanceVehicles.length === 0 ? (
          <div
            style={{
              padding: '40px 20px',
              textAlign: 'center',
              backgroundColor: 'var(--color-background, #f8fafc)',
              borderRadius: '6px',
              border: '1px dashed var(--color-border, #cbd5e1)',
            }}
          >
            <div style={{ fontSize: '28px', marginBottom: '8px' }}>🚗</div>
            <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--color-text-primary, #0f172a)' }}>
              No Vehicles In Active Maintenance
            </div>
            <div style={{ fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', marginTop: '4px', maxWidth: '480px', margin: '4px auto 16px' }}>
              {tableSearchTerm
                ? `No vehicles in maintenance match your search term "${tableSearchTerm}".`
                : 'There are currently no vehicles undergoing or awaiting maintenance in this hub view.'}
            </div>
            <Button variant="outline" size="sm" onClick={() => onNavigate('manage-fleet')}>
              View Fleet Inventory
            </Button>
          </div>
        ) : (
          <div className="table-responsive" style={{ overflowX: 'auto' }}>
            <table className="users-table" style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th>Vehicle Details</th>
                  <th>VIN & Plate</th>
                  <th>Hub Location</th>
                  <th>Odometer</th>
                  <th>Status</th>
                  <th>Duration</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredMaintenanceVehicles.map((vehicle) => (
                  <tr key={vehicle.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--color-text-primary, #0f172a)' }}>
                        {vehicle.year} {vehicle.make} {vehicle.model}
                      </div>
                      <span style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
                        {vehicle.categoryName || 'Fleet Unit'}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontWeight: 600, letterSpacing: '0.04em' }}>
                        {vehicle.licensePlate || 'N/A'}
                      </div>
                      <span style={{ fontSize: '11px', color: 'var(--color-text-muted, #64748b)', fontFamily: 'monospace' }}>
                        {vehicle.vin}
                      </span>
                    </td>
                    <td>
                      <span style={{ fontSize: '13px', color: 'var(--color-text-secondary, #475569)' }}>
                        {vehicle.hubLocation || 'Unassigned'}
                      </span>
                    </td>
                    <td>
                      <span style={{ fontSize: '13px', fontWeight: 500 }}>
                        {Number(vehicle.mileage || 0).toLocaleString()} mi
                      </span>
                    </td>
                    <td>
                      <span className="status-badge maintenance">
                        {vehicle.status}
                      </span>
                    </td>
                    <td>
                      <span style={{ fontSize: '12px', color: 'var(--color-text-secondary, #475569)' }}>
                        {vehicle.daysInCurrentStatus === 0 ? 'Today' : `${vehicle.daysInCurrentStatus} days`}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: '6px' }}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleOpenStatusModal(vehicle)}
                          title="Change operational status"
                        >
                          Status
                        </Button>
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => {
                            if (onSelectVehicle) onSelectVehicle(vehicle);
                            onNavigate('staff-vehicle-details', { vehicle });
                          }}
                          title="Inspect vehicle"
                        >
                          Details
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Operational Status Update Modal */}
      <Modal
        isOpen={Boolean(statusModalVehicle)}
        onClose={handleCloseStatusModal}
        title="Update Operational Status"
        subtitle={`Modify operational availability for ${statusModalVehicle?.year} ${statusModalVehicle?.make} ${statusModalVehicle?.model}`}
      >
        {statusModalError && (
          <div style={{ marginBottom: '16px' }}>
            <Alert type="error" title="Status Update Error" message={statusModalError} />
          </div>
        )}

        <div style={{ marginBottom: '1.25rem', fontSize: '13px', color: 'var(--color-text-secondary, #64748b)', lineHeight: '1.5' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '12px 14px',
              backgroundColor: 'var(--color-surface, #f8fafc)',
              borderRadius: '8px',
              border: '1px solid var(--color-border, #e2e8f0)',
              marginBottom: '16px',
            }}
          >
            <div>
              <div style={{ fontWeight: 600, color: 'var(--color-text-primary, #0f172a)' }}>
                {statusModalVehicle?.make} {statusModalVehicle?.model} ({statusModalVehicle?.licensePlate || statusModalVehicle?.vin})
              </div>
              <div style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)' }}>
                Location: {statusModalVehicle?.hubLocation || 'Unassigned'} • Mileage: {Number(statusModalVehicle?.mileage || 0).toLocaleString()} mi
              </div>
            </div>
            <div>
              <span className={`status-badge ${(statusModalVehicle?.status || '').toLowerCase()}`}>
                {statusModalVehicle?.status}
              </span>
            </div>
          </div>

          <label
            htmlFor="targetStatusSelect"
            style={{ display: 'block', fontWeight: 600, marginBottom: '6px', color: 'var(--color-text-primary, #0f172a)' }}
          >
            Select New Operational Status:
          </label>
          <select
            id="targetStatusSelect"
            value={targetStatus}
            onChange={(e) => {
              setTargetStatus(e.target.value);
              setStatusModalError(null);
            }}
            disabled={isUpdatingStatus}
            style={{
              width: '100%',
              padding: '10px 12px',
              borderRadius: '6px',
              border: '1px solid var(--color-border, #cbd5e1)',
              backgroundColor: '#fff',
              fontSize: '14px',
              fontWeight: 500,
              color: 'var(--color-text-primary, #0f172a)',
              marginBottom: '8px',
            }}
          >
            {allowedStatusOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>

          {selectedStatusDesc && (
            <p style={{ fontSize: '12px', color: 'var(--color-text-muted, #64748b)', margin: '4px 0 16px' }}>
              {selectedStatusDesc}
            </p>
          )}

          {targetStatus === 'Retired' && (
            <div
              style={{
                padding: '10px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(239, 68, 68, 0.1)',
                border: '1px solid rgba(239, 68, 68, 0.2)',
                color: '#b91c1c',
                fontSize: '12px',
                marginBottom: '16px',
              }}
            >
              <strong>Caution:</strong> Retiring this vehicle will permanently withdraw it from commercial service and customer availability.
            </div>
          )}

          {targetStatus === 'Available' && statusModalVehicle?.status === 'Maintenance' && (
            <div
              style={{
                padding: '10px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(16, 185, 129, 0.1)',
                border: '1px solid rgba(16, 185, 129, 0.2)',
                color: '#065f46',
                fontSize: '12px',
                marginBottom: '16px',
              }}
            >
              <strong>Service Release:</strong> Marking this vehicle as Available certifies that maintenance tasks are complete and the vehicle is safe for customer dispatch.
            </div>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
          <Button
            variant="outline"
            onClick={handleCloseStatusModal}
            disabled={isUpdatingStatus}
          >
            Cancel
          </Button>
          <Button
            variant="primary"
            onClick={handleConfirmStatusChange}
            disabled={isUpdatingStatus || targetStatus === statusModalVehicle?.status}
          >
            {isUpdatingStatus ? 'Updating...' : 'Update Status'}
          </Button>
        </div>
      </Modal>
    </div>
  );
};

export default MaintenanceDashboardPage;
