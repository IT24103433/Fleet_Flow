import { PlusIcon } from '@phosphor-icons/react/dist/csr/Plus';
import React, { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getAdminUsers } from '../../services/adminUserService';
import { getMaintenanceDashboard } from '../../services/maintenanceService';
import { FleetSnapshot, AttentionPreview, WorkspaceTasks } from '../StaffDashboardPage';
import { accountName } from '../../utils/accountPresentation';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import OperationalPulse from './OperationalPulse';
export default function AdminDashboardPage({
  onNavigate
}) {
  const {
    token,
    user
  } = useAuth();
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState(null);
  const key = `${token}:${revision}`;
  useEffect(() => {
    let active = true;
    Promise.all([getAdminUsers(token), getMaintenanceDashboard(token)]).then(([accounts, fleet]) => {
      if (active) setResult({
        key,
        accounts,
        fleet
      });
    });
    return () => {
      active = false;
    };
  }, [token, key]);
  const current = result?.key === key ? result : null;
  const accounts = current?.accounts.success ? current.accounts.data : null;
  const metrics = accounts ? [['Accounts', accounts.length], ['Enabled', accounts.filter(account => account.isActive === true).length], ['Disabled', accounts.filter(account => account.isActive === false).length]] : [];
  const actions = [['User directory', 'Review account details and manage access.', 'admin-users'], ['Create an account', 'Add a customer or staff member with the appropriate role.', 'admin-create-user'], ['Fleet reports', 'Review vehicle status, booking activity, and recorded maintenance.', 'fleet-performance'], ['Profile & security', 'Review your saved details and password settings.', 'staff-profile']];
  return <div className="admin-dashboard-container"><div className="account-heading"><div><p className="eyebrow">Administration workspace</p><h1>Welcome back, {accountName(user)}.</h1><p>Account access, fleet activity, and the tools to keep things organised.</p></div><Button onClick={() => onNavigate('admin-create-user')}>Create account <PlusIcon size={18} weight="regular" aria-hidden="true" /></Button></div><section className="workspace-snapshot" aria-label="Account overview"><div className="dashboard-section-header"><h2>Accounts at a glance</h2><Button variant="outline" size="sm" disabled={!current} onClick={() => setRevision(value => value + 1)}>Refresh</Button></div>{!current ? <p role="status">Loading account overview…</p> : !accounts ? <Alert type="error" title="Account overview unavailable" message={current.accounts.message || 'Please try again.'} /> : <div className="admin-account-metrics">{metrics.map(([label, value]) => <article className="stat-card" key={label}><span className="stat-label">{label}</span><strong className="workspace-metric">{value.toLocaleString()}</strong></article>)}</div>}</section><FleetSnapshot data={current?.fleet.data} loading={!current} error={current && !current.fleet.success ? current.fleet.message || 'Fleet overview unavailable.' : ''} onRefresh={() => setRevision(value => value + 1)} /><OperationalPulse onNavigate={onNavigate} /><div className="workspace-overview-grid"><AttentionPreview data={current?.fleet.data} loading={!current} error={current && !current.fleet.success} onNavigate={onNavigate} /><WorkspaceTasks actions={actions.map(([title, text, view]) => ({ title, text, view }))} onNavigate={onNavigate} label="Administration shortcuts" /></div></div>;
}
