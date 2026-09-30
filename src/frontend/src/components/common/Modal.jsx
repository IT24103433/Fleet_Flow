import React, { useEffect, useEffectEvent, useId, useRef } from 'react';

const Modal = ({
  isOpen,
  onClose,
  title,
  subtitle,
  children,
  footer,
  maxWidth = '520px',
}) => {
  const modalRef = useRef(null);
  const previouslyFocusedRef = useRef(null);
  const titleId = useId();
  const subtitleId = useId();
  const closeFromKeyboard = useEffectEvent(() => onClose());

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        closeFromKeyboard();
      }
    };
    if (isOpen) {
      previouslyFocusedRef.current = document.activeElement;
      const previousOverflow = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
      window.addEventListener('keydown', handleKeyDown);
      const focusTimer = window.setTimeout(() => {
        const initialFocus = modalRef.current?.querySelector('[data-autofocus]:not([disabled])') ||
          modalRef.current?.querySelector('input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), button:not([disabled])');
        (initialFocus || modalRef.current)?.focus();
      }, 0);
      return () => {
        window.clearTimeout(focusTimer);
        document.body.style.overflow = previousOverflow;
        window.removeEventListener('keydown', handleKeyDown);
        if (previouslyFocusedRef.current?.isConnected) previouslyFocusedRef.current.focus?.();
      };
    }
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        ref={modalRef}
        className="modal-container"
        style={{ maxWidth }}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={subtitle ? subtitleId : undefined}
        tabIndex={-1}
      >
        <div className="modal-header">
          <div className="modal-title-group">
            <h3 className="modal-title" id={titleId}>{title}</h3>
            {subtitle && <p className="modal-subtitle" id={subtitleId}>{subtitle}</p>}
          </div>
          <button
            type="button"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Close dialog"
          >
            <svg viewBox="0 0 24 24" width="20" height="20" stroke="currentColor" strokeWidth="2" fill="none" aria-hidden="true">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="modal-body">
          {children}
        </div>

        {footer && (
          <div className="modal-footer">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
};

export default Modal;
