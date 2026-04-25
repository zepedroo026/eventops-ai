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
  }

  useEffect(load, []);

  /* metrics */
  const now      = new Date();
  const thisMes  = eventos.filter(ev => {
    const d = new Date(ev.dataInicio);
    return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
  });
  const proximos  = eventos.filter(ev => getStatus(ev) === 'proximo');

  return (
    <div className="dash-page">
      <div className="dash-title-row">
        <h2>Eventos</h2>
        {!loading && !error && (
          <span className="dash-count">{eventos.length} evento{eventos.length !== 1 ? 's' : ''}</span>
        )}
      </div>

      {/* ── Metrics ── */}
      {!loading && !error && (
        <div className="metrics-grid">
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
        </div>
      )}

      {loading && (
        <div className="metrics-grid">
          {[1,2,3].map(i => <div key={i} className="metric-card skeleton" style={{ height: 80 }} />)}
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

      {!loading && !error && eventos.length > 0 && (
        <div className="events-grid">
          {eventos.map(ev => {
            const status   = getStatus(ev);
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
