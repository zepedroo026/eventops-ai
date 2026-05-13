import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useToast } from '../components/Toast';

const fmtDate = s => new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const fmtCur  = v => Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });

const PERFIL_COLORS = {
  Administrador: { bg: 'rgba(239,68,68,.1)',  color: '#ef4444', border: 'rgba(239,68,68,.25)'  },
  Organizador:   { bg: 'rgba(170,59,255,.1)', color: '#aa3bff', border: 'rgba(170,59,255,.25)' },
  Staff:         { bg: 'rgba(34,197,94,.1)',  color: '#22c55e', border: 'rgba(34,197,94,.25)'  },
};
const PERFIL_NOMES  = { 0: 'Administrador', 1: 'Organizador', 2: 'Staff' };
const PERFIL_VALUES = { Administrador: 0, Organizador: 1, Staff: 2 };

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
  const toast    = useToast();

  const [utilizadores, setUtilizadores] = useState([]);
  const [stats,        setStats]        = useState(null);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState('');
  const [busyIds,      setBusyIds]      = useState(new Set());

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  /* Guard: only Administrador */
  useEffect(() => {
    if (user.perfil !== 'Administrador') navigate('/dashboard', { replace: true });
  }, []);

  useEffect(() => {
    if (user.perfil !== 'Administrador') return;
    Promise.all([api.get('/admin/utilizadores'), api.get('/admin/stats')])
      .then(([u, s]) => { setUtilizadores(u); setStats(s); })
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token'); navigate('/login');
        } else {
          setError('Não foi possível carregar os dados de administração.');
        }
      })
      .finally(() => setLoading(false));
  }, []);

  if (user.perfil !== 'Administrador') return null;

  /* ── helpers ── */
  function setBusy(id, v) {
    setBusyIds(p => { const n = new Set(p); v ? n.add(id) : n.delete(id); return n; });
  }

  async function handleAlterarPerfil(u, novoPerfil) {
    const nomeNovoPerfil = PERFIL_NOMES[novoPerfil];
    if (!window.confirm(`Alterar o perfil de "${u.nome}" para "${nomeNovoPerfil}"?`)) return;
    setBusy(u.id, true);
    try {
      await api.put(`/admin/utilizadores/${u.id}/perfil`, { perfil: novoPerfil });
      setUtilizadores(p => p.map(x => x.id === u.id ? { ...x, perfil: nomeNovoPerfil } : x));
      toast(`Perfil de "${u.nome}" alterado para ${nomeNovoPerfil}.`);
    } catch (e) {
      toast(e.message || 'Erro ao alterar perfil.', 'error');
    } finally {
      setBusy(u.id, false);
    }
  }

  async function handleToggleBloquear(u) {
    const acao = u.bloqueado ? 'desbloquear' : 'bloquear';
    if (!window.confirm(`Confirmar ${acao} a conta de "${u.nome}"?`)) return;
    setBusy(u.id, true);
    try {
      const res = await api.put(`/admin/utilizadores/${u.id}/bloquear`, {});
      setUtilizadores(p => p.map(x => x.id === u.id ? { ...x, bloqueado: res.bloqueado } : x));
      toast(`Conta de "${u.nome}" ${res.bloqueado ? 'bloqueada' : 'desbloqueada'} com sucesso.`);
    } catch (e) {
      toast(e.message || 'Erro ao alterar estado da conta.', 'error');
    } finally {
      setBusy(u.id, false);
    }
  }

  async function handleRemover(u) {
    if (!window.confirm(`Remover o utilizador "${u.nome}"?\nEsta ação não pode ser desfeita.`)) return;
    setBusy(u.id, true);
    try {
      await api.delete(`/admin/utilizadores/${u.id}`);
      setUtilizadores(p => p.filter(x => x.id !== u.id));
      toast(`Utilizador "${u.nome}" removido.`);
    } catch (e) {
      toast(e.message || 'Erro ao remover utilizador.', 'error');
    } finally {
      setBusy(u.id, false);
    }
  }

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
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {utilizadores.map(u => {
                  const isSelf = u.email === user.email;
                  const busy   = busyIds.has(u.id);
                  return (
                    <tr key={u.id} className={u.bloqueado ? 'admin-row-blocked' : ''}>
                      {/* Utilizador */}
                      <td>
                        <div className="admin-user-cell">
                          <div className="admin-avatar" style={u.bloqueado ? { opacity: .5 } : undefined}>
                            {u.nome.charAt(0).toUpperCase()}
                          </div>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                            <span className="admin-user-nome" style={u.bloqueado ? { opacity: .6 } : undefined}>
                              {u.nome}
                            </span>
                            {u.bloqueado && (
                              <span className="admin-blocked-badge">Bloqueado</span>
                            )}
                          </div>
                          {isSelf && <span className="admin-self-badge">Eu</span>}
                        </div>
                      </td>

                      {/* Email */}
                      <td><span className="admin-email">{u.email}</span></td>

                      {/* Perfil */}
                      <td><PerfilBadge perfil={u.perfil} /></td>

                      {/* Registo */}
                      <td><span className="admin-date">{fmtDate(u.criadoEm)}</span></td>

                      {/* Ações */}
                      <td>
                        {isSelf ? (
                          <span style={{ fontSize: 12, color: 'var(--text)' }}>—</span>
                        ) : (
                          <div className="admin-actions">
                            {/* Alterar perfil */}
                            <select
                              className="admin-perfil-select"
                              value={PERFIL_VALUES[u.perfil] ?? 2}
                              disabled={busy}
                              onChange={e => handleAlterarPerfil(u, Number(e.target.value))}
                              title="Alterar perfil"
                            >
                              <option value={0}>Administrador</option>
                              <option value={1}>Organizador</option>
                              <option value={2}>Staff</option>
                            </select>

                            {/* Bloquear / Desbloquear */}
                            <button
                              className={`admin-btn-block ${u.bloqueado ? 'unblock' : 'block'}`}
                              disabled={busy}
                              onClick={() => handleToggleBloquear(u)}
                              title={u.bloqueado ? 'Desbloquear conta' : 'Bloquear conta'}
                            >
                              {u.bloqueado ? 'Desbloquear' : 'Bloquear'}
                            </button>

                            {/* Remover */}
                            <button
                              className="admin-btn-remove"
                              disabled={busy}
                              onClick={() => handleRemover(u)}
                              title="Remover utilizador"
                            >
                              Remover
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
