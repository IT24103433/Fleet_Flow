import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { reportClient } from '../../services/reportService';

export default function useReport(path) {
  const { token } = useAuth();
  const [revision, setRevision] = useState(0);
  const key = `${token}:${path}:${revision}`;
  const [result, setResult] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    reportClient.getReport(path, controller.signal).then(data => {
      if (!controller.signal.aborted) setResult({ key, data, error: '' });
    }).catch(error => {
      if (!controller.signal.aborted) setResult({ key, data: null, error: error.message });
    });
    return () => controller.abort();
  }, [key, path]);
  // Do not display a previous user's or previous filter's report while a fresh query loads.
  const current = result?.key === key ? result : null;
  return { data: current?.data, error: current?.error, loading: !current,
    refresh: () => setRevision(value => value + 1) };
}
