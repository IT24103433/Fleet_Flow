import React, { useRef, useState } from 'react';
import Modal from './Modal';
import Button from './Button';

export default function useConfirmAction() {
  const [confirmation, setConfirmation] = useState(null);
  const [pending, setPending] = useState(false);
  const submitting = useRef(false);
  const close = () => { if (!submitting.current) setConfirmation(null); };
  const confirm = async () => {
    if (!confirmation || submitting.current) return;
    submitting.current = true;
    setPending(true);
    try { await confirmation.action(); }
    finally { submitting.current = false; setPending(false); setConfirmation(null); }
  };
  return {
    requestConfirmation: setConfirmation,
    confirmationDialog: <Modal isOpen={Boolean(confirmation)} onClose={close} title={confirmation?.title}>
      <p className="confirmation-message">{confirmation?.message}</p>
      <div className="action-row">
        <Button variant="outline" onClick={close} disabled={pending}>Keep current state</Button>
        <Button variant={confirmation?.variant || 'primary'} onClick={confirm} isLoading={pending}>{confirmation?.label || 'Confirm change'}</Button>
      </div>
    </Modal>,
  };
}
