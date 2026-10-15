import { ArrowUpRightIcon } from "@phosphor-icons/react/dist/csr/ArrowUpRight";
import React, { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import Button from '../components/common/Button';
import Alert from '../components/Alert';
import FleetDistribution from '../components/common/FleetDistribution';
import { accountName } from '../utils/accountPresentation';
import { getMaintenanceDashboard } from '../services/maintenanceService';
export function FleetSnapshot({
  data,
  loading,
  error,
  onRefresh,
  compact = false
}) {
  const metrics = [['Total vehicles', 'totalVehicles'], ['Available', 'available'], ['In use', 'inUse'], ['In maintenance', 'undergoingMaintenance']];
  return <section className="workspace-snapshot" aria-labelledby="snapshot-title">
    <div className="dashboard-section-header"><h3 id="snapshot-title">Fleet at a glance</h3><Button variant="outline" size="sm" onClick={onRefresh} disabled={loading}>Refresh</Button></div>
    {loading ? <p role="status">Loading fleet overview…</p> : error ? <Alert type="error" message={error} /> : <>
      {!compact && <div className="metrics-grid">{metrics.map(([label, key]) => <div className="stat-card" key={key}><span className="stat-label">{label}</span><strong className="workspace-metric">{typeof data?.statusCounts?.[key] === 'number' ? data.statusCounts[key].toLocaleString() : 'Unavailable'}</strong></div>)}</div>}
      {data?.statusCounts?.totalVehicles === 0 && <p className="workspace-empty">No vehicles have been added yet.</p>}
      <FleetDistribution counts={data?.statusCounts} />
    </>}
  </section>;
}
export function AttentionPreview({ data, loading, error, onNavigate }) {
  const items = Array.isArray(data?.attentionItems) ? data.attentionItems : null;
  return <section className="workspace-attention" aria-labelledby="attention-title"><div className="dashboard-section-header"><h2 id="attention-title">Needs attention</h2><Button variant="ghost" size="sm" onClick={() => onNavigate('maintenance-dashboard')}>View queue</Button></div>
    {loading ? <p role="status">Loading attention queue…</p> : error ? <Alert type="error" message="The attention queue is unavailable. Please refresh the overview." /> : !items ? <p className="workspace-empty">Attention data unavailable.</p> : !items.length ? <p className="workspace-empty">No vehicles in the current attention queue.</p> : <ul>{items.slice(0,5).map(item => <li key={item.id}><div><strong>{item.year} {item.make} {item.model}</strong><p>{String(item.attentionReason || '').replace(/([a-z])([A-Z])/g, '$1 $2')}</p><span className="plate-pill">{item.licensePlate || item.vin}</span></div><span className="attention-urgency">{item.urgency}</span></li>)}</ul>}
  </section>;
}
export function WorkspaceTasks({ actions, onNavigate, label = 'Workspace tasks' }) {
  return <section className="workspace-task-panel" aria-label={label}><h2>Quick access</h2><ul>{actions.map(action => <li key={action.view}><button type="button" className="workspace-action" onClick={() => onNavigate(action.view)}><span><strong>{action.title}</strong><span>{action.text}</span></span><ArrowUpRightIcon size={18} weight="regular" aria-hidden="true" /></button></li>)}</ul></section>;
}
export default function StaffDashboardPage({
  onNavigate
}) {
  const {
    user,
    roles,
    token
  } = useAuth();
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState(null);
  const key = `${token}:${revision}`;
  useEffect(() => {
    let active = true;
    getMaintenanceDashboard(token).then(response => {
      if (active) setResult({
        key,
        data: response.success ? response.data : null,
        error: response.success ? '' : 'We couldn’t load the fleet overview. Please try again.'
      });
    });
    return () => {
      active = false;
    };
  }, [token, key]);
  const current = result?.key === key ? result : null;
  const canManage = roles?.some(role => ['ADMIN', 'FLEET_MANAGER'].includes(String(role).toUpperCase()));
  const actions = [...(canManage ? [{
    title: 'Vehicle inventory',
    text: 'Review vehicles, pricing, and operational status.',
    view: 'manage-fleet'
  }] : []), {
    title: 'Maintenance workspace',
    text: 'Review the attention queue and manage work orders.',
    view: 'maintenance-dashboard'
  }, {
    title: 'Maintenance reports',
    text: 'Review recorded work orders and maintenance costs.',
    view: 'maintenance-reports'
  }, {
    title: 'Profile & security',
    text: 'Review your profile and account settings.',
    view: 'staff-profile'
  }];
  return <div className="staff-dashboard-container">
    <div className="workspace-welcome"><p className="landing-eyebrow">Your operations workspace</p><h1>Welcome back, {accountName(user)}.</h1><p>{canManage ? 'Keep the fleet organised, monitor vehicle status, and plan maintenance.' : 'Review vehicle maintenance needs and keep work orders up to date.'}</p></div>
    <FleetSnapshot data={current?.data} loading={!current} error={current?.error} onRefresh={() => setRevision(value => value + 1)} />
    <div className="workspace-overview-grid"><AttentionPreview data={current?.data} loading={!current} error={current?.error} onNavigate={onNavigate} /><WorkspaceTasks actions={actions} onNavigate={onNavigate} label="Operations shortcuts" /></div>
  </div>;
}
