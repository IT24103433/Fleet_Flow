import { useEffect, useRef, useState } from 'react';
import { DownloadSimpleIcon } from '@phosphor-icons/react/dist/csr/DownloadSimple';
import { useAuth } from '../../context/AuthContext';
import Button from '../../components/common/Button';
import Alert from '../../components/Alert';
import { reportClient } from '../../services/reportService';

export default function ReportExportButton({ path, filename, disabled }) {
  const { token } = useAuth();
  const key = `${token}:${path}`;
  const active = useRef(null);
  const [state, setState] = useState(null);
  const current = state?.key === key ? state : null;
  useEffect(() => () => { active.current?.abort(); }, [path, token]);
  async function download() {
    if (active.current && !active.current.signal.aborted) return;
    const controller = new AbortController(); active.current = controller;
    setState({ key, busy: true });
    try {
      const blob = await reportClient.exportReport(path, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a'); link.href = url; link.download = filename;
      document.body.appendChild(link);
      try { link.click(); } finally { link.remove(); window.setTimeout(() => URL.revokeObjectURL(url), 1000); }
      setState({ key, busy: false, message: 'CSV prepared. Check your downloads.' });
    } catch (error) {
      if (!controller.signal.aborted) setState({ key, busy: false, error: error.message });
    } finally { if (active.current === controller) active.current = null; }
  }
  return <div className="report-export-action"><Button disabled={disabled} isLoading={current?.busy} onClick={download}
    icon={<DownloadSimpleIcon size={18} weight="regular" aria-hidden="true" />}>Download CSV</Button>
    {current?.error && <Alert type="error" message={current.error} />}
    {current?.message && <p className="report-export-notice" role="status">{current.message}</p>}
  </div>;
}
