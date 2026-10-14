import React, { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getVehicleById } from '../../services/vehicleService';
import { getAdminUsers } from '../../services/adminUserService';
import Alert from '../Alert';
import Button from './Button';

export default function RecordPage({ kind, id, onNavigate, children, fallbackView }) {
  const { token, isLoading } = useAuth();
  const [revision, setRevision] = useState(0);
  const key = `${kind}:${id}:${token}:${revision}`;
  const [result, setResult] = useState(null);
  useEffect(() => {
    let active = true;
    if (!id || isLoading || (kind === 'user' && !token)) return;
    const request = kind === 'vehicle' ? getVehicleById(id) : getAdminUsers(token);
    request.then(response => {
      if (!active) return;
      const record = response.success ? (kind === 'vehicle' ? response.data : response.data?.find(item => item.id === id)) : null;
      setResult({ key, record, error: !response.success && response.status !== 404 ? response.message || 'We couldn’t load this record. Please try again.' : '' });
    });
    return () => { active = false; };
  }, [id, kind, token, key, isLoading]);
  const current = result?.key === key ? result : null;
  if (id && !current) return <p className="page-loading" role="status">Loading {kind === 'vehicle' ? 'vehicle' : 'account'} details…</p>;
  if (!current?.record) return <section className="record-empty"><p className="eyebrow">Record unavailable</p><h2>{kind === 'vehicle' ? 'Choose a vehicle to continue' : 'Choose an account to continue'}</h2><p>Open an existing record from the {kind === 'vehicle' ? 'fleet' : 'user directory'}, or retry if the service is unavailable.</p>{current?.error && <Alert type="error" message={current.error} />}<div className="action-row"><Button onClick={() => onNavigate(fallbackView || (kind === 'vehicle' ? 'manage-fleet' : 'admin-users'))}>Return to {kind === 'vehicle' ? 'fleet' : 'user directory'}</Button>{id && current?.error && <Button variant="outline" onClick={() => setRevision(value => value + 1)}>Try again</Button>}</div></section>;
  return children(current.record);
}
