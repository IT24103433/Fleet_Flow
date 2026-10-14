const fields = { ScheduledDateTime: 'Planned start', PlannedEndDateTime: 'Planned end', AssignedStaffId: 'Staff reference', AssignedStaffName: 'Assigned staff', ServiceInformation: 'Service', Details: 'Details', Cost: 'Cost (LKR)', Status: 'Status', CompletedAt: 'Completed at' };
export function auditChanges(entry) {
  try {
    const before = entry.before ? JSON.parse(entry.before) : {};
    const after = JSON.parse(entry.after);
    return Object.entries(fields).filter(([key]) => (before[key] ?? null) !== (after[key] ?? null)).map(([key, label]) => ({ label,
      before: before[key] ?? 'Not recorded', after: after[key] ?? 'Not recorded' }));
  } catch { return []; }
}

export function auditValue(label, value) {
  if (value === 'Not recorded') return value;
  if (label === 'Planned start' || label === 'Planned end' || label === 'Completed at') return new Date(value).toLocaleString('en-GB');
  if (label === 'Staff reference') return `#${String(value).slice(0, 8)}`;
  if (label === 'Status') return String(value).toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase());
  if (label === 'Cost (LKR)') return `LKR ${Number(value).toLocaleString('en-LK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  return String(value);
}
