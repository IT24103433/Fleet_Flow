import { CheckIcon } from '@phosphor-icons/react/dist/csr/Check';
import { WrenchIcon } from '@phosphor-icons/react/dist/csr/Wrench';
import { CarProfileIcon } from '@phosphor-icons/react/dist/csr/CarProfile';
import { SquaresFourIcon } from '@phosphor-icons/react/dist/csr/SquaresFour';
import { ArrowClockwiseIcon } from "@phosphor-icons/react/dist/csr/ArrowClockwise";
import { MagnifyingGlassIcon } from "@phosphor-icons/react/dist/csr/MagnifyingGlass";
import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getMaintenanceDashboard } from '../../services/maintenanceService';
import { updateVehicleStatus } from '../../services/vehicleService';
import { validateStatusChange, getAvailableStatusTransitionsForRoles } from '../../validation/vehicleStatusValidation';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import Modal from '../../components/common/Modal';
import MaintenanceWorkOrders from '../../components/maintenance/MaintenanceWorkOrders';
const MaintenanceDashboardPage = ({
  onNavigate,
  onSelectVehicle
}) => {
  const {
    token,
    roles
  } = useAuth();
  const [dashboardData, setDashboardData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);
  const [selectedHub, setSelectedHub] = useState('All Hubs');
  const [hubLocations, setHubLocations] = useState([]);
  const [tableSearchTerm, setTableSearchTerm] = useState('');
  const latestRequest = useRef({
    id: 0
  });

  // Status Change Modal State
  const [statusModalVehicle, setStatusModalVehicle] = useState(null);
  const [targetStatus, setTargetStatus] = useState('');
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusModalError, setStatusModalError] = useState(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState(null);
  const fetchDashboard = useCallback(async () => {
    const requestId = ++latestRequest.current.id;
    setIsLoading(true);
    setErrorMessage(null);
    const params = {};
    if (selectedHub && selectedHub !== 'All Hubs') {
      params.hub = selectedHub;
    }
    const result = await getMaintenanceDashboard(token, params);
    if (requestId !== latestRequest.current.id) return;
    setIsLoading(false);
    if (result.success && result.data) {
      setDashboardData(result.data);
    } else {
      setDashboardData(null);
      setErrorMessage(result.message || 'Failed to retrieve maintenance data.');
    }
  }, [token, selectedHub]);
  useEffect(() => {
    const requestState = latestRequest.current;
    const timer = window.setTimeout(fetchDashboard, 0);
    return () => {
      window.clearTimeout(timer);
      requestState.id++;
    };
  }, [fetchDashboard]);

  // Role-filtered status options for modal
  const allowedStatusOptions = useMemo(() => {
    return getAvailableStatusTransitionsForRoles(roles);
  }, [roles]);
  const selectedStatusDesc = useMemo(() => {
    return allowedStatusOptions.find(o => o.value === targetStatus)?.description || '';
  }, [allowedStatusOptions, targetStatus]);
  const handleOpenStatusModal = vehicle => {
    setStatusModalVehicle(vehicle);
    const nextDefault = allowedStatusOptions.find(s => s.value !== vehicle.status)?.value || allowedStatusOptions[0]?.value || '';
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
      setStatusSuccessMessage(`Vehicle ${statusModalVehicle.licensePlate || statusModalVehicle.vin} status updated to "${targetStatus}".`);
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
    return list.filter(v => v.make && v.make.toLowerCase().includes(term) || v.model && v.model.toLowerCase().includes(term) || v.vin && v.vin.toLowerCase().includes(term) || v.licensePlate && v.licensePlate.toLowerCase().includes(term) || v.hubLocation && v.hubLocation.toLowerCase().includes(term));
  }, [dashboardData, tableSearchTerm]);
  const counts = dashboardData?.statusCounts || {};
  const attentionItems = dashboardData?.attentionItems || [];
  const hasCurrentData = !isLoading && !errorMessage && dashboardData;
  const displayCount = key => isLoading ? '…' : errorMessage ? 'Unavailable' : counts[key] ?? 'Unavailable';
  return <div className="manage-fleet-container view-2d16fb38">
      {/* Top Header */}
      <div className="admin-page-header view-002dc26c">
        <div>
          <div className="view-6292f18b">
            <span className="view-37d4ed0a">
              <span className="view-9e10bdb8" />
              Fleet maintenance
            </span>
            <span className="view-35dbeb2f">
              Work orders & vehicle status
            </span>
          </div>
          <h1 className="admin-page-title">Maintenance overview</h1>
          <p className="admin-page-subtitle">
            Plan service, review vehicles needing attention, and record completed work.
          </p>
        </div>

        <div className="view-d8c16219">
          <Button variant="outline" size="sm" onClick={fetchDashboard} disabled={isLoading}>
            <ArrowClockwiseIcon width="14" height="14" className="view-942b190e" weight="regular" aria-hidden="true" />
            Refresh
          </Button>
          <Button variant="primary" size="sm" onClick={() => onNavigate('manage-fleet')}>
            Vehicle inventory
          </Button>
        </div>
      </div>

      {statusSuccessMessage && <div className="view-4933c088">
          <Alert type="success" title="Operational Status Updated" message={statusSuccessMessage} />
        </div>}

      {errorMessage && <div className="view-4933c088">
          <Alert type="error" title="Failed to Load Maintenance Data" message={errorMessage} actionText="Retry" onAction={fetchDashboard} />
        </div>}

      {/* Hub Location Filter Bar */}
      <div className="view-c213a079">
        <div className="view-0c1a7e49">
          <span className="view-c0098a6b">
            Filter Hub Location:
          </span>
          <select value={selectedHub} onChange={e => setSelectedHub(e.target.value)} className="filter-select-input view-01a3bb68" aria-label="Filter maintenance by hub">
            {['All Hubs', ...hubLocations].map(hub => <option key={hub} value={hub}>{hub}</option>)}
          </select>
        </div>

        {hasCurrentData && dashboardData?.generatedAt && <span className="view-35dbeb2f">
            Report timestamp: {new Date(dashboardData.generatedAt).toLocaleTimeString([], {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit'
        })}
          </span>}
      </div>

      {/* 1. Persisted Operational Status Summaries (Zero Fabricated Metrics) */}
      <div className="view-91f0fef0">
        {/* Card 1: Undergoing Maintenance */}
        <div className="stat-card view-00788f09">
          <div className="view-e4fc022e">
            <span className="view-c0098a6b">
              In maintenance
            </span>
            <span className="view-2ad0e336">
              <WrenchIcon size={18} weight="regular" aria-hidden="true" />
            </span>
          </div>
          <div className="view-497eb6eb">
            <span className="view-de5a9851">
              {displayCount('undergoingMaintenance')}
            </span>
            <span className="view-35dbeb2f">
              active units
            </span>
          </div>
          <div className="view-c10c0f6c">
            Requires technician inspection or repair
          </div>
        </div>

        {/* Card 2: Operational / Available */}
        <div className="stat-card view-a4d48576">
          <div className="view-e4fc022e">
            <span className="view-c0098a6b">
              Available vehicles
            </span>
            <span className="view-c9b7a394">
              <CheckIcon size={18} weight="regular" aria-hidden="true" />
            </span>
          </div>
          <div className="view-497eb6eb">
            <span className="view-e316e8ce">
              {displayCount('available')}
            </span>
            <span className="view-35dbeb2f">
              units ready
            </span>
          </div>
          <div className="view-3d4f856c">
            Available for customer reservations
          </div>
        </div>

        {/* Card 3: In Use / Active Rentals */}
        <div className="stat-card view-95c8bac2">
          <div className="view-e4fc022e">
            <span className="view-c0098a6b">
              Vehicles in use
            </span>
            <span className="view-0f9795d4">
              <CarProfileIcon size={18} weight="regular" aria-hidden="true" />
            </span>
          </div>
          <div className="view-497eb6eb">
            <span className="view-98a0ffc1">
              {displayCount('inUse')}
            </span>
            <span className="view-35dbeb2f">
              on road
            </span>
          </div>
          <div className="view-972ad60e">
            Currently dispatched with customers
          </div>
        </div>

        {/* Card 4: Total Registered Fleet */}
        <div className="stat-card view-40eb88b0">
          <div className="view-e4fc022e">
            <span className="view-c0098a6b">
              Total vehicles
            </span>
            <span className="view-83e8a9f5">
              <SquaresFourIcon size={18} weight="regular" aria-hidden="true" />
            </span>
          </div>
          <div className="view-497eb6eb">
            <span className="view-1bf8e9e3">
              {displayCount('totalVehicles')}
            </span>
            <span className="view-35dbeb2f">
              total units
            </span>
          </div>
          <div className="view-5c01f8e5">
            {hasCurrentData && typeof counts.retired === 'number' ? `Includes ${counts.retired} retired vehicles` : 'Retired vehicle totals unavailable'}
          </div>
        </div>
      </div>

      <MaintenanceWorkOrders token={token} onDataChanged={fetchDashboard} onLocationsLoaded={setHubLocations} />

      {/* 2. Vehicles Requiring Attention Section */}
      <div className="view-825c2434">
        <div className="view-0a303a64">
          <div>
            <div className="view-378e7657">
              <h2 className="view-07e20700">
                Vehicles needing attention
              </h2>
              <span style={{
              backgroundColor: attentionItems.length > 0 ? '#ef4444' : '#10b981',
              color: '#ffffff',
              fontSize: '11px',
              fontWeight: 700,
              padding: '2px 8px',
              borderRadius: '12px'
            }}>
                {hasCurrentData ? `${attentionItems.length} ${attentionItems.length === 1 ? 'vehicle' : 'vehicles'}` : 'Unavailable'}
              </span>
            </div>
            <p className="view-1595ea46">
              Flagged based on active maintenance state, prolonged workshop duration, or high mileage inspection intervals.
            </p>
          </div>
        </div>

        {isLoading ? <div className="view-25c27249">
            <div className="spinner view-b0f042a7" />
            <span>Loading vehicles needing attention…</span>
          </div> : errorMessage ? <p>Attention queue unavailable. Refresh to try again.</p> : attentionItems.length === 0 ? <div className="view-a68a14de">
            <div className="view-f5cee306">
              No vehicles flagged for attention
            </div>
            <div className="view-94eb9312">
              No vehicles are flagged for attention in this hub view.
            </div>
          </div> : <div className="view-466f7f6b">
            {attentionItems.map(item => {
          const isHigh = item.urgency === 'High';
          return <div key={item.id} style={{
            padding: '14px 16px',
            borderRadius: '8px',
            backgroundColor: isHigh ? 'rgba(239, 68, 68, 0.04)' : 'rgba(245, 158, 11, 0.04)',
            border: `1px solid ${isHigh ? 'rgba(239, 68, 68, 0.3)' : 'rgba(245, 158, 11, 0.3)'}`,
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between'
          }}>
                  <div>
                    <div className="view-d479f924">
                      <span style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  padding: '2px 6px',
                  borderRadius: '4px',
                  backgroundColor: isHigh ? '#fee2e2' : '#fef3c7',
                  color: isHigh ? '#b91c1c' : '#b45309'
                }}>
                        {item.urgency} Priority
                      </span>
                      <span className="view-61cc2da9">
                        {item.hubLocation || 'Unassigned Hub'}
                      </span>
                    </div>

                    <div className="view-f43749b1">
                      {item.year} {item.make} {item.model}
                    </div>

                    <div className="view-8b221b13">
                      {item.licensePlate || 'NO PLATE'} • VIN: {item.vin}
                    </div>

                    <div style={{
                fontSize: '12px',
                padding: '6px 8px',
                borderRadius: '4px',
                backgroundColor: isHigh ? 'rgba(239, 68, 68, 0.08)' : 'rgba(245, 158, 11, 0.08)',
                color: isHigh ? '#991b1b' : '#92400e',
                marginBottom: '10px',
                fontWeight: 500
              }}>
                      {item.attentionReason}
                    </div>

                    <div className="view-dd05d1ff">
                      <span>Odometer: <strong>{Number(item.mileage || 0).toLocaleString()} mi</strong></span>
                      {item.daysInMaintenance > 0 && <span className="view-cb60857c">
                          In Service: <strong>{item.daysInMaintenance} days</strong>
                        </span>}
                    </div>
                  </div>

                  <div className="view-62f58851">
                    <Button variant="outline" size="sm" onClick={() => handleOpenStatusModal(item)} className="view-126244f1">
                      Update Status
                    </Button>
                    <Button variant="secondary" size="sm" onClick={() => {
                if (onSelectVehicle) onSelectVehicle(item);
                onNavigate('staff-vehicle-details', {
                  vehicle: item
                });
              }}>
                      Inspect
                    </Button>
                  </div>
                </div>;
        })}
          </div>}
      </div>

      {/* 3. Active Maintenance Work Queue Table */}
      <div className="view-2ff41118">
        <div className="view-c94b7d00">
          <div>
            <h2 className="view-07e20700">
              Active Maintenance Work Queue
            </h2>
            <p className="view-1595ea46">
              All vehicles currently marked with "Maintenance" operational status.
            </p>
          </div>

          <div className="search-box-wrapper view-4915ee79">
            <MagnifyingGlassIcon className="search-icon" weight="regular" aria-hidden="true" />
            <input type="text" placeholder="Search queue by plate, VIN, make..." value={tableSearchTerm} onChange={e => setTableSearchTerm(e.target.value)} className="filter-search-input" aria-label="Filter maintenance queue" />
          </div>
        </div>

        {isLoading ? <div className="view-097687ab">
            <div className="spinner view-b0f042a7" />
            <span>Loading active maintenance work queue...</span>
          </div> : errorMessage ? <p>Maintenance queue unavailable. Refresh to try again.</p> : filteredMaintenanceVehicles.length === 0 ? <div className="view-ccdda724">
            <div className="view-72a57276"><WrenchIcon size={32} weight="regular" aria-hidden="true" /></div>
            <div className="view-e222ecb4">
              No Vehicles In Active Maintenance
            </div>
            <div className="view-d8923426">
              {tableSearchTerm ? `No vehicles in maintenance match your search term "${tableSearchTerm}".` : 'There are currently no vehicles undergoing or awaiting maintenance in this hub view.'}
            </div>
            <Button variant="outline" size="sm" onClick={() => onNavigate('manage-fleet')}>
              View Fleet Inventory
            </Button>
          </div> : <div className="table-responsive view-aeadbd09">
            <table className="users-table view-57761fd3">
              <thead>
                <tr>
                  <th>Vehicle Details</th>
                  <th>VIN & Plate</th>
                  <th>Hub Location</th>
                  <th>Odometer</th>
                  <th>Status</th>
                  <th>Duration</th>
                  <th className="view-37dd0c2a">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredMaintenanceVehicles.map(vehicle => <tr key={vehicle.id}>
                    <td>
                      <div className="view-8907d496">
                        {vehicle.year} {vehicle.make} {vehicle.model}
                      </div>
                      <span className="view-35dbeb2f">
                        {vehicle.categoryName || 'Fleet Unit'}
                      </span>
                    </td>
                    <td>
                      <div className="view-37272423">
                        {vehicle.licensePlate || 'N/A'}
                      </div>
                      <span className="view-92809128">
                        {vehicle.vin}
                      </span>
                    </td>
                    <td>
                      <span className="view-fce42fdd">
                        {vehicle.hubLocation || 'Unassigned'}
                      </span>
                    </td>
                    <td>
                      <span className="view-7b0ad62b">
                        {Number(vehicle.mileage || 0).toLocaleString()} mi
                      </span>
                    </td>
                    <td>
                      <span className="status-badge maintenance">
                        {vehicle.status}
                      </span>
                    </td>
                    <td>
                      <span className="view-150363c2">
                        {vehicle.daysInCurrentStatus === 0 ? 'Today' : `${vehicle.daysInCurrentStatus} days`}
                      </span>
                    </td>
                    <td className="view-37dd0c2a">
                      <div className="view-fee47fc0">
                        <Button variant="outline" size="sm" onClick={() => handleOpenStatusModal(vehicle)} title="Change operational status">
                          Status
                        </Button>
                        <Button variant="secondary" size="sm" onClick={() => {
                    if (onSelectVehicle) onSelectVehicle(vehicle);
                    onNavigate('staff-vehicle-details', {
                      vehicle
                    });
                  }} title="Inspect vehicle">
                          Details
                        </Button>
                      </div>
                    </td>
                  </tr>)}
              </tbody>
            </table>
          </div>}
      </div>

      {/* Operational Status Update Modal */}
      <Modal isOpen={Boolean(statusModalVehicle)} onClose={handleCloseStatusModal} title="Update Operational Status" subtitle={`Modify operational availability for ${statusModalVehicle?.year} ${statusModalVehicle?.make} ${statusModalVehicle?.model}`}>
        {statusModalError && <div className="view-4933c088">
            <Alert type="error" title="Status Update Error" message={statusModalError} />
          </div>}

        <div className="view-b1d83fd0">
          <div className="view-836939bd">
            <div>
              <div className="view-8907d496">
                {statusModalVehicle?.make} {statusModalVehicle?.model} ({statusModalVehicle?.licensePlate || statusModalVehicle?.vin})
              </div>
              <div className="view-35dbeb2f">
                Location: {statusModalVehicle?.hubLocation || 'Unassigned'} • Mileage: {Number(statusModalVehicle?.mileage || 0).toLocaleString()} mi
              </div>
            </div>
            <div>
              <span className={`status-badge ${(statusModalVehicle?.status || '').toLowerCase()}`}>
                {statusModalVehicle?.status}
              </span>
            </div>
          </div>

          <label htmlFor="targetStatusSelect" className="view-4a744cf0">
            Select New Operational Status:
          </label>
          <select id="targetStatusSelect" value={targetStatus} onChange={e => {
          setTargetStatus(e.target.value);
          setStatusModalError(null);
        }} disabled={isUpdatingStatus} className="view-ba4c97ef">
            {allowedStatusOptions.map(opt => <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>)}
          </select>

          {selectedStatusDesc && <p className="view-603f425d">
              {selectedStatusDesc}
            </p>}

          {targetStatus === 'Retired' && <div className="view-4b2ad759">
              <strong>Caution:</strong> Retiring this vehicle will permanently withdraw it from commercial service and customer availability.
            </div>}

          {targetStatus === 'Available' && statusModalVehicle?.status === 'Maintenance' && <div className="view-abbc4e18">
              <strong>Service Release:</strong> Marking this vehicle as Available certifies that maintenance tasks are complete and the vehicle is safe for customer dispatch.
            </div>}
        </div>

        <div className="view-db0a7dcd">
          <Button variant="outline" onClick={handleCloseStatusModal} disabled={isUpdatingStatus}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleConfirmStatusChange} disabled={isUpdatingStatus || targetStatus === statusModalVehicle?.status}>
            {isUpdatingStatus ? 'Updating...' : 'Update Status'}
          </Button>
        </div>
      </Modal>
    </div>;
};
export default MaintenanceDashboardPage;
