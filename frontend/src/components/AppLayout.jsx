import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import NovoEventoModal from './NovoEventoModal';

export default function AppLayout() {
  const [modalOpen, setModalOpen] = useState(false);
  const navigate = useNavigate();

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  function handleLogout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    navigate('/login');
  }

  function handleCreated(evento) {
    setModalOpen(false);
    navigate(`/eventos/${evento.id}`);
  }

  return (
    <div className="app-layout">
      <aside className="sidebar">
        <div className="sidebar-top">
          <div className="sidebar-brand">
            <span className="sidebar-brand-icon">⚡</span>
            <span className="sidebar-brand-name">EventOps</span>
          </div>

          <nav className="sidebar-nav">
            <NavLink to="/dashboard" className={({ isActive }) => 'sidebar-link' + (isActive ? ' active' : '')}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
              </svg>
              Eventos
            </NavLink>
          </nav>

          <button className="sidebar-new-btn" onClick={() => setModalOpen(true)}>
            <span>+</span> Novo Evento
          </button>
        </div>

        <div className="sidebar-footer">
          <div className="sidebar-user-info">
            <span className="sidebar-user-name">{user.nome}</span>
            {user.perfil && <span className="badge">{user.perfil}</span>}
          </div>
          <button className="btn-logout" onClick={handleLogout}>Sair</button>
        </div>
      </aside>

      <main className="app-content">
        <Outlet />
      </main>

      {modalOpen && (
        <NovoEventoModal onClose={() => setModalOpen(false)} onCreated={handleCreated} />
      )}
    </div>
  );
}
