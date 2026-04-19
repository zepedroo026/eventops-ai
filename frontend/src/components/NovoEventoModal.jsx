import { useState } from 'react';
import { api } from '../api/client';

function getUserId() {
  const token = localStorage.getItem('token');
  if (!token) return null;
  try {
    const payload = token.split('.')[1];
    const decoded = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return parseInt(decoded.sub, 10);
  } catch {
    return null;
  }
}

export default function NovoEventoModal({ onClose, onCreated }) {
  const [form, setForm] = useState({
    nome: '', descricao: '', localizacao: '',
    dataInicio: '', dataFim: '', orcamentoMaximo: '',
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const set = field => e => setForm(f => ({ ...f, [field]: e.target.value }));

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');

    const organizadorId = getUserId();
    if (!organizadorId) { setError('Sessão inválida. Faz login novamente.'); return; }
    if (new Date(form.dataInicio) >= new Date(form.dataFim)) {
      setError('A data de início deve ser anterior à data de fim.');
      return;
    }

    setLoading(true);
    try {
      const evento = await api.post('/eventos', {
        nome: form.nome,
        descricao: form.descricao || null,
        localizacao: form.localizacao || null,
        dataInicio: new Date(form.dataInicio).toISOString(),
        dataFim: new Date(form.dataFim).toISOString(),
        orcamentoMaximo: parseFloat(form.orcamentoMaximo) || 0,
        organizadorId,
      });
      onCreated(evento);
    } catch (err) {
      setError(err.message || 'Erro ao criar evento.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <div className="modal" role="dialog" aria-modal="true">
        <div className="modal-header">
          <h3>Novo Evento</h3>
          <button className="modal-close" onClick={onClose} aria-label="Fechar">✕</button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form">
          <div className="field">
            <label>Nome *</label>
            <input type="text" value={form.nome} onChange={set('nome')}
              placeholder="Nome do evento" required autoFocus />
          </div>

          <div className="field">
            <label>Descrição</label>
            <textarea value={form.descricao} onChange={set('descricao')}
              placeholder="Descrição opcional" rows={3} className="field-textarea" />
          </div>

          <div className="field">
            <label>Localização</label>
            <input type="text" value={form.localizacao} onChange={set('localizacao')}
              placeholder="Local do evento" />
          </div>

          <div className="modal-row">
            <div className="field">
              <label>Início *</label>
              <input type="datetime-local" value={form.dataInicio} onChange={set('dataInicio')} required />
            </div>
            <div className="field">
              <label>Fim *</label>
              <input type="datetime-local" value={form.dataFim} onChange={set('dataFim')} required />
            </div>
          </div>

          <div className="field">
            <label>Orçamento máximo (€) *</label>
            <input type="number" min="0" step="0.01" value={form.orcamentoMaximo}
              onChange={set('orcamentoMaximo')} placeholder="0.00" required />
          </div>

          {error && <div className="login-error">{error}</div>}

          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn-primary-sm" disabled={loading}>
              {loading ? <span className="spinner" /> : 'Criar Evento'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
