import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';

const fmtDate = str => new Date(str).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const fmtCur  = v   => Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });

function getStatus(ev) {
  const now   = Date.now();
  const start = new Date(ev.dataInicio).getTime();
  const end   = new Date(ev.dataFim).getTime();
  if (now < start) return 'proximo';
  if (now > end)   return 'terminado';
  return 'decorrer';
}
const statusLabel = { proximo: 'Próximo', decorrer: 'A Decorrer', terminado: 'Terminado' };

export default function Dashboard() {
  const [eventos,        setEventos]        = useState([]);
  const [loading,        setLoading]        = useState(true);
  const [error,          setError]          = useState('');
  const [conflictCounts, setConflictCounts] = useState({});
  const [deletingEvIds,  setDeletingEvIds]  = useState(new Set());
  const [totalStaff,     setTotalStaff]     = useState(null);
  const [totalDespesas,  setTotalDespesas]  = useState(null);
  const [busca,          setBusca]          = useState('');
  const [filtroStatus,   setFiltroStatus]   = useState('todos');
  const navigate = useNavigate();

  function load() {
    setLoading(true);
    setError('');
    api.get('/eventos')
      .then(data => {
        const evs = data ?? [];
        setEventos(evs);
        evs.forEach(ev => {
          api.get(`/atividades/conflitos?eventoId=${ev.id}`)
            .then(cs => { if (cs.length > 0) setConflictCounts(p => ({ ...p, [ev.id]: cs.length })); })
            .catch(() => {});
        });
      })
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token'); navigate('/login');
        } else {
          setError('Não foi possível carregar os eventos.');
        }
      })
      .finally(() => setLoading(false));

    api.get('/staff').then(s => setTotalStaff((s ?? []).length)).catch(() => {});
    api.get('/despesas').then(d => setTotalDespesas((d ?? []).reduce((acc, x) => acc + (x.valor ?? 0), 0))).catch(() => {});
  }

  useEffect(load, []);

  /* ── delete event ── */
  async function handleDeleteEvento(e, ev) {
    e.stopPropagation();
    if (!window.confirm(`Eliminar o evento "${ev.nome}"?\nEsta ação não pode ser desfeita.`)) return;
    setDeletingEvIds(p => new Set([...p, ev.id]));
    try {
      await api.delete(`/eventos/${ev.id}`);
      setEventos(p => p.filter(x => x.id !== ev.id));
    } catch {
      setError('Não foi possível eliminar o evento.');
    } finally {
      setDeletingEvIds(p => { const n = new Set(p); n.delete(ev.id); return n; });
    }
  }

  /* metrics */
  const now      = new Date();
  const thisMes  = eventos.filter(ev => {
    const d = new Date(ev.dataInicio);
    return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
  });
  const proximos = eventos.filter(ev => getStatus(ev) === 'proximo');

  /* client-side search + filter */
  const eventosFiltrados = eventos.filter(ev => {
    const matchBusca  = (ev.nome ?? '').toLowerCase().includes(busca.toLowerCase());
    const matchStatus = filtroStatus === 'todos' || getStatus(ev) === filtroStatus;
    return matchBusca && matchStatus;
  });

  const FILTROS = [
    { key: 'todos',     label: 'Todos' },
    { key: 'proximo',   label: 'Próximo' },
    { key: 'decorrer',  label: 'A Decorrer' },
    { key: 'terminado', label: 'Terminado' },
  ];

  return (
    <div className="dash-page">
      <div className="dash-title-row">
        <h2>Eventos</h2>
        {!loading && !error && (
          <span className="dash-count">
            {eventosFiltrados.length !== eventos.length
              ? `${eventosFiltrados.length} de ${eventos.length}`
              : `${eventos.length} evento${eventos.length !== 1 ? 's' : ''}`}
          </span>
        )}
      </div>

      {/* ── Metrics ── */}
      {!loading && !error && (
        <div className="metrics-grid metrics-grid-5">
          <div className="metric-card">
            <span className="metric-value">{eventos.length}</span>
            <span className="metric-label">Total de Eventos</span>
          </div>
          <div className="metric-card">
            <span className="metric-value">{thisMes.length}</span>
            <span className="metric-label">Eventos Este Mês</span>
          </div>
          <div className="metric-card">
            <span className="metric-value metric-accent">{proximos.length}</span>
            <span className="metric-label">Próximos</span>
          </div>
          <div className="metric-card">
            <span className="metric-value">
              {totalStaff === null
                ? <span className="skeleton" style={{ display: 'inline-block', width: 40, height: 28, borderRadius: 4 }} />
                : totalStaff}
            </span>
            <span className="metric-label">Total de Staff</span>
          </div>
          <div className="metric-card">
            <span className="metric-value metric-accent" style={{ fontSize: 20 }}>
              {totalDespesas === null
                ? <span className="skeleton" style={{ display: 'inline-block', width: 80, height: 28, borderRadius: 4 }} />
                : fmtCur(totalDespesas)}
            </span>
            <span className="metric-label">Total de Despesas</span>
          </div>
        </div>
      )}

      {loading && (
        <div className="metrics-grid metrics-grid-5">
          {[1,2,3,4,5].map(i => <div key={i} className="metric-card skeleton" style={{ height: 80 }} />)}
        </div>
      )}

      {/* ── Search + filters ── */}
      {!loading && !error && eventos.length > 0 && (
        <div className="dash-filters">
          <div className="dash-search-wrap">
            <svg className="dash-search-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
            </svg>
            <input
              className="dash-search"
              type="search"
              placeholder="Pesquisar evento…"
              value={busca}
              onChange={e => setBusca(e.target.value)}
            />
            {busca && (
              <button className="dash-search-clear" onClick={() => setBusca('')} aria-label="Limpar">✕</button>
            )}
          </div>
          <div className="dash-filter-btns">
            {FILTROS.map(f => (
              <button
                key={f.key}
                className={`filter-btn${filtroStatus === f.key ? ' active' : ''}`}
                onClick={() => setFiltroStatus(f.key)}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>
      )}

      {loading && (
        <div className="events-grid" style={{ marginTop: 24 }}>
          {[1,2,3].map(i => <div key={i} className="skeleton" style={{ height: 180, borderRadius: 12 }} />)}
        </div>
      )}

      {!loading && error && (
        <div className="dash-state error">
          <p>{error}</p>
          <button className="btn-secondary" onClick={load}>Tentar novamente</button>
        </div>
      )}

      {!loading && !error && eventos.length === 0 && (
        <div className="dash-state">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.2" style={{ color: 'var(--border)' }}>
            <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          <p style={{ fontWeight: 600, color: 'var(--text-h)' }}>Nenhum evento ainda</p>
          <p style={{ fontSize: 14 }}>Cria o teu primeiro evento para começar</p>
        </div>
      )}

      {!loading && !error && eventos.length > 0 && eventosFiltrados.length === 0 && (
        <div className="dash-state">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.2" style={{ color: 'var(--border)' }}>
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          <p style={{ fontWeight: 600, color: 'var(--text-h)' }}>Nenhum resultado</p>
          <p style={{ fontSize: 14 }}>
            {busca ? `Nenhum evento encontrado para "${busca}"` : 'Nenhum evento com este estado'}
          </p>
          <button className="btn-secondary" onClick={() => { setBusca(''); setFiltroStatus('todos'); }}>
            Limpar filtros
          </button>
        </div>
      )}

      {!loading && !error && eventosFiltrados.length > 0 && (
        <div className="events-grid">
          {eventosFiltrados.map(ev => {
            const status    = getStatus(ev);
            const nConflits = conflictCounts[ev.id] ?? 0;
            return (
              <article
                key={ev.id}
                className="event-card"
                onClick={() => navigate(`/eventos/${ev.id}`)}
                role="button"
                tabIndex={0}
                onKeyDown={e => e.key === 'Enter' && navigate(`/eventos/${ev.id}`)}
              >
                <div className="event-card-top">
                  <div className="event-card-title-row">
                    <h3 className="event-name">{ev.nome}</h3>
                    <span className={`event-status status-${status}`}>{statusLabel[status]}</span>
                    <button
                      className="btn-delete"
                      disabled={deletingEvIds.has(ev.id)}
                      title="Eliminar evento"
                      onClick={e => handleDeleteEvento(e, ev)}
                    >✕</button>
                  </div>
                  {ev.localizacao && <span className="event-location">{ev.localizacao}</span>}
                </div>

                {ev.descricao && <p className="event-desc">{ev.descricao}</p>}

                <div className="event-dates">
                  <span>{fmtDate(ev.dataInicio)}</span>
                  <span className="event-dates-sep">→</span>
                  <span>{fmtDate(ev.dataFim)}</span>
                </div>

                <div className="event-card-footer">
                  <span className="event-budget">{fmtCur(ev.orcamentoMaximo)}</span>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    {nConflits > 0 && (
                      <span className="event-conflict-badge" title={`${nConflits} conflito(s)`}>
                        ⚠ {nConflits}
                      </span>
                    )}
                    {ev.organizador?.nome && (
                      <span className="event-organizer">{ev.organizador.nome}</span>
                    )}
                  </div>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}
