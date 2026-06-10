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

function classifyEvento(ev) {
  const now   = Date.now();
  const start = new Date(ev.dataInicio).getTime();
  const end   = new Date(ev.dataFim).getTime();
  if (ev.estado === 'Pendente')   return 'pendentes';
  if (ev.estado === 'Rejeitado')  return 'passados';
  if (now < start) return 'futuros';
  if (now > end)   return 'passados';
  return 'decorrer';
}

const TABS = [
  { key: 'pendentes', label: 'Pendentes' },
  { key: 'decorrer',  label: 'A Decorrer' },
  { key: 'futuros',   label: 'Futuros' },
  { key: 'passados',  label: 'Passados' },
];

export default function AdminDashboard() {
  const navigate = useNavigate();
  const toast    = useToast();

  const [utilizadores, setUtilizadores] = useState([]);
  const [stats,        setStats]        = useState(null);
  const [eventos,      setEventos]      = useState([]);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState('');
  const [busyIds,      setBusyIds]      = useState(new Set());
  const [busyEvIds,    setBusyEvIds]    = useState(new Set());
  const [activeTab,    setActiveTab]    = useState('pendentes');

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
    Promise.all([
      api.get('/admin/utilizadores'),
      api.get('/admin/stats'),
      api.get('/eventos'),
    ])
      .then(([u, s, ev]) => { setUtilizadores(u); setStats(s); setEventos(ev ?? []); })
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

  /* ── user helpers ── */
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

  async function handleRemoverUtilizador(u) {
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

  /* ── event helpers ── */
  function setBusyEv(id, v) {
    setBusyEvIds(p => { const n = new Set(p); v ? n.add(id) : n.delete(id); return n; });
  }

  async function handleAprovar(ev) {
    setBusyEv(ev.id, true);
    try {
      await api.put(`/admin/eventos/${ev.id}/aprovar`, {});
      setEventos(p => p.map(x => x.id === ev.id ? { ...x, estado: 'Aprovado' } : x));
      toast(`Evento "${ev.nome}" aprovado.`);
    } catch (e) {
      toast(e.message || 'Erro ao aprovar evento.', 'error');
    } finally {
      setBusyEv(ev.id, false);
    }
  }

  async function handleRejeitar(ev) {
    setBusyEv(ev.id, true);
    try {
      await api.put(`/admin/eventos/${ev.id}/rejeitar`, {});
      setEventos(p => p.map(x => x.id === ev.id ? { ...x, estado: 'Rejeitado' } : x));
      toast(`Evento "${ev.nome}" rejeitado.`);
    } catch (e) {
      toast(e.message || 'Erro ao rejeitar evento.', 'error');
    } finally {
      setBusyEv(ev.id, false);
    }
  }

  async function handleEliminarEvento(ev) {
    if (!window.confirm(`Eliminar o evento "${ev.nome}"?\nEsta ação não pode ser desfeita.`)) return;
    setBusyEv(ev.id, true);
    try {
      await api.delete(`/eventos/${ev.id}`);
      setEventos(p => p.filter(x => x.id !== ev.id));
      toast(`Evento "${ev.nome}" eliminado.`);
    } catch (e) {
      toast(e.message || 'Erro ao eliminar evento.', 'error');
    } finally {
      setBusyEv(ev.id, false);
    }
  }

  /* ── derived data ── */
  const byPerfil  = utilizadores.reduce((acc, u) => { acc[u.perfil] = (acc[u.perfil] ?? 0) + 1; return acc; }, {});
  const tabCounts = TABS.reduce((acc, t) => {
    acc[t.key] = eventos.filter(ev => classifyEvento(ev) === t.key).length;
    return acc;
  }, {});
  const eventosFiltrados = eventos.filter(ev => classifyEvento(ev) === activeTab);

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

  if (error) return <div className="dash-state error"><p>{error}</p></div>;

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

      {/* ══════════════════════════════════════════════════════════
          UTILIZADORES
          ══════════════════════════════════════════════════════════ */}
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
                      <td>
                        <div className="admin-user-cell">
                          <div className="admin-avatar" style={u.bloqueado ? { opacity: .5 } : undefined}>
                            {u.nome.charAt(0).toUpperCase()}
                          </div>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                            <span className="admin-user-nome" style={u.bloqueado ? { opacity: .6 } : undefined}>
                              {u.nome}
                            </span>
                            {u.bloqueado && <span className="admin-blocked-badge">Bloqueado</span>}
                          </div>
                          {isSelf && <span className="admin-self-badge">Eu</span>}
                        </div>
                      </td>
                      <td><span className="admin-email">{u.email}</span></td>
                      <td><PerfilBadge perfil={u.perfil} /></td>
                      <td><span className="admin-date">{fmtDate(u.criadoEm)}</span></td>
                      <td>
                        {isSelf ? (
                          <span style={{ fontSize: 12, color: 'var(--text)' }}>—</span>
                        ) : (
                          <div className="admin-actions">
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
                            <button
                              className={`admin-btn-block ${u.bloqueado ? 'unblock' : 'block'}`}
                              disabled={busy}
                              onClick={() => handleToggleBloquear(u)}
                              title={u.bloqueado ? 'Desbloquear conta' : 'Bloquear conta'}
                            >
                              {u.bloqueado ? 'Desbloquear' : 'Bloquear'}
                            </button>
                            <button
                              className="admin-btn-remove"
                              disabled={busy}
                              onClick={() => handleRemoverUtilizador(u)}
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

      {/* ══════════════════════════════════════════════════════════
          GESTÃO DE EVENTOS
          ══════════════════════════════════════════════════════════ */}
      <div className="admin-section">
        <div className="section-title-row" style={{ marginBottom: 16 }}>
          <h3>Gestão de Eventos</h3>
          <span className="section-count">{eventos.length}</span>
        </div>

        {/* ── Tabs ── */}
        <div className="admin-event-tabs">
          {TABS.map(t => (
            <button
              key={t.key}
              className={`admin-event-tab${activeTab === t.key ? ' active' : ''}`}
              onClick={() => setActiveTab(t.key)}
            >
              {t.label}
              {tabCounts[t.key] > 0 && (
                <span className={`admin-event-tab-count${t.key === 'pendentes' && tabCounts.pendentes > 0 ? ' pending' : ''}`}>
                  {tabCounts[t.key]}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* ── Events table ── */}
        {eventosFiltrados.length === 0 ? (
          <p className="section-empty" style={{ padding: '24px 0' }}>
            {activeTab === 'pendentes' ? 'Nenhum evento aguarda aprovação.' :
             activeTab === 'decorrer'  ? 'Nenhum evento a decorrer.' :
             activeTab === 'futuros'   ? 'Nenhum evento futuro aprovado.' :
                                         'Nenhum evento passado.'}
          </p>
        ) : (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Evento</th>
                  <th>Organizador</th>
                  <th style={{ whiteSpace: 'nowrap' }}>Datas</th>
                  <th>Orçamento</th>
                  <th>Estado</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {eventosFiltrados.map(ev => {
                  const busy = busyEvIds.has(ev.id);
                  return (
                    <tr key={ev.id}>
                      <td>
                        <span style={{ fontWeight: 600, color: 'var(--text-h)', fontSize: 14 }}>{ev.nome}</span>
                        {ev.localizacao && (
                          <span style={{ display: 'block', fontSize: 12, color: 'var(--text)', marginTop: 2 }}>
                            {ev.localizacao}
                          </span>
                        )}
                      </td>
                      <td>
                        <span style={{ fontSize: 13, color: 'var(--text-h)' }}>
                          {ev.organizador?.nome ?? '—'}
                        </span>
                      </td>
                      <td style={{ whiteSpace: 'nowrap', fontSize: 12, color: 'var(--text)' }}>
                        {fmtDate(ev.dataInicio)}<br />{fmtDate(ev.dataFim)}
                      </td>
                      <td style={{ fontSize: 13, color: 'var(--accent)', fontWeight: 600 }}>
                        {fmtCur(ev.orcamentoMaximo)}
                      </td>
                      <td>
                        <span className={`admin-event-estado estado-${(ev.estado ?? 'Pendente').toLowerCase()}`}>
                          {ev.estado ?? 'Pendente'}
                        </span>
                      </td>
                      <td>
                        <div className="admin-actions">
                          {activeTab === 'pendentes' && (
                            <>
                              <button
                                className="admin-btn-aprovar"
                                disabled={busy}
                                onClick={() => handleAprovar(ev)}
                                title="Aprovar evento"
                              >
                                Aprovar
                              </button>
                              <button
                                className="admin-btn-rejeitar"
                                disabled={busy}
                                onClick={() => handleRejeitar(ev)}
                                title="Rejeitar evento"
                              >
                                Rejeitar
                              </button>
                            </>
                          )}
                          <button
                            className="admin-btn-remove"
                            disabled={busy}
                            onClick={() => handleEliminarEvento(ev)}
                            title="Eliminar evento"
                          >
                            Eliminar
                          </button>
                        </div>
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
