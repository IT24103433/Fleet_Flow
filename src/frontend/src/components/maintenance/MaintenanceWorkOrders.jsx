import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from '../Alert';
import Button from '../common/Button';
import Modal from '../common/Modal';
import StatusBadge from '../common/StatusBadge';
import { getVehicles } from '../../services/vehicleService';
import {
  createMaintenanceRecord,
  getVehicleMaintenanceHistory,
  updateMaintenanceRecord,
  updateMaintenanceStatus,
} from '../../services/maintenanceService';
import {
  getMaintenanceStatusTransitions,
  validateMaintenanceRecord,
} from '../../validation/maintenanceValidation';
import { normalizeValidationErrors } from '../../utils/validationErrorUtils';

const createDefaultForm = (vehicleId = '') => {
  const scheduled = new Date(Date.now() + 24 * 60 * 60 * 1000);
  scheduled.setMinutes(0, 0, 0);
  return {
    vehicleId,
    scheduledDateTime: new Date(scheduled.getTime() - scheduled.getTimezoneOffset() * 60000).toISOString().slice(0, 16),
    serviceInformation: '',
    details: '',
    cost: '0',
  };
};

const formatStatus = (status) => String(status || '').replaceAll('_', ' ');

const MaintenanceWorkOrders = ({ token, onDataChanged }) => {
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

  const selectedVehicle = useMemo(
    () => vehicles.find((vehicle) => vehicle.id === selectedVehicleId) || null,
    [vehicles, selectedVehicleId],
  );
  const schedulableVehicles = useMemo(
    () => vehicles.filter((vehicle) => vehicle.status === 'Available' || vehicle.status === 'Maintenance'),
    [vehicles],
  );

  const loadVehicles = useCallback(async () => {
    setIsLoadingVehicles(true);
    const result = await getVehicles({ includeRetired: true, pageSize: 100, sortBy: 'licensePlate', sortOrder: 'asc' });
    setIsLoadingVehicles(false);
    if (!result.success) {
      setErrorMessage(result.message || 'Unable to load fleet vehicles.');
      return;
    }

    setVehicles(result.data || []);
    setSelectedVehicleId((current) => current || result.data?.[0]?.id || '');
  }, []);

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
    const defaultVehicleId = schedulableVehicles.find((vehicle) => vehicle.id === selectedVehicleId)?.id
      || schedulableVehicles[0]?.id
      || '';
    setEditingRecord(null);
    setForm(createDefaultForm(defaultVehicleId));
    setFormErrors({});
  };

  const openEditModal = (record) => {
    setEditingRecord(record);
    setForm({
      vehicleId: record.vehicleId,
      scheduledDateTime: record.scheduledDateTime,
      serviceInformation: record.serviceInformation || '',
      details: record.details || '',
      cost: String(record.cost ?? 0),
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
    const errors = validateMaintenanceRecord(form, { requireSchedule: !editingRecord });
    setFormErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setIsSaving(true);
    setErrorMessage(null);
    const payload = {
      serviceInformation: form.serviceInformation.trim(),
      details: form.details.trim() || null,
      cost: Number(form.cost || 0),
    };
    const result = editingRecord
      ? await updateMaintenanceRecord(token, editingRecord.id, payload)
      : await createMaintenanceRecord(token, {
          ...payload,
          vehicleId: form.vehicleId,
          scheduledDateTime: new Date(form.scheduledDateTime).toISOString(),
        });
    setIsSaving(false);

    if (!result.success) {
      const serverErrors = normalizeValidationErrors(result.errors);
      setFormErrors(Object.keys(serverErrors).length > 0
        ? serverErrors
        : { submit: result.message || 'Unable to save the maintenance record.' });
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
    if (!window.confirm(`Change maintenance status to ${formatStatus(status)}?`)) return;
    setUpdatingStatusId(record.id);
    setErrorMessage(null);
    const result = await updateMaintenanceStatus(token, record.id, status);
    setUpdatingStatusId(null);

    if (!result.success) {
      setErrorMessage(result.message || 'Unable to update maintenance status.');
      return;
    }

    setRecords((current) => current.map((item) => item.id === result.data.id ? result.data : item));
    setSuccessMessage(`Maintenance status updated to ${formatStatus(result.data.status)}.`);
    await loadVehicles();
    if (onDataChanged) await onDataChanged();
  };

  return (
    <section style={{ background: 'var(--color-surface, #fff)', border: '1px solid var(--color-border, #e2e8f0)', borderRadius: '8px', padding: '20px', marginBottom: '28px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '16px', flexWrap: 'wrap', marginBottom: '16px' }}>
        <div>
          <h2 style={{ margin: 0, fontSize: '18px' }}>Maintenance Work Orders & History</h2>
          <p style={{ margin: '4px 0 0', color: 'var(--color-text-secondary, #64748b)', fontSize: '13px' }}>
            Schedule service, record progress and cost, and review persisted vehicle maintenance history.
          </p>
        </div>
        <Button variant="primary" size="sm" onClick={openCreateModal} disabled={isLoadingVehicles || schedulableVehicles.length === 0}>
          Schedule Maintenance
        </Button>
      </div>

      {errorMessage && <div style={{ marginBottom: '12px' }}><Alert type="error" title="Maintenance Request Failed" message={errorMessage} /></div>}
      {successMessage && <div style={{ marginBottom: '12px' }}><Alert type="success" title="Maintenance Record Saved" message={successMessage} /></div>}

      <label htmlFor="maintenanceHistoryVehicle" style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '6px' }}>
        Vehicle maintenance history
      </label>
      <select
        id="maintenanceHistoryVehicle"
        value={selectedVehicleId}
        onChange={(event) => {
          setSelectedVehicleId(event.target.value);
          setErrorMessage(null);
          setSuccessMessage(null);
        }}
        disabled={isLoadingVehicles}
        className="filter-select-input"
        style={{ width: '100%', maxWidth: '520px', marginBottom: '16px' }}
      >
        {vehicles.length === 0 && <option value="">No vehicles available</option>}
        {vehicles.map((vehicle) => (
          <option key={vehicle.id} value={vehicle.id}>
            {vehicle.licensePlate} — {vehicle.year} {vehicle.make} {vehicle.model} ({vehicle.status})
          </option>
        ))}
      </select>

      {isLoadingHistory ? (
        <div style={{ padding: '24px', textAlign: 'center' }}>Loading maintenance history...</div>
      ) : records.length === 0 ? (
        <div style={{ padding: '28px', textAlign: 'center', border: '1px dashed #cbd5e1', borderRadius: '6px', color: '#64748b' }}>
          {selectedVehicle ? `No maintenance history exists for ${selectedVehicle.licensePlate}.` : 'Select a vehicle to view maintenance history.'}
        </div>
      ) : (
        <div className="table-responsive" style={{ overflowX: 'auto' }}>
          <table className="users-table" style={{ width: '100%' }}>
            <thead><tr><th>Scheduled</th><th>Status</th><th>Service information</th><th>Cost</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
            <tbody>
              {records.map((record) => {
                const transitions = getMaintenanceStatusTransitions(record.status);
                return (
                  <tr key={record.id}>
                    <td>{new Date(record.scheduledDateTime).toLocaleString()}</td>
                    <td><StatusBadge status={record.status} /></td>
                    <td>
                      <div style={{ fontWeight: 600 }}>{record.serviceInformation}</div>
                      {record.details && <div style={{ fontSize: '12px', color: '#64748b', marginTop: '3px' }}>{record.details}</div>}
                    </td>
                    <td>LKR {Number(record.cost || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', flexWrap: 'wrap', justifyContent: 'flex-end', gap: '6px' }}>
                        <Button variant="outline" size="sm" onClick={() => openEditModal(record)}>Edit Details</Button>
                        {transitions.map((status) => (
                          <Button
                            key={status}
                            variant={status === 'CANCELLED' ? 'outline' : 'secondary'}
                            size="sm"
                            disabled={updatingStatusId === record.id}
                            onClick={() => handleStatusUpdate(record, status)}
                          >
                            {formatStatus(status)}
                          </Button>
                        ))}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        isOpen={Boolean(form)}
        onClose={closeModal}
        title={editingRecord ? 'Update Maintenance Record' : 'Schedule Vehicle Maintenance'}
        subtitle={editingRecord ? `Record ${editingRecord.id}` : 'Create a persisted maintenance work order'}
      >
        {form && (
          <div style={{ display: 'grid', gap: '14px' }}>
            {formErrors.submit && <Alert type="error" title="Unable to Save" message={formErrors.submit} />}
            {!editingRecord && (
              <div>
                <label htmlFor="maintenanceVehicle">Vehicle</label>
                <select
                  id="maintenanceVehicle"
                  value={form.vehicleId}
                  onChange={(event) => {
                    setForm({ ...form, vehicleId: event.target.value });
                    setFormErrors(current => ({ ...current, vehicleId: undefined }));
                  }}
                  style={{ width: '100%', padding: '9px', marginTop: '5px' }}
                  data-autofocus
                  aria-invalid={Boolean(formErrors.vehicleId)}
                  aria-describedby={formErrors.vehicleId ? 'maintenanceVehicleError' : undefined}
                >
                  <option value="">Select a vehicle</option>
                  {schedulableVehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.licensePlate} — {vehicle.make} {vehicle.model}</option>)}
                </select>
                {formErrors.vehicleId && <div id="maintenanceVehicleError" className="field-error-message">{formErrors.vehicleId}</div>}
              </div>
            )}
            {!editingRecord && (
              <div>
                <label htmlFor="maintenanceScheduledDateTime">Maintenance date and time</label>
                <input
                  id="maintenanceScheduledDateTime"
                  type="datetime-local"
                  value={form.scheduledDateTime}
                  onChange={(event) => {
                    setForm({ ...form, scheduledDateTime: event.target.value });
                    setFormErrors(current => ({ ...current, scheduledDateTime: undefined }));
                  }}
                  style={{ width: '100%', padding: '9px', marginTop: '5px' }}
                  aria-invalid={Boolean(formErrors.scheduledDateTime)}
                  aria-describedby={formErrors.scheduledDateTime ? 'maintenanceScheduledDateTimeError' : undefined}
                />
                {formErrors.scheduledDateTime && <div id="maintenanceScheduledDateTimeError" className="field-error-message">{formErrors.scheduledDateTime}</div>}
              </div>
            )}
            <div>
              <label htmlFor="maintenanceServiceInformation">Required service information</label>
              <textarea
                id="maintenanceServiceInformation"
                value={form.serviceInformation}
                onChange={(event) => {
                  setForm({ ...form, serviceInformation: event.target.value });
                  setFormErrors(current => ({ ...current, serviceInformation: undefined }));
                }}
                rows="3"
                maxLength="2000"
                style={{ width: '100%', padding: '9px', marginTop: '5px' }}
                data-autofocus={editingRecord ? true : undefined}
                aria-invalid={Boolean(formErrors.serviceInformation)}
                aria-describedby={formErrors.serviceInformation ? 'maintenanceServiceInformationError' : undefined}
              />
              {formErrors.serviceInformation && <div id="maintenanceServiceInformationError" className="field-error-message">{formErrors.serviceInformation}</div>}
            </div>
            <div>
              <label htmlFor="maintenanceDetails">Progress / service details</label>
              <textarea
                id="maintenanceDetails"
                value={form.details}
                onChange={(event) => {
                  setForm({ ...form, details: event.target.value });
                  setFormErrors(current => ({ ...current, details: undefined }));
                }}
                rows="3"
                maxLength="4000"
                style={{ width: '100%', padding: '9px', marginTop: '5px' }}
                aria-invalid={Boolean(formErrors.details)}
                aria-describedby={formErrors.details ? 'maintenanceDetailsError' : undefined}
              />
              {formErrors.details && <div id="maintenanceDetailsError" className="field-error-message">{formErrors.details}</div>}
            </div>
            <div>
              <label htmlFor="maintenanceCost">Maintenance cost (LKR)</label>
              <input
                id="maintenanceCost"
                type="number"
                min="0"
                step="0.01"
                value={form.cost}
                onChange={(event) => {
                  setForm({ ...form, cost: event.target.value });
                  setFormErrors(current => ({ ...current, cost: undefined }));
                }}
                style={{ width: '100%', padding: '9px', marginTop: '5px' }}
                aria-invalid={Boolean(formErrors.cost)}
                aria-describedby={formErrors.cost ? 'maintenanceCostError' : undefined}
              />
              {formErrors.cost && <div id="maintenanceCostError" className="field-error-message">{formErrors.cost}</div>}
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <Button variant="outline" onClick={closeModal} disabled={isSaving}>Cancel</Button>
              <Button variant="primary" onClick={handleSave} disabled={isSaving}>{isSaving ? 'Saving...' : 'Save Maintenance Record'}</Button>
            </div>
          </div>
        )}
      </Modal>
    </section>
  );
};

export default MaintenanceWorkOrders;
