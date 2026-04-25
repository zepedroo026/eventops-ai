import { createContext, useCallback, useContext, useEffect, useState } from 'react';

const ToastCtx = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((msg, type = 'success') => {
    const id = Date.now() + Math.random();
    setToasts(p => [...p, { id, msg, type }]);
  }, []);

  function dismiss(id) {
    setToasts(p => p.filter(t => t.id !== id));
  }

  return (
    <ToastCtx.Provider value={addToast}>
      {children}
      <div className="toast-container" aria-live="polite" aria-atomic="false">
        {toasts.map(t => (
          <ToastItem key={t.id} toast={t} onDismiss={dismiss} />
        ))}
      </div>
    </ToastCtx.Provider>
  );
}

function ToastItem({ toast, onDismiss }) {
  useEffect(() => {
    const t = setTimeout(() => onDismiss(toast.id), 3800);
    return () => clearTimeout(t);
  }, []);

  const icon = toast.type === 'success' ? '✓' : toast.type === 'error' ? '✕' : 'ℹ';

  return (
    <div className={`toast toast-${toast.type}`} role="status">
      <span className="toast-icon">{icon}</span>
      <span className="toast-msg">{toast.msg}</span>
      <button className="toast-close" onClick={() => onDismiss(toast.id)} aria-label="Fechar">✕</button>
    </div>
  );
}

export const useToast = () => useContext(ToastCtx);
