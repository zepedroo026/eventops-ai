import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';

/* ── helpers ── */
function formatDate(s) {
  return new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
}
function formatTime(s) {
  return new Date(s).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
}
function formatCurrency(v) {
  return Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });
}
function toISO(datetimeLocal) {
  return new Date(datetimeLocal).toISOString();
}

/* ── inline form component ── */
function InlineForm({ onCancel, onSubmit, loading, error, children }) {
  const ref = useRef(null);
  useEffect(() => { ref.current?.querySelector('input,textarea,select')?.focus(); }, []);

  return (
    <form
      ref={ref}
      className="inline-form"
      onSubmit={onSubmit}
      noValidate
    >
      {children}
      {error && <p className="inline-form-error">{error}</p>}
      <div className="inline-form-actions">
        <button type="button" className="btn-secondary" onClick={onCancel}>Cancelar</button>
        <button type="submit" className="btn-primary-sm" disabled={loading}>
          {loading ? <span className="spinner" /> : 'Guardar'}
        </button>
      </div>
    </form>
  );
}

/* ── section header ── */
function SectionHeader({ title, count, onAdd, adding }) {
  return (
    <div className="section-title-row">
      <h3>{title}</h3>
      <span className="section-count">{count}</span>
      <button
        type="button"
        className={`btn-add${adding ? ' active' : ''}`}
        onClick={onAdd}
        aria-label={`Adicionar ${title}`}
      >
        {adding ? '✕' : '+ Adicionar'}
      </button>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════ */
export default function EventoDetalhe() {
  const { id } = useParams();
  const eventoId = Number(id);
  const navigate = useNavigate();

  const [evento,    setEvento]    = useState(null);
  const [loading,   setLoading]   = useState(true);
  const [pageError, setPageError] = useState('');

  /* listas locais — actualizadas sem re-fetch do evento inteiro */
  const [salas,      setSalas]      = useState([]);
  const [atividades, setAtividades] = useState([]);
  const [staff,      setStaff]      = useState([]);
  const [conflitos,  setConflitos]  = useState([]);
  const [conflitosLoading, setConflitosLoading] = useState(false);

  /* quais formulários estão abertos */
  const [addingSala,      setAddingSala]      = useState(false);
  const [addingAtividade, setAddingAtividade] = useState(false);
  const [addingStaff,     setAddingStaff]     = useState(false);

  /* ── carregar evento ── */
  useEffect(() => {
    api.get(`/eventos/${eventoId}`)
      .then(ev => {
        setEvento(ev);
        setSalas(ev.salas ?? []);
        setAtividades([...(ev.atividades ?? [])].sort(
          (a, b) => new Date(a.horaInicio) - new Date(b.horaInicio)
        ));
        setStaff(ev.staff ?? []);
      })
      .catch(err => {
        if (err.message.includes('401') || err.message.includes('403')) {
          localStorage.removeItem('token'); navigate('/login');
        } else {
          setPageError(err.message.includes('404') ? 'Evento não encontrado.' : 'Não foi possível carregar o evento.');
        }
      })
      .finally(() => setLoading(false));
  }, [eventoId]);

  /* ── carregar conflitos (chamado no mount e após alterar atividades) ── */
  function loadConflitos() {
    setConflitosLoading(true);
    api.get(`/atividades/conflitos?eventoId=${eventoId}`)
      .then(setConflitos)
      .catch(() => setConflitos([]))
      .finally(() => setConflitosLoading(false));
  }
  useEffect(() => { if (evento) loadConflitos(); }, [evento]);

  /* ─────────────────────────────────────────────────────────── */
  /*  FORMULÁRIOS                                                */
  /* ─────────────────────────────────────────────────────────── */

  /* Sala */
  function FormSala() {
    const [form, setForm] = useState({ nome: '', capacidade: '', localizacao: '' });
    const [err,  setErr]  = useState('');
    const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const sala = await api.post('/salas', {
          nome: form.nome,
          capacidade: parseInt(form.capacidade, 10) || 0,
          localizacao: form.localizacao || null,
          eventoId,
        });
        setSalas(prev => [...prev, sala]);
        setAddingSala(false);
      } catch (ex) { setErr(ex.message || 'Erro ao criar sala.'); }
      finally { setBusy(false); }
    }

    return (
      <InlineForm onCancel={() => setAddingSala(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field">
            <label>Nome *</label>
            <input type="text" value={form.nome} onChange={set('nome')} placeholder="Ex: Auditório A" required />
          </div>
          <div className="field">
            <label>Capacidade *</label>
            <input type="number" min="1" value={form.capacidade} onChange={set('capacidade')} placeholder="100" required />
          </div>
          <div className="field" style={{ gridColumn: '1 / -1' }}>
            <label>Localização</label>
            <input type="text" value={form.localizacao} onChange={set('localizacao')} placeholder="Ex: Piso 2, Ala Norte" />
          </div>
        </div>
      </InlineForm>
    );
  }

  /* Atividade */
  function FormAtividade() {
    const [form, setForm] = useState({ nome: '', horaInicio: '', horaFim: '', salaId: salas[0]?.id ?? '', descricao: '' });
    const [err,  setErr]  = useState('');
    const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

    if (salas.length === 0) return (
      <div className="inline-form">
        <p className="inline-form-error">Cria pelo menos uma sala antes de adicionar atividades.</p>
        <div className="inline-form-actions">
          <button type="button" className="btn-secondary" onClick={() => setAddingAtividade(false)}>Fechar</button>
        </div>
      </div>
    );

    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      if (new Date(form.horaInicio) >= new Date(form.horaFim)) {
        setErr('A hora de início deve ser anterior à hora de fim.'); setBusy(false); return;
      }
      try {
        const at = await api.post('/atividades', {
          nome: form.nome,
          descricao: form.descricao || null,
          horaInicio: toISO(form.horaInicio),
          horaFim: toISO(form.horaFim),
          salaId: Number(form.salaId),
          eventoId,
        });
        setAtividades(prev =>
          [...prev, at].sort((a, b) => new Date(a.horaInicio) - new Date(b.horaInicio))
        );
        setAddingAtividade(false);
        loadConflitos();
      } catch (ex) { setErr(ex.message || 'Erro ao criar atividade.'); }
      finally { setBusy(false); }
    }

    return (
      <InlineForm onCancel={() => setAddingAtividade(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field" style={{ gridColumn: '1 / -1' }}>
            <label>Nome *</label>
            <input type="text" value={form.nome} onChange={set('nome')} placeholder="Ex: Keynote de abertura" required />
          </div>
          <div className="field">
            <label>Hora de início *</label>
            <input type="datetime-local" value={form.horaInicio} onChange={set('horaInicio')} required />
          </div>
          <div className="field">
            <label>Hora de fim *</label>
            <input type="datetime-local" value={form.horaFim} onChange={set('horaFim')} required />
          </div>
          <div className="field" style={{ gridColumn: '1 / -1' }}>
            <label>Sala *</label>
            <select className="field-select" value={form.salaId} onChange={set('salaId')} required>
              {salas.map(s => <option key={s.id} value={s.id}>{s.nome}</option>)}
            </select>
          </div>
          <div className="field" style={{ gridColumn: '1 / -1' }}>
            <label>Descrição</label>
            <textarea className="field-textarea" rows={2} value={form.descricao} onChange={set('descricao')} placeholder="Opcional" />
          </div>
        </div>
      </InlineForm>
    );
  }

  /* Staff */
  function FormStaff() {
    const [form, setForm] = useState({ nome: '', funcao: '', contacto: '' });
    const [err,  setErr]  = useState('');
    const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const membro = await api.post('/staff', {
          nome: form.nome,
          funcao: form.funcao || null,
          contacto: form.contacto || null,
          eventoId,
        });
        setStaff(prev => [...prev, membro]);
        setAddingStaff(false);
      } catch (ex) { setErr(ex.message || 'Erro ao criar membro de staff.'); }
      finally { setBusy(false); }
    }

    return (
      <InlineForm onCancel={() => setAddingStaff(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field">
            <label>Nome *</label>
            <input type="text" value={form.nome} onChange={set('nome')} placeholder="Nome completo" required />
          </div>
          <div className="field">
            <label>Função</label>
            <input type="text" value={form.funcao} onChange={set('funcao')} placeholder="Ex: Técnico de Som" />
          </div>
          <div className="field" style={{ gridColumn: '1 / -1' }}>
            <label>Contacto</label>
            <input type="text" value={form.contacto} onChange={set('contacto')} placeholder="Email ou telefone" />
          </div>
        </div>
      </InlineForm>
    );
  }

  /* ─────────────────────────────────────────────────────────── */
  /*  RENDER                                                     */
  /* ─────────────────────────────────────────────────────────── */

  if (loading) return (
    <div className="dash-state"><span className="spinner large" /><p>A carregar evento…</p></div>
  );
  if (pageError) return (
    <div className="dash-state error">
      <p>{pageError}</p>
      <button className="btn-secondary" onClick={() => navigate('/dashboard')}>← Voltar</button>
    </div>
  );

  const salaMap = Object.fromEntries(salas.map(s => [s.id, s.nome]));

  return (
    <div className="detalhe-page">

      {/* ── Cabeçalho ── */}
      <div className="detalhe-header">
        <button className="btn-back" onClick={() => navigate('/dashboard')}>← Voltar</button>
        <div className="detalhe-title-row">
          <h2 className="detalhe-title">{evento.nome}</h2>
          {evento.localizacao && <span className="detalhe-location">{evento.localizacao}</span>}
        </div>
        <div className="detalhe-meta">
          <span className="detalhe-dates">{formatDate(evento.dataInicio)} → {formatDate(evento.dataFim)}</span>
          <span className="detalhe-budget">{formatCurrency(evento.orcamentoMaximo)}</span>
          {evento.descricao && <p className="detalhe-desc">{evento.descricao}</p>}
        </div>
      </div>

      <div className="detalhe-sections">

        {/* ── Salas ── */}
        <section className="detalhe-section">
          <SectionHeader title="Salas" count={salas.length}
            adding={addingSala} onAdd={() => setAddingSala(v => !v)} />
          {addingSala && <FormSala />}
          {salas.length === 0 && !addingSala
            ? <p className="section-empty">Nenhuma sala registada.</p>
            : (
              <div className="salas-grid">
                {salas.map(s => (
                  <div key={s.id} className="sala-card">
                    <span className="sala-name">{s.nome}</span>
                    <div className="sala-meta">
                      <span>{s.capacidade} lugares</span>
                      {s.localizacao && <span>{s.localizacao}</span>}
                    </div>
                  </div>
                ))}
              </div>
            )
          }
        </section>

        {/* ── Run of Show ── */}
        <section className="detalhe-section">
          <SectionHeader title="Run of Show" count={atividades.length}
            adding={addingAtividade} onAdd={() => setAddingAtividade(v => !v)} />
          {addingAtividade && <FormAtividade />}
          {atividades.length === 0 && !addingAtividade
            ? <p className="section-empty">Nenhuma atividade agendada.</p>
            : (
              <div className="ros-list">
                {atividades.map(a => (
                  <div key={a.id} className="ros-item">
                    <div className="ros-time">
                      <span>{formatTime(a.horaInicio)}</span>
                      <span className="ros-time-sep">↓</span>
                      <span>{formatTime(a.horaFim)}</span>
                    </div>
                    <div className="ros-bar" />
                    <div className="ros-body">
                      <span className="ros-name">{a.nome}</span>
                      {salaMap[a.salaId] && <span className="ros-sala">{salaMap[a.salaId]}</span>}
                      {a.descricao && <p className="ros-desc">{a.descricao}</p>}
                    </div>
                  </div>
                ))}
              </div>
            )
          }
        </section>

        {/* ── Staff ── */}
        <section className="detalhe-section">
          <SectionHeader title="Staff" count={staff.length}
            adding={addingStaff} onAdd={() => setAddingStaff(v => !v)} />
          {addingStaff && <FormStaff />}
          {staff.length === 0 && !addingStaff
            ? <p className="section-empty">Nenhum membro de staff registado.</p>
            : (
              <div className="staff-list">
                {staff.map(s => (
                  <div key={s.id} className="staff-item">
                    <div className="staff-avatar">{s.nome.charAt(0).toUpperCase()}</div>
                    <div className="staff-info">
                      <span className="staff-nome">{s.nome}</span>
                      {s.funcao && <span className="staff-funcao">{s.funcao}</span>}
                    </div>
                    {s.contacto && <span className="staff-contacto">{s.contacto}</span>}
                  </div>
                ))}
              </div>
            )
          }
        </section>

        {/* ── Conflitos ── */}
        <section className="detalhe-section">
          <div className="section-title-row">
            <h3>Conflitos</h3>
            {!conflitosLoading && (
              <span className={`section-count${conflitos.length > 0 ? ' danger' : ''}`}>
                {conflitos.length}
              </span>
            )}
            <button type="button" className="btn-add" onClick={loadConflitos}>
              ↻ Verificar
            </button>
          </div>

          {conflitosLoading && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '12px 0' }}>
              <span className="spinner large" style={{ width: 18, height: 18 }} />
              <span style={{ fontSize: 13, color: 'var(--text)' }}>A verificar conflitos…</span>
            </div>
          )}

          {!conflitosLoading && conflitos.length === 0 && (
            <p className="section-empty" style={{ color: 'var(--success)' }}>
              ✓ Sem conflitos detetados.
            </p>
          )}

          {!conflitosLoading && conflitos.length > 0 && (
            <div className="conflitos-list">
              {conflitos.map((c, i) => (
                <div key={i} className="conflito-item">
                  <span className="conflito-icon">⚠</span>
                  <div className="conflito-body">
                    <span className={`conflito-tipo ${c.tipo === 'SalaConflito' ? 'sala' : 'staff'}`}>
                      {c.tipo === 'SalaConflito' ? 'Sala' : 'Staff'}
                    </span>
                    <p className="conflito-desc">{c.descricao}</p>
                    <span className="conflito-ids">
                      Atividades #{c.atividadeAId} · #{c.atividadeBId}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

      </div>
    </div>
  );
}
