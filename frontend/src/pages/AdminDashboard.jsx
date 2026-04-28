import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';

const fmtDate = s => new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const fmtCur  = v => Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });

const PERFIL_COLORS = {
  Administrador: { bg: 'rgba(239,68,68,.1)',   color: '#ef4444',  border: 'rgba(239,68,68,.25)'  },
  Organizador:   { bg: 'rgba(170,59,255,.1)',  color: '#aa3bff',  border: 'rgba(170,59,255,.25)' },
  Staff:         { bg: 'rgba(34,197,94,.1)',   color: '#22c55e',  border: 'rgba(34,197,94,.25)'  },
};

function PerfilBadge({ perfil }) {
  const s = PERFIL_COLORS[perfil] ?? PERFIL_COLORS.Staff;
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '2px 9px', borderRadius: 99,
      background: s.bg, color: s.color,
      border: `1px solid ${s.border}`,
      letterSpacing: '.04em',
    }}>
      {perfil}
    </span>
  );
}

export default function AdminDashboard() {
  const navigate = useNavigate();
  const [utilizadores, setUtilizadores] = useState([]);
  const [stats,        setStats]        = useState(null);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState('');

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  /* Guard: only Administrador */
  useEffect(() => {
    if (user.perfil !== 'Administrador') {
      navigate('/dashboard', { replace: true });
    }
  }, []);

  useEffect(() => {
    if (user.perfil !== 'Administrador') return;

    Promise.all([
      api.get('/admin/utilizadores'),
      api.get('/admin/stats'),
    ])
      .then(([u, s]) => { setUtilizadores(u); setStats(s); })
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token');
          navigate('/login');
        } else {
          setError('Não foi possível carregar os dados de administração.');
        }
      })
      .finally(() => setLoading(false));
  }, []);

  if (user.perfil !== 'Administrador') return null;

  /* ── Loading ── */
  if (loading) return (
    <div className="dash-page">
      <div className="skeleton" style={{ width: 200, height: 28, marginBottom: 32 }} />
      <div className="metrics-grid metrics-grid-4" style={{ marginBottom: 28 }}>
        {[1,2,3,4].map(i => <div key={i} className="metric-card skeleton" style={{ height: 80 }} />)}
      </div>
      <div className="skeleton" style={{ height: 260, borderRadius: 12 }} />
    </div>
  );

  if (error) return (
    <div className="dash-state error"><p>{error}</p></div>
  );

  /* group counts by perfil */
  const byPerfil = utilizadores.reduce((acc, u) => {
    acc[u.perfil] = (acc[u.perfil] ?? 0) + 1;
    return acc;
  }, {});

  return (
    <div className="dash-page">
      {/* ── Cabeçalho ── */}
      <div className="dash-title-row">
        <h2>Administração</h2>
        <span className="badge">Administrador</span>
      </div>

      {/* ── Métricas globais ── */}
      {stats && (
        <div className="metrics-grid metrics-grid-4">
          <div className="metric-card">
            <span className="metric-value">{stats.totalEventos}</span>
            <span className="metric-label">Total de Eventos</span>
          </div>
          <div className="metric-card">
            <span className="metric-value metric-accent">{stats.totalUtilizadores}</span>
            <span className="metric-label">Utilizadores</span>
          </div>
          <div className="metric-card">
            <span className="metric-value">{stats.totalStaff}</span>
            <span className="metric-label">Total de Staff</span>
          </div>
          <div className="metric-card">
            <span className="metric-value metric-accent" style={{ fontSize: 20 }}>
              {fmtCur(stats.totalDespesas)}
            </span>
            <span className="metric-label">Total de Despesas</span>
          </div>
        </div>
      )}

      {/* ── Utilizadores registados ── */}
      <div className="admin-section">
        <div className="section-title-row" style={{ marginBottom: 16 }}>
          <h3>Utilizadores Registados</h3>
          <span className="section-count">{utilizadores.length}</span>
          {Object.entries(byPerfil).map(([perfil, count]) => (
            <PerfilBadge key={perfil} perfil={`${perfil} (${count})`} />
          ))}
        </div>

        {utilizadores.length === 0 ? (
          <p className="section-empty">Nenhum utilizador registado.</p>
        ) : (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Utilizador</th>
                  <th>Email</th>
                  <th>Perfil</th>
                  <th style={{ whiteSpace: 'nowrap' }}>Registo</th>
                </tr>
              </thead>
              <tbody>
                {utilizadores.map(u => (
                  <tr key={u.id}>
                    <td>
                      <div className="admin-user-cell">
                        <div className="admin-avatar">{u.nome.charAt(0).toUpperCase()}</div>
                        <span className="admin-user-nome">{u.nome}</span>
                      </div>
                    </td>
                    <td>
                      <span className="admin-email">{u.email}</span>
                    </td>
                    <td>
                      <PerfilBadge perfil={u.perfil} />
                    </td>
                    <td>
                      <span className="admin-date">{fmtDate(u.criadoEm)}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
