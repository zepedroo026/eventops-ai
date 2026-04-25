import { useEffect, useState } from 'react';
import { api } from '../api/client';

export default function EscalaModal({ eventoId, onClose, onApplied }) {
  const [sugestoes,  setSugestoes]  = useState([]);
  const [selected,   setSelected]   = useState(new Set());
  const [loading,    setLoading]    = useState(true);
  const [applying,   setApplying]   = useState(false);
  const [error,      setError]      = useState('');
  const [resultado,  setResultado]  = useState(null);

  useEffect(() => {
    api.post(`/staff/gerar-escala?eventoId=${eventoId}`, {})
      .then(data => {
        setSugestoes(data);
        setSelected(new Set(data.map((_, i) => i)));
      })
      .catch(err => setError(err.message || 'Erro ao gerar escala.'))
      .finally(() => setLoading(false));
  }, [eventoId]);

  function toggleAll(e) {
    setSelected(e.target.checked ? new Set(sugestoes.map((_, i) => i)) : new Set());
  }

  function toggle(i) {
    setSelected(prev => {
      const next = new Set(prev);
      next.has(i) ? next.delete(i) : next.add(i);
      return next;
    });
  }

  async function aplicar() {
    const itens = [...selected].map(i => ({
      staffId:     sugestoes[i].staffId,
      atividadeId: sugestoes[i].atividadeId,
    }));
    if (itens.length === 0) return;
    setApplying(true); setError('');
    try {
      const res = await api.post('/staff/aplicar-escala', itens);
      setResultado(res);
      onApplied?.();
    } catch (err) {
      setError(err.message || 'Erro ao aplicar escala.');
    } finally {
      setApplying(false);
    }
  }

  const allChecked = sugestoes.length > 0 && selected.size === sugestoes.length;
  const someChecked = selected.size > 0 && selected.size < sugestoes.length;

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal escala-modal">

        <div className="modal-header">
          <div className="escala-modal-title">
            <span className="escala-ai-icon">⚡</span>
            <h3>Escala Automática</h3>
          </div>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>

        <div className="escala-modal-body">
          {loading && (
            <div className="escala-loading">
              <span className="spinner large" />
              <p>A gerar sugestões de alocação…</p>
            </div>
          )}

          {!loading && error && !resultado && (
            <p className="escala-error">{error}</p>
          )}

          {!loading && !error && sugestoes.length === 0 && (
            <div className="escala-empty">
              <p>Não foi possível gerar sugestões.</p>
              <p className="escala-empty-hint">Certifica-te de que o evento tem staff e atividades registados.</p>
            </div>
          )}

          {!loading && resultado && (
            <div className="escala-resultado">
              <span className="escala-resultado-icon">✓</span>
              <div>
                <p className="escala-resultado-title">Escala aplicada com sucesso</p>
                <p className="escala-resultado-detail">
                  {resultado.criadas} alocaç{resultado.criadas === 1 ? 'ão criada' : 'ões criadas'}
                  {resultado.duplicadas > 0 && `, ${resultado.duplicadas} já existiam`}.
                </p>
              </div>
            </div>
          )}

          {!loading && !resultado && sugestoes.length > 0 && (
            <div className="escala-table-wrap">
              <p className="escala-intro">
                {sugestoes.length} sugestão{sugestoes.length !== 1 ? 'ões' : ''} gerada{sugestoes.length !== 1 ? 's' : ''}.
                Desmarca as que não quiseres aplicar.
              </p>
              <table className="escala-table">
                <thead>
                  <tr>
                    <th>
                      <input
                        type="checkbox"
                        checked={allChecked}
                        ref={el => { if (el) el.indeterminate = someChecked; }}
                        onChange={toggleAll}
                      />
                    </th>
                    <th>Staff</th>
                    <th>Atividade</th>
                    <th>Motivo</th>
                  </tr>
                </thead>
                <tbody>
                  {sugestoes.map((s, i) => (
                    <tr
                      key={i}
                      className={selected.has(i) ? 'selected' : 'deselected'}
                      onClick={() => toggle(i)}
                    >
                      <td onClick={e => e.stopPropagation()}>
                        <input type="checkbox" checked={selected.has(i)} onChange={() => toggle(i)} />
                      </td>
                      <td>
                        <span className="escala-staff-nome">{s.staffNome}</span>
                        {s.staffFuncao && <span className="escala-staff-funcao">{s.staffFuncao}</span>}
                      </td>
                      <td className="escala-atividade">{s.atividadeNome}</td>
                      <td className="escala-motivo">{s.motivo}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="modal-footer escala-footer">
          {error && resultado === null && !loading && <p className="escala-error-inline">{error}</p>}

          {resultado ? (
            <button className="btn-primary" onClick={onClose}>Fechar</button>
          ) : (
            <>
              <button className="btn-secondary" onClick={onClose} disabled={applying}>Cancelar</button>
              {sugestoes.length > 0 && (
                <button
                  className="btn-ai"
                  onClick={aplicar}
                  disabled={applying || selected.size === 0}
                >
                  {applying
                    ? <><span className="spinner" /> A aplicar…</>
                    : `Aplicar ${selected.size} alocaç${selected.size === 1 ? 'ão' : 'ões'}`}
                </button>
              )}
            </>
          )}
        </div>

      </div>
    </div>
  );
}
