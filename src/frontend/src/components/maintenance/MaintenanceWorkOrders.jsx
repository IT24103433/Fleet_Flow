import MaintenancePlanActions from './MaintenancePlanActions';
import useConfirmAction from "../common/useConfirmAction";
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Alert from '../Alert';
import Button from '../common/Button';
import Modal from '../common/Modal';
import StatusBadge from '../common/StatusBadge';
import { getVehicles } from '../../services/vehicleService';
import { createMaintenanceRecord, getVehicleMaintenanceHistory, updateMaintenanceRecord, updateMaintenanceStatus } from '../../services/maintenanceService';
import { getMaintenanceStatusTransitions, validateMaintenanceRecord } from '../../validation/maintenanceValidation';
import { normalizeValidationErrors } from '../../utils/validationErrorUtils';
const createDefaultForm = (vehicleId = '') => {
  const scheduled = new Date(Date.now() + 24 * 60 * 60 * 1000);
  scheduled.setMinutes(0, 0, 0);
  return {
    vehicleId,
    scheduledDateTime: new Date(scheduled.getTime() - scheduled.getTimezoneOffset() * 60000).toISOString().slice(0, 16),
    plannedEndDateTime: new Date(scheduled.getTime() + 2 * 3600000 - scheduled.getTimezoneOffset() * 60000).toISOString().slice(0, 16),
    serviceInformation: '',
    details: '',
    cost: '0'
  };
};
const formatStatus = status => String(status || '').toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase());
const MaintenanceWorkOrders = ({
  token,
  canAssign = false,
  onDataChanged,
  onLocationsLoaded
}) => {
  const {
    requestConfirmation,
    confirmationDialog
  } = useConfirmAction();
  const [vehicles, setVehicles] = useState([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState('');
  const [records, setRecords] = useState([]);
  const [isLoadingVehicles, setIsLoadingVehicles] = useState(true);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);
  const [successMessage, setSuccessMessage] = useState(null);
  const [editingRecord, setEditingRecord] = useState(null);
  const [form, setForm] = useState(null);
  const [formErrors, setFormErrors] = useState({});
  const [isSaving, setIsSaving] = useState(false);
  const [updatingStatusId, setUpdatingStatusId] = useState(null);
  const saving = useRef(false);
  const selectedVehicle = useMemo(() => vehicles.find(vehicle => vehicle.id === selectedVehicleId) || null, [vehicles, selectedVehicleId]);
  const schedulableVehicles = useMemo(() => vehicles.filter(vehicle => vehicle.status === 'Available' || vehicle.status === 'Maintenance'), [vehicles]);
  const loadVehicles = useCallback(async () => {
    setIsLoadingVehicles(true);
    const result = await getVehicles({
      includeRetired: true,
      pageSize: 100,
      sortBy: 'licensePlate',
      sortOrder: 'asc'
    });
    setIsLoadingVehicles(false);
    if (!result.success) {
      setErrorMessage(result.message || 'Unable to load fleet vehicles.');
      return;
    }
    setVehicles(result.data || []);
    onLocationsLoaded?.([...new Set((result.data || []).map(vehicle => vehicle.hubLocation).filter(Boolean))].sort());
    setSelectedVehicleId(current => current || result.data?.[0]?.id || '');
  }, [onLocationsLoaded]);
  const loadHistory = useCallback(async () => {
    if (!selectedVehicleId) {
      setRecords([]);
      return;
    }
    setIsLoadingHistory(true);
    const result = await getVehicleMaintenanceHistory(token, selectedVehicleId);
    setIsLoadingHistory(false);
    if (result.success) {
      setRecords(Array.isArray(result.data) ? result.data : []);
    } else {
      setRecords([]);
      setErrorMessage(result.message || 'Unable to retrieve maintenance history.');
    }
  }, [selectedVehicleId, token]);
  useEffect(() => {
    const timer = window.setTimeout(loadVehicles, 0);
    return () => window.clearTimeout(timer);
  }, [loadVehicles]);
  useEffect(() => {
    const timer = window.setTimeout(loadHistory, 0);
    return () => window.clearTimeout(timer);
  }, [loadHistory]);
  const openCreateModal = () => {
    const defaultVehicleId = schedulableVehicles.find(vehicle => vehicle.id === selectedVehicleId)?.id || schedulableVehicles[0]?.id || '';
    setEditingRecord(null);
    setForm(createDefaultForm(defaultVehicleId));
    setFormErrors({});
  };
  const openEditModal = record => {
    setEditingRecord(record);
    setForm({
      vehicleId: record.vehicleId,
      scheduledDateTime: record.scheduledDateTime,
      serviceInformation: record.serviceInformation || '',
      details: record.details || '',
      cost: String(record.cost ?? 0)
    });
    setFormErrors({});
  };
  const closeModal = () => {
    if (isSaving) return;
    setEditingRecord(null);
    setForm(null);
    setFormErrors({});
  };
  const handleSave = async () => {
    if (saving.current) return;
    const errors = validateMaintenanceRecord(form, {
      requireSchedule: !editingRecord, requireEnd: !editingRecord
    });
    setFormErrors(errors);
    if (Object.keys(errors).length > 0) return;
    saving.current = true; setIsSaving(true);
    setErrorMessage(null);
    const payload = {
      serviceInformation: form.serviceInformation.trim(),
      details: form.details.trim() || null,
      cost: Number(form.cost || 0)
    };
    const result = editingRecord ? await updateMaintenanceRecord(token, editingRecord.id, payload) : await createMaintenanceRecord(token, {
      ...payload,
      vehicleId: form.vehicleId,
      scheduledDateTime: new Date(form.scheduledDateTime).toISOString(),
      plannedEndDateTime: new Date(form.plannedEndDateTime).toISOString()
    });
    saving.current = false; setIsSaving(false);
    if (!result.success) {
      const serverErrors = normalizeValidationErrors(result.errors);
      setFormErrors(Object.keys(serverErrors).length > 0 ? serverErrors : {
        submit: result.message || 'Unable to save the maintenance record.'
      });
      return;
    }
    const saved = result.data;
    setSelectedVehicleId(saved.vehicleId);
    setSuccessMessage(editingRecord ? 'Maintenance record updated.' : 'Maintenance work order scheduled.');
    closeModal();
    await loadHistory();
    await loadVehicles();
    if (onDataChanged) await onDataChanged();
  };
  const handleStatusUpdate = async (record, status) => {
    requestConfirmation({
      title: "Change maintenance status?",
      message: "The work order will move to the selected status.",
      action: async () => {
        setUpdatingStatusId(record.id);
        setErrorMessage(null);
        const result = await updateMaintenanceStatus(token, record.id, status);
        setUpdatingStatusId(null);
        if (!result.success) {
          setErrorMessage(result.message || 'Unable to update maintenance status.');
          return;
        }
        setRecords(current => current.map(item => item.id === result.data.id ? result.data : item));
        setSuccessMessage(`Maintenance status updated to ${formatStatus(result.data.status)}.`);
        await loadVehicles();
        if (onDataChanged) await onDataChanged();
      },
      label: "Confirm change",
      variant: "primary"
    });
  };
  return <section className="view-2ed7b740">
      <div className="view-3d34149d">
        <div>
          <h2 className="view-a7e5960f">Maintenance Work Orders & History</h2>
          <p className="view-515b1e99">
            Schedule service, record progress and costs, and review vehicle maintenance history.
          </p>
        </div>
        <Button variant="primary" size="sm" onClick={openCreateModal} disabled={isLoadingVehicles || schedulableVehicles.length === 0}>
          Schedule Maintenance
        </Button>
      </div>

      {errorMessage && <div className="view-c804002a"><Alert type="error" title="Maintenance Request Failed" message={errorMessage} /></div>}
      {successMessage && <div className="view-c804002a"><Alert type="success" title="Maintenance Record Saved" message={successMessage} /></div>}

      <label htmlFor="maintenanceHistoryVehicle" className="view-5c14b541">
        Vehicle maintenance history
      </label>
      <select id="maintenanceHistoryVehicle" value={selectedVehicleId} onChange={event => {
      setSelectedVehicleId(event.target.value);
      setErrorMessage(null);
      setSuccessMessage(null);
    }} disabled={isLoadingVehicles} className="filter-select-input view-671ceeee">
        {vehicles.length === 0 && <option value="">No vehicles available</option>}
        {vehicles.map(vehicle => <option key={vehicle.id} value={vehicle.id}>
            {vehicle.licensePlate} — {vehicle.year} {vehicle.make} {vehicle.model} ({vehicle.status})
          </option>)}
      </select>

      {isLoadingHistory ? <div className="view-b86a2810">Loading maintenance history...</div> : records.length === 0 ? <div className="view-3a7b6877">
          {selectedVehicle ? `No maintenance history exists for ${selectedVehicle.licensePlate}.` : 'Select a vehicle to view maintenance history.'}
        </div> : <div className="table-responsive view-aeadbd09">
          <table className="users-table view-37ab52c9">
            <thead><tr><th>Planned window</th><th>Status</th><th>Service information</th><th>Cost</th><th className="view-37dd0c2a">Actions</th></tr></thead>
            <tbody>
              {records.map(record => {
            const transitions = getMaintenanceStatusTransitions(record.status);
            return <tr key={record.id}>
                    <td>{new Date(record.scheduledDateTime).toLocaleString()}<div className="view-2bab5824">{record.plannedEndDateTime ? `Until ${new Date(record.plannedEndDateTime).toLocaleString()}` : 'End not recorded · blocks availability'}</div></td>
                    <td><StatusBadge status={record.status} /></td>
                    <td>
                      <div className="view-3d9df89e">{record.serviceInformation}</div><div className="view-2bab5824">{record.assignedStaffName ? `Assigned to ${record.assignedStaffName}` : 'Unassigned'}</div>
                      {record.details && <div className="view-2bab5824">{record.details}</div>}
                    </td>
                    <td>LKR {Number(record.cost || 0).toLocaleString(undefined, {
                  minimumFractionDigits: 2
                })}</td>
                    <td className="view-37dd0c2a">
                      <div className="view-ab805e11 maintenance-row-actions">
                        <Button variant="outline" size="sm" onClick={() => openEditModal(record)}>Edit details</Button>
                        <MaintenancePlanActions record={record} token={token} canAssign={canAssign} onSaved={async updated => {
                          setRecords(current => current.map(item => item.id === updated.id ? updated : item));
                          setSuccessMessage('Maintenance plan updated.'); await loadVehicles(); if (onDataChanged) await onDataChanged();
                        }} />
                        {transitions.map(status => <Button key={status} variant={status === 'CANCELLED' ? 'outline' : 'secondary'} size="sm" disabled={updatingStatusId === record.id} onClick={() => handleStatusUpdate(record, status)}>
                            {formatStatus(status)}
                          </Button>)}
                      </div>
                    </td>
                  </tr>;
          })}
            </tbody>
          </table>
        </div>}

      <Modal isOpen={Boolean(form)} onClose={closeModal} title={editingRecord ? 'Update Maintenance Record' : 'Schedule Vehicle Maintenance'} subtitle={editingRecord ? `Record ${editingRecord.id}` : 'Choose a vehicle, planned window and required service.'}>
        {form && <div className="view-01702319">
            {formErrors.submit && <Alert type="error" title="Unable to Save" message={formErrors.submit} />}
            {!editingRecord && <div>
                <label htmlFor="maintenanceVehicle">Vehicle</label>
                <select id="maintenanceVehicle" value={form.vehicleId} onChange={event => {
            setForm({
              ...form,
              vehicleId: event.target.value
            });
            setFormErrors(current => ({
              ...current,
              vehicleId: undefined
            }));
          }} data-autofocus aria-invalid={Boolean(formErrors.vehicleId)} aria-describedby={formErrors.vehicleId ? 'maintenanceVehicleError' : undefined} className="view-3cd14f20">
                  <option value="">Select a vehicle</option>
                  {schedulableVehicles.map(vehicle => <option key={vehicle.id} value={vehicle.id}>{vehicle.licensePlate} — {vehicle.make} {vehicle.model}</option>)}
                </select>
                {formErrors.vehicleId && <div id="maintenanceVehicleError" className="field-error-message">{formErrors.vehicleId}</div>}
              </div>}
            {!editingRecord && <div>
                <label htmlFor="maintenanceScheduledDateTime">Maintenance date and time</label>
                <input id="maintenanceScheduledDateTime" type="datetime-local" value={form.scheduledDateTime} onChange={event => {
            setForm({
              ...form,
              scheduledDateTime: event.target.value
            });
            setFormErrors(current => ({
              ...current,
              scheduledDateTime: undefined
            }));
          }} aria-invalid={Boolean(formErrors.scheduledDateTime)} aria-describedby={formErrors.scheduledDateTime ? 'maintenanceScheduledDateTimeError' : undefined} className="view-3cd14f20" />
                {formErrors.scheduledDateTime && <div id="maintenanceScheduledDateTimeError" className="field-error-message">{formErrors.scheduledDateTime}</div>}
              </div>}
            {!editingRecord && <div><label htmlFor="maintenancePlannedEnd">Planned end</label><input id="maintenancePlannedEnd" type="datetime-local" value={form.plannedEndDateTime} onChange={event => setForm(previous => ({ ...previous, plannedEndDateTime: event.target.value }))} aria-invalid={Boolean(formErrors.plannedEndDateTime)} aria-describedby={formErrors.plannedEndDateTime ? 'maintenancePlannedEndError' : undefined} className="view-3cd14f20" />{formErrors.plannedEndDateTime && <div id="maintenancePlannedEndError" className="field-error-message">{formErrors.plannedEndDateTime}</div>}</div>}
            <div>
              <label htmlFor="maintenanceServiceInformation">Required service information</label>
              <textarea id="maintenanceServiceInformation" value={form.serviceInformation} onChange={event => {
            setForm({
              ...form,
              serviceInformation: event.target.value
            });
            setFormErrors(current => ({
              ...current,
              serviceInformation: undefined
            }));
          }} rows="3" maxLength="2000" data-autofocus={editingRecord ? true : undefined} aria-invalid={Boolean(formErrors.serviceInformation)} aria-describedby={formErrors.serviceInformation ? 'maintenanceServiceInformationError' : undefined} className="view-3cd14f20" />
              {formErrors.serviceInformation && <div id="maintenanceServiceInformationError" className="field-error-message">{formErrors.serviceInformation}</div>}
            </div>
            <div>
              <label htmlFor="maintenanceDetails">Progress / service details</label>
              <textarea id="maintenanceDetails" value={form.details} onChange={event => {
            setForm({
              ...form,
              details: event.target.value
            });
            setFormErrors(current => ({
              ...current,
              details: undefined
            }));
          }} rows="3" maxLength="4000" aria-invalid={Boolean(formErrors.details)} aria-describedby={formErrors.details ? 'maintenanceDetailsError' : undefined} className="view-3cd14f20" />
              {formErrors.details && <div id="maintenanceDetailsError" className="field-error-message">{formErrors.details}</div>}
            </div>
            <div>
              <label htmlFor="maintenanceCost">Maintenance cost (LKR)</label>
              <input id="maintenanceCost" type="number" min="0" step="0.01" value={form.cost} onChange={event => {
            setForm({
              ...form,
              cost: event.target.value
            });
            setFormErrors(current => ({
              ...current,
              cost: undefined
            }));
          }} aria-invalid={Boolean(formErrors.cost)} aria-describedby={formErrors.cost ? 'maintenanceCostError' : undefined} className="view-3cd14f20" />
              {formErrors.cost && <div id="maintenanceCostError" className="field-error-message">{formErrors.cost}</div>}
            </div>
            <div className="view-db0a7dcd">
              <Button variant="outline" onClick={closeModal} disabled={isSaving}>Cancel</Button>
              <Button variant="primary" onClick={handleSave} disabled={isSaving}>{isSaving ? 'Saving...' : 'Save Maintenance Record'}</Button>
            </div>
          </div>}
      </Modal>
    {confirmationDialog}</section>;
};
export default MaintenanceWorkOrders;
