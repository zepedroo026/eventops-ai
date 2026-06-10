import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';

const fmtDate = s => new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const fmtTime = s => new Date(s).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });

export default function StaffDashboard() {
  const navigate = useNavigate();
  const [staffEntries, setStaffEntries] = useState([]);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState('');

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  useEffect(() => {
    api.get('/staff/me')
      .then(setStaffEntries)
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token');
          navigate('/login');
        } else {
          setError('Não foi possível carregar os teus dados.');
        }
      })
      .finally(() => setLoading(false));
  }, []);

  /* flatten and sort all allocations chronologically */
  const tarefas = staffEntries
    .flatMap(s =>
      (s.alocacoes || []).map(aloc => ({
        eventoNome: aloc.atividade?.evento?.nome,
        eventoId:   aloc.atividade?.evento?.id,
        atividade:  aloc.atividade,
      }))
    )
    .filter(t => t.atividade)
    .sort((a, b) => new Date(a.atividade.horaInicio) - new Date(b.atividade.horaInicio));

  /* ── Loading ── */
  if (loading) return (
    <div className="dash-page">
      <div className="skeleton" style={{ width: 220, height: 28, marginBottom: 32 }} />
      <div className="metrics-grid" style={{ gridTemplateColumns: 'repeat(2, 1fr)' }}>
        {[1, 2].map(i => <div key={i} className="metric-card skeleton" style={{ height: 80 }} />)}
      </div>
    </div>
  );

  if (error) return (
    <div className="dash-state error"><p>{error}</p></div>
  );

  return (
    <div className="dash-page">
      {/* ── Cabeçalho ── */}
      <div className="dash-title-row">
        <h2>Olá, {user.nome || 'Staff'}</h2>
        <span className="badge">Staff</span>
      </div>

      {/* ── Métricas ── */}
      <div className="metrics-grid" style={{ gridTemplateColumns: 'repeat(2, 1fr)' }}>
        <div className="metric-card">
          <span className="metric-value metric-accent">{tarefas.length}</span>
          <span className="metric-label">Tarefas Atribuídas</span>
        </div>
        <div className="metric-card">
          <span className="metric-value">{new Set(tarefas.map(t => t.eventoId).filter(Boolean)).size}</span>
          <span className="metric-label">Evento{new Set(tarefas.map(t => t.eventoId).filter(Boolean)).size !== 1 ? 's' : ''}</span>
        </div>
      </div>

      {/* ── As Minhas Tarefas ── */}
      <div className="staff-dash-section">
        <div className="section-title-row">
          <h3>As Minhas Tarefas</h3>
          <span className="section-count">{tarefas.length}</span>
        </div>

        {tarefas.length === 0 ? (
          <p className="section-empty" style={{ padding: '16px 0' }}>
            Nenhuma tarefa atribuída ainda.
          </p>
        ) : (
          <div className="staff-task-list">
            {tarefas.map((t, i) => (
              <div key={i} className="staff-task-item">
                <div className="staff-task-time">
                  <span>{fmtTime(t.atividade.horaInicio)}</span>
                  <span className="ros-time-sep">↓</span>
                  <span>{fmtTime(t.atividade.horaFim)}</span>
                </div>
                <div className="ros-bar" />
                <div className="staff-task-body">
                  <span className="staff-task-name">{t.atividade.nome}</span>
                  {t.atividade.sala && (
                    <span className="staff-task-sala">{t.atividade.sala.nome}</span>
                  )}
                  {t.eventoNome && (
                    <span className="staff-task-evento">{t.eventoNome}</span>
                  )}
                </div>
                <div className="staff-task-date">{fmtDate(t.atividade.horaInicio)}</div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* ── Os Meus Horários ── */}
      <div className="staff-dash-section">
        <div className="section-title-row">
          <h3>Os Meus Horários</h3>
        </div>

        {tarefas.length === 0 ? (
          <p className="section-empty" style={{ padding: '16px 0' }}>
            Nenhum horário disponível.
          </p>
        ) : (
          <div className="timetable-wrap">
            <table className="timetable">
              <thead>
                <tr>
                  <th className="timetable-staff-col">Atividade</th>
                  <th style={{ whiteSpace: 'nowrap' }}>Data</th>
                  <th>Início</th>
                  <th>Fim</th>
                  <th>Sala</th>
                  <th>Evento</th>
                </tr>
              </thead>
              <tbody>
                {tarefas.map((t, i) => (
                  <tr key={i}>
                    <td className="timetable-staff-col">
                      <span className="timetable-staff-name">{t.atividade.nome}</span>
                    </td>
                    <td style={{ fontSize: 13, color: 'var(--text)', whiteSpace: 'nowrap' }}>
                      {fmtDate(t.atividade.horaInicio)}
                    </td>
                    <td><span className="timetable-th-time">{fmtTime(t.atividade.horaInicio)}</span></td>
                    <td><span className="timetable-th-time">{fmtTime(t.atividade.horaFim)}</span></td>
                    <td style={{ fontSize: 13, color: 'var(--accent)' }}>
                      {t.atividade.sala?.nome || '—'}
                    </td>
                    <td style={{ fontSize: 13, color: 'var(--text)' }}>
                      {t.eventoNome || '—'}
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
