import { useEffect, useState } from 'react';
import { NavLink, Outlet, useNavigate, Link } from 'react-router-dom';
import Logo from './Logo';
import NovoEventoModal from './NovoEventoModal';

export default function AppLayout() {
  const [modalOpen,   setModalOpen]   = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const navigate = useNavigate();

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  const isStaff = user.perfil === 'Staff';
  const isAdmin = user.perfil === 'Administrador';

  /* Redirect Staff users away from /dashboard to /staff-dashboard */
  useEffect(() => {
    if (isStaff && window.location.pathname === '/dashboard') {
      navigate('/staff-dashboard', { replace: true });
    }
  }, []);

  function handleLogout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    navigate('/login');
  }

  function handleCreated(evento) {
    setModalOpen(false);
    setSidebarOpen(false);
    navigate(`/eventos/${evento.id}`);
  }

  function closeSidebar() { setSidebarOpen(false); }

  return (
    <div className="app-layout">
      {/* Mobile overlay */}
      {sidebarOpen && <div className="sidebar-overlay" onClick={closeSidebar} />}

      <aside className={`sidebar${sidebarOpen ? ' sidebar-open' : ''}`}>
        <div className="sidebar-top">
          <div className="sidebar-brand">
            <Logo size={24} />
          </div>

          <nav className="sidebar-nav">
            {isStaff ? (
              <NavLink
                to="/staff-dashboard"
                className={({ isActive }) => 'sidebar-link' + (isActive ? ' active' : '')}
                onClick={closeSidebar}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <circle cx="12" cy="7" r="4"/><path d="M4 21c0-4 3.6-7 8-7s8 3 8 7"/>
                </svg>
                As Minhas Tarefas
              </NavLink>
            ) : (
              <NavLink
                to="/dashboard"
                className={({ isActive }) => 'sidebar-link' + (isActive ? ' active' : '')}
                onClick={closeSidebar}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
                </svg>
                Eventos
              </NavLink>
            )}
          </nav>

          {isAdmin && (
            <NavLink
              to="/admin"
              className={({ isActive }) => 'sidebar-link' + (isActive ? ' active' : '')}
              onClick={closeSidebar}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
              </svg>
              Administração
            </NavLink>
          )}

          {!isStaff && (
            <button className="sidebar-new-btn" onClick={() => setModalOpen(true)}>
              <span>+</span> Novo Evento
            </button>
          )}
        </div>

        <div className="sidebar-footer">
          <div className="sidebar-user-info">
            <Link to="/perfil" className="sidebar-user-name sidebar-profile-link" onClick={closeSidebar}>
              {user.nome}
            </Link>
            {user.perfil && <span className="badge">{user.perfil}</span>}
          </div>
          <button className="btn-logout" onClick={handleLogout}>Sair</button>
        </div>
      </aside>

      <main className="app-content">
        <button
          className="sidebar-hamburger"
          onClick={() => setSidebarOpen(v => !v)}
          aria-label="Abrir menu"
        >
          <span /><span /><span />
        </button>
        <Outlet />
      </main>

      {modalOpen && (
        <NovoEventoModal onClose={() => setModalOpen(false)} onCreated={handleCreated} />
      )}
    </div>
  );
}
