import { auditChanges, auditValue } from '../../utils/maintenanceAudit';
import { useEffect, useRef, useState } from 'react';
import { CalendarBlankIcon } from '@phosphor-icons/react/dist/csr/CalendarBlank';
import { UserCircleIcon } from '@phosphor-icons/react/dist/csr/UserCircle';
import { ClockCounterClockwiseIcon } from '@phosphor-icons/react/dist/csr/ClockCounterClockwise';
import Button from '../common/Button';
import Modal from '../common/Modal';
import Alert from '../Alert';
import { assignMaintenance, getMaintenanceAudit, getMaintenanceStaff, rescheduleMaintenance } from '../../services/maintenanceService';
import { validateMaintenanceWindow } from '../../validation/maintenanceValidation';
import './MaintenancePlanning.css';
const localInput = value => value ? new Date(new Date(value).getTime() - new Date(value).getTimezoneOffset() * 60000).toISOString().slice(0, 16) : '';
const date = value => value ? new Date(value).toLocaleString('en-GB') : 'Not recorded';
export default function MaintenancePlanActions({ record, token, canAssign = false, onSaved }) {
  const [mode, setMode] = useState(null);
  const [values, setValues] = useState({});
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const guard = useRef(false);
  const mounted = useRef(false);
  const read = useRef(null);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; read.current?.abort(); }; }, []);
  function close() { if (guard.current) return; read.current?.abort(); setMode(null); setError(null); }
  async function open(next) {
    read.current?.abort(); const controller = new AbortController(); read.current = controller;
    setMode(next); setError(null); setData(null);
    setValues({ assignedStaffId: record.assignedStaffId || '', scheduledDateTime: localInput(record.scheduledDateTime), plannedEndDateTime: localInput(record.plannedEndDateTime) });
    if (next === 'schedule') return;
    const result = next === 'assignment' ? await getMaintenanceStaff(token, controller.signal) : await getMaintenanceAudit(token, record.id, controller.signal);
    if (!controller.signal.aborted && mounted.current) {
      if (result.success) setData(result.data || []); else setError(result.message || 'The requested records are unavailable.');
    }
  }
  async function save() {
    if (guard.current) return;
    if (mode === 'schedule') {
      const errors = validateMaintenanceWindow(values);
      if (Object.keys(errors).length) { setError(Object.values(errors).join(' ')); return; }
    }
    guard.current = true; setBusy(true); setError(null);
    const result = mode === 'assignment' ? await assignMaintenance(token, record.id, values.assignedStaffId || null) : await rescheduleMaintenance(token, record.id, {
      scheduledDateTime: new Date(values.scheduledDateTime).toISOString(), plannedEndDateTime: new Date(values.plannedEndDateTime).toISOString() });
    guard.current = false;
    if (!mounted.current) return;
    setBusy(false);
    if (!result.success) { setError(result.message || 'The change was not saved.'); return; }
    setMode(null); onSaved?.(result.data);
  }
  const titles = { schedule: 'Plan the maintenance window', assignment: 'Assign maintenance work', audit: 'Maintenance activity history' };
  const openWork = ['SCHEDULED', 'IN_PROGRESS'].includes(record.status);
  return <>
    {record.status === 'SCHEDULED' && <Button variant="outline" size="sm" onClick={() => open('schedule')}><CalendarBlankIcon aria-hidden="true" weight="regular" size={16} />Plan window</Button>}
    {canAssign && openWork && <Button variant="outline" size="sm" onClick={() => open('assignment')}><UserCircleIcon aria-hidden="true" weight="regular" size={16} />Assign staff</Button>}
    <Button variant="outline" size="sm" onClick={() => open('audit')}><ClockCounterClockwiseIcon aria-hidden="true" weight="regular" size={16} />Activity</Button>
    <Modal isOpen={Boolean(mode)} onClose={close} title={titles[mode]} subtitle={`${record.vehicleLicensePlate} · ${record.serviceInformation}`}>
      <div className="maintenance-plan-dialog">
        {error && <Alert type="error" message={error} />}
        {mode === 'schedule' && <><p>Bookings outside this window stay available. Conflicting periods cannot be saved.</p>{[['scheduledDateTime', 'Planned start'], ['plannedEndDateTime', 'Planned end']].map(([key, label]) => <label key={key} htmlFor={`plan-${key}`}>{label}<input id={`plan-${key}`} type="datetime-local" value={values[key] || ''} onChange={event => setValues(previous => ({ ...previous, [key]: event.target.value }))} disabled={busy} required /></label>)}</>}
        {mode === 'assignment' && <>{data === null && !error ? <p role="status">Loading active Maintenance Staff…</p> : data && <><label htmlFor="assigned-maintenance-staff">Maintenance Staff<select id="assigned-maintenance-staff" value={values.assignedStaffId || ''} onChange={event => setValues(previous => ({ ...previous, assignedStaffId: event.target.value }))} disabled={busy}><option value="">Unassigned</option>{record.assignedStaffId && !data.some(account => account.id === record.assignedStaffId) && <option value={record.assignedStaffId} disabled>Current assignment unavailable</option>}{data.map(account => <option key={account.id} value={account.id}>{account.fullName || account.username}</option>)}</select></label><p>Identity rechecks account status and role when this assignment is saved.</p>{data.length === 0 && <p>No active Maintenance Staff accounts are available.</p>}</>}</>}
        {mode === 'audit' && <>{data === null && !error ? <p role="status">Loading saved activity…</p> : data?.length === 0 ? <p>No audit entries were recorded for this historical work order.</p> : data && <ol className="maintenance-audit-list">{[...data].reverse().map(entry => <li key={entry.id}><div className="maintenance-audit-heading"><strong>{({ Created: 'Work order created', DetailsUpdated: 'Details updated', Rescheduled: 'Window changed', Assigned: 'Assignment changed', StatusChanged: 'Status changed' })[entry.action] || entry.action}</strong><time>{date(entry.occurredAt)}</time></div><p>Recorded by staff #{entry.actorId.slice(0, 8)}</p><details open={entry.action !== 'Created'}><summary>Saved changes</summary><dl>{auditChanges(entry).map(change => <div key={change.label}><dt>{change.label}</dt><dd><span>{auditValue(change.label, change.before)}</span><strong>{auditValue(change.label, change.after)}</strong></dd></div>)}</dl></details></li>)}</ol>}</>}
        <div className="modal-actions-row"><Button variant="outline" onClick={close} disabled={busy} data-autofocus>{mode === 'audit' ? 'Close history' : 'Cancel'}</Button>{mode !== 'audit' && <Button onClick={save} isLoading={busy} disabled={mode === 'assignment' && data === null}>{mode === 'assignment' ? 'Save assignment' : 'Save window'}</Button>}</div>
      </div>
    </Modal>
  </>;
}
