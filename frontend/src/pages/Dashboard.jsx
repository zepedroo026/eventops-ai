import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';

function formatDate(str) {
  return new Date(str).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
}
function formatCurrency(value) {
  return Number(value).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });
}

export default function Dashboard() {
  const [eventos, setEventos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  function load() {
    setLoading(true);
    setError('');
    api.get('/eventos')
      .then(data => setEventos(data ?? []))
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token');
          navigate('/login');
        } else {
          setError('Não foi possível carregar os eventos.');
        }
      })
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  return (
    <div className="dash-page">
      <div className="dash-title-row">
        <h2>Eventos</h2>
        {!loading && !error && (
          <span className="dash-count">
            {eventos.length} evento{eventos.length !== 1 ? 's' : ''}
          </span>
        )}
      </div>

      {loading && (
        <div className="dash-state">
          <span className="spinner large" />
          <p>A carregar eventos…</p>
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
          <p className="dash-empty">Ainda não existem eventos. Cria o primeiro!</p>
        </div>
      )}

      {!loading && !error && eventos.length > 0 && (
        <div className="events-grid">
          {eventos.map(ev => (
            <article
              key={ev.id}
              className="event-card"
              onClick={() => navigate(`/eventos/${ev.id}`)}
              role="button"
              tabIndex={0}
              onKeyDown={e => e.key === 'Enter' && navigate(`/eventos/${ev.id}`)}
            >
              <div className="event-card-top">
                <h3 className="event-name">{ev.nome}</h3>
                {ev.localizacao && <span className="event-location">{ev.localizacao}</span>}
              </div>

              {ev.descricao && <p className="event-desc">{ev.descricao}</p>}

              <div className="event-dates">
                <span>{formatDate(ev.dataInicio)}</span>
                <span className="event-dates-sep">→</span>
                <span>{formatDate(ev.dataFim)}</span>
              </div>

              <div className="event-card-footer">
                <span className="event-budget">{formatCurrency(ev.orcamentoMaximo)}</span>
                {ev.organizador?.nome && (
                  <span className="event-organizer">{ev.organizador.nome}</span>
                )}
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
