import { useState } from 'react';
import { api } from '../api/client';

export default function EditEventoModal({ evento, onClose, onSaved }) {
  const fmt = iso => iso ? iso.slice(0, 16) : '';

  const [form, setForm] = useState({
    nome:            evento.nome,
    descricao:       evento.descricao   ?? '',
    localizacao:     evento.localizacao ?? '',
    dataInicio:      fmt(evento.dataInicio),
    dataFim:         fmt(evento.dataFim),
    orcamentoMaximo: String(evento.orcamentoMaximo),
  });
  const [error,   setError]   = useState('');
  const [loading, setLoading] = useState(false);
  const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

  async function submit(e) {
    e.preventDefault();
    setError('');
    if (new Date(form.dataInicio) >= new Date(form.dataFim)) {
      setError('A data de início deve ser anterior à data de fim.');
      return;
    }
    setLoading(true);
    try {
      await api.put(`/eventos/${evento.id}`, {
        id:              evento.id,
        nome:            form.nome,
        descricao:       form.descricao    || null,
        localizacao:     form.localizacao  || null,
        dataInicio:      new Date(form.dataInicio).toISOString(),
        dataFim:         new Date(form.dataFim).toISOString(),
        orcamentoMaximo: parseFloat(form.orcamentoMaximo) || 0,
        organizadorId:   evento.organizadorId,
      });
      onSaved({
        ...evento,
        nome:            form.nome,
        descricao:       form.descricao    || null,
        localizacao:     form.localizacao  || null,
        dataInicio:      new Date(form.dataInicio).toISOString(),
        dataFim:         new Date(form.dataFim).toISOString(),
        orcamentoMaximo: parseFloat(form.orcamentoMaximo) || 0,
      });
    } catch (err) {
      setError(err.message || 'Erro ao guardar alterações.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <h3>Editar Evento</h3>
          <button className="modal-close" onClick={onClose} aria-label="Fechar">✕</button>
        </div>
        <form onSubmit={submit} className="modal-form" noValidate>
          <div className="field">
            <label>Nome *</label>
            <input type="text" value={form.nome} onChange={set('nome')} required autoFocus />
          </div>
          <div className="modal-row">
            <div className="field">
              <label>Data de início *</label>
              <input type="datetime-local" value={form.dataInicio} onChange={set('dataInicio')} required />
            </div>
            <div className="field">
              <label>Data de fim *</label>
              <input type="datetime-local" value={form.dataFim} onChange={set('dataFim')} required />
            </div>
          </div>
          <div className="field">
            <label>Localização</label>
            <input type="text" value={form.localizacao} onChange={set('localizacao')} placeholder="Ex: Lisboa" />
          </div>
          <div className="field">
            <label>Orçamento máximo (€) *</label>
            <input type="number" min="0" step="0.01" value={form.orcamentoMaximo} onChange={set('orcamentoMaximo')} required />
          </div>
          <div className="field">
            <label>Descrição</label>
            <textarea className="field-textarea" rows={2} value={form.descricao} onChange={set('descricao')} />
          </div>
          {error && <p className="inline-form-error">{error}</p>}
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>Cancelar</button>
            <button type="submit" className="btn-primary-sm" disabled={loading}>
              {loading ? <span className="spinner" /> : 'Guardar alterações'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
