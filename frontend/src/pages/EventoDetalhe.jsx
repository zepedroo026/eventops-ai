import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';

/* ── helpers ── */
const fmtDate     = s => new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const fmtTime     = s => new Date(s).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
const fmtCurrency = v => Number(v).toLocaleString('pt-PT', { style: 'currency', currency: 'EUR' });
const toISO       = s => new Date(s).toISOString();
const todayLocal  = () => new Date().toISOString().slice(0, 10);

/* ── InlineForm ── */
function InlineForm({ onCancel, onSubmit, loading, error, children }) {
  const ref = useRef(null);
  useEffect(() => { ref.current?.querySelector('input,textarea,select')?.focus(); }, []);
  return (
    <form ref={ref} className="inline-form" onSubmit={onSubmit} noValidate>
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

/* ── SectionHeader ── */
function SectionHeader({ title, count, countClass, onAdd, adding, extra }) {
  return (
    <div className="section-title-row">
      <h3>{title}</h3>
      {count !== undefined && (
        <span className={`section-count${countClass ? ' ' + countClass : ''}`}>{count}</span>
      )}
      {extra}
      {onAdd && (
        <button type="button" className={`btn-add${adding ? ' active' : ''}`} onClick={onAdd}>
          {adding ? '✕' : '+ Adicionar'}
        </button>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════ */
export default function EventoDetalhe() {
  const { id } = useParams();
  const eventoId = Number(id);
  const navigate = useNavigate();

  const [evento,    setEvento]    = useState(null);
  const [loading,   setLoading]   = useState(true);
  const [pageError, setPageError] = useState('');

  const [salas,      setSalas]      = useState([]);
  const [atividades, setAtividades] = useState([]);
  const [staff,      setStaff]      = useState([]);
  const [despesas,   setDespesas]   = useState([]);
  const [conflitos,  setConflitos]  = useState([]);
  const [resumo,     setResumo]     = useState(null);

  const [conflitosLoading, setConflitosLoading] = useState(false);
  const [resumoLoading,    setResumoLoading]    = useState(false);

  const [addingSala,      setAddingSala]      = useState(false);
  const [addingAtividade, setAddingAtividade] = useState(false);
  const [addingStaff,     setAddingStaff]     = useState(false);
  const [addingDespesa,   setAddingDespesa]   = useState(false);

  /* ── load evento ── */
  useEffect(() => {
    api.get(`/eventos/${eventoId}`)
      .then(ev => {
        setEvento(ev);
        setSalas(ev.salas ?? []);
        setAtividades([...(ev.atividades ?? [])].sort(
          (a, b) => new Date(a.horaInicio) - new Date(b.horaInicio)
        ));
        setStaff(ev.staff ?? []);
        setDespesas([...(ev.despesas ?? [])].sort(
          (a, b) => new Date(b.data) - new Date(a.data)
        ));
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

  /* ── load conflitos ── */
  function loadConflitos() {
    setConflitosLoading(true);
    api.get(`/atividades/conflitos?eventoId=${eventoId}`)
      .then(setConflitos).catch(() => setConflitos([]))
      .finally(() => setConflitosLoading(false));
  }
  useEffect(() => { if (evento) loadConflitos(); }, [evento]);

  /* ── load resumo ── */
  function loadResumo() {
    setResumoLoading(true);
    api.get(`/despesas/resumo?eventoId=${eventoId}`)
      .then(setResumo).catch(() => {})
      .finally(() => setResumoLoading(false));
  }
  useEffect(() => { if (evento) loadResumo(); }, [evento]);

  /* ══════════════════════════════════════════════════════════ */
  /*  FORMS                                                     */
  /* ══════════════════════════════════════════════════════════ */

  function FormSala() {
    const [form, setForm] = useState({ nome: '', capacidade: '', localizacao: '' });
    const [err, setBusy_err] = useState(''); const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));
    async function submit(e) {
      e.preventDefault(); setBusy_err(''); setBusy(true);
      try {
        const sala = await api.post('/salas', { nome: form.nome, capacidade: parseInt(form.capacidade) || 0, localizacao: form.localizacao || null, eventoId });
        setSalas(p => [...p, sala]); setAddingSala(false);
      } catch (ex) { setBusy_err(ex.message || 'Erro ao criar sala.'); }
      finally { setBusy(false); }
    }
    return (
      <InlineForm onCancel={() => setAddingSala(false)} onSubmit={submit} loading={busy} error={setBusy_err}>
        <div className="inline-form-grid">
          <div className="field"><label>Nome *</label><input type="text" value={form.nome} onChange={set('nome')} placeholder="Ex: Auditório A" required /></div>
          <div className="field"><label>Capacidade *</label><input type="number" min="1" value={form.capacidade} onChange={set('capacidade')} placeholder="100" required /></div>
          <div className="field" style={{ gridColumn: '1/-1' }}><label>Localização</label><input type="text" value={form.localizacao} onChange={set('localizacao')} placeholder="Ex: Piso 2, Ala Norte" /></div>
        </div>
      </InlineForm>
    );
  }

  function FormAtividade() {
    const [form, setForm] = useState({ nome: '', horaInicio: '', horaFim: '', salaId: salas[0]?.id ?? '', descricao: '' });
    const [err, setErr] = useState(''); const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));
    if (salas.length === 0) return (
      <div className="inline-form">
        <p className="inline-form-error">Cria pelo menos uma sala antes de adicionar atividades.</p>
        <div className="inline-form-actions"><button type="button" className="btn-secondary" onClick={() => setAddingAtividade(false)}>Fechar</button></div>
      </div>
    );
    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      if (new Date(form.horaInicio) >= new Date(form.horaFim)) { setErr('A hora de início deve ser anterior à hora de fim.'); setBusy(false); return; }
      try {
        const at = await api.post('/atividades', { nome: form.nome, descricao: form.descricao || null, horaInicio: toISO(form.horaInicio), horaFim: toISO(form.horaFim), salaId: Number(form.salaId), eventoId });
        setAtividades(p => [...p, at].sort((a, b) => new Date(a.horaInicio) - new Date(b.horaInicio)));
        setAddingAtividade(false); loadConflitos();
      } catch (ex) { setErr(ex.message || 'Erro ao criar atividade.'); }
      finally { setBusy(false); }
    }
    return (
      <InlineForm onCancel={() => setAddingAtividade(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field" style={{ gridColumn: '1/-1' }}><label>Nome *</label><input type="text" value={form.nome} onChange={set('nome')} placeholder="Ex: Keynote de abertura" required /></div>
          <div className="field"><label>Hora de início *</label><input type="datetime-local" value={form.horaInicio} onChange={set('horaInicio')} required /></div>
          <div className="field"><label>Hora de fim *</label><input type="datetime-local" value={form.horaFim} onChange={set('horaFim')} required /></div>
          <div className="field" style={{ gridColumn: '1/-1' }}><label>Sala *</label><select className="field-select" value={form.salaId} onChange={set('salaId')} required>{salas.map(s => <option key={s.id} value={s.id}>{s.nome}</option>)}</select></div>
          <div className="field" style={{ gridColumn: '1/-1' }}><label>Descrição</label><textarea className="field-textarea" rows={2} value={form.descricao} onChange={set('descricao')} placeholder="Opcional" /></div>
        </div>
      </InlineForm>
    );
  }

  function FormStaff() {
    const [form, setForm] = useState({ nome: '', funcao: '', contacto: '' });
    const [err, setErr] = useState(''); const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));
    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const m = await api.post('/staff', { nome: form.nome, funcao: form.funcao || null, contacto: form.contacto || null, eventoId });
        setStaff(p => [...p, m]); setAddingStaff(false);
      } catch (ex) { setErr(ex.message || 'Erro ao criar membro de staff.'); }
      finally { setBusy(false); }
    }
    return (
      <InlineForm onCancel={() => setAddingStaff(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field"><label>Nome *</label><input type="text" value={form.nome} onChange={set('nome')} placeholder="Nome completo" required /></div>
          <div className="field"><label>Função</label><input type="text" value={form.funcao} onChange={set('funcao')} placeholder="Ex: Técnico de Som" /></div>
          <div className="field" style={{ gridColumn: '1/-1' }}><label>Contacto</label><input type="text" value={form.contacto} onChange={set('contacto')} placeholder="Email ou telefone" /></div>
        </div>
      </InlineForm>
    );
  }

  function FormDespesa() {
    const [form, setForm] = useState({ descricao: '', valor: '', categoria: '', data: todayLocal() });
    const [err, setErr] = useState(''); const [busy, setBusy] = useState(false);
    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));
    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const d = await api.post('/despesas', {
          descricao: form.descricao,
          valor: parseFloat(form.valor),
          categoria: form.categoria || null,
          data: new Date(form.data).toISOString(),
          eventoId,
        });
        setDespesas(p => [d, ...p]);
        setAddingDespesa(false);
        loadResumo();
      } catch (ex) { setErr(ex.message || 'Erro ao registar despesa.'); }
      finally { setBusy(false); }
    }
    return (
      <InlineForm onCancel={() => setAddingDespesa(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="inline-form-grid">
          <div className="field" style={{ gridColumn: '1/-1' }}>
            <label>Descrição *</label>
            <input type="text" value={form.descricao} onChange={set('descricao')} placeholder="Ex: Aluguer de palco" required />
          </div>
          <div className="field">
            <label>Valor (€) *</label>
            <input type="number" min="0" step="0.01" value={form.valor} onChange={set('valor')} placeholder="0.00" required />
          </div>
          <div className="field">
            <label>Data *</label>
            <input type="date" value={form.data} onChange={set('data')} required />
          </div>
          <div className="field" style={{ gridColumn: '1/-1' }}>
            <label>Categoria</label>
            <input list="categorias-list" value={form.categoria} onChange={set('categoria')} placeholder="Ex: Venue, Catering…" className="field-input" />
            <datalist id="categorias-list">
              {['Venue', 'Catering', 'AV / Técnico', 'Marketing', 'Staff', 'Decoração', 'Transporte', 'Outro'].map(c => (
                <option key={c} value={c} />
              ))}
            </datalist>
          </div>
        </div>
      </InlineForm>
    );
  }

  /* ══════════════════════════════════════════════════════════ */
  /*  RENDER                                                    */
  /* ══════════════════════════════════════════════════════════ */

  if (loading) return <div className="dash-state"><span className="spinner large" /><p>A carregar evento…</p></div>;
  if (pageError) return (
    <div className="dash-state error">
      <p>{pageError}</p>
      <button className="btn-secondary" onClick={() => navigate('/dashboard')}>← Voltar</button>
    </div>
  );

  const salaMap = Object.fromEntries(salas.map(s => [s.id, s.nome]));

  /* budget bar helpers */
  const pct        = resumo ? Math.min(resumo.percentagemUtilizada, 100) : 0;
  const over        = resumo ? resumo.percentagemUtilizada > 100 : false;
  const barColor    = over ? 'var(--danger)' : resumo?.percentagemUtilizada >= 85 ? '#f59e0b' : 'var(--success)';

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
          <span className="detalhe-dates">{fmtDate(evento.dataInicio)} → {fmtDate(evento.dataFim)}</span>
          <span className="detalhe-budget">{fmtCurrency(evento.orcamentoMaximo)}</span>
          {evento.descricao && <p className="detalhe-desc">{evento.descricao}</p>}
        </div>
      </div>

      <div className="detalhe-sections">

        {/* ── Salas ── */}
        <section className="detalhe-section">
          <SectionHeader title="Salas" count={salas.length} adding={addingSala} onAdd={() => setAddingSala(v => !v)} />
          {addingSala && <FormSala />}
          {salas.length === 0 && !addingSala
            ? <p className="section-empty">Nenhuma sala registada.</p>
            : <div className="salas-grid">{salas.map(s => (
                <div key={s.id} className="sala-card">
                  <span className="sala-name">{s.nome}</span>
                  <div className="sala-meta"><span>{s.capacidade} lugares</span>{s.localizacao && <span>{s.localizacao}</span>}</div>
                </div>
              ))}</div>
          }
        </section>

        {/* ── Run of Show ── */}
        <section className="detalhe-section">
          <SectionHeader title="Run of Show" count={atividades.length} adding={addingAtividade} onAdd={() => setAddingAtividade(v => !v)} />
          {addingAtividade && <FormAtividade />}
          {atividades.length === 0 && !addingAtividade
            ? <p className="section-empty">Nenhuma atividade agendada.</p>
            : <div className="ros-list">{atividades.map(a => (
                <div key={a.id} className="ros-item">
                  <div className="ros-time"><span>{fmtTime(a.horaInicio)}</span><span className="ros-time-sep">↓</span><span>{fmtTime(a.horaFim)}</span></div>
                  <div className="ros-bar" />
                  <div className="ros-body">
                    <span className="ros-name">{a.nome}</span>
                    {salaMap[a.salaId] && <span className="ros-sala">{salaMap[a.salaId]}</span>}
                    {a.descricao && <p className="ros-desc">{a.descricao}</p>}
                  </div>
                </div>
              ))}</div>
          }
        </section>

        {/* ── Staff ── */}
        <section className="detalhe-section">
          <SectionHeader title="Staff" count={staff.length} adding={addingStaff} onAdd={() => setAddingStaff(v => !v)} />
          {addingStaff && <FormStaff />}
          {staff.length === 0 && !addingStaff
            ? <p className="section-empty">Nenhum membro de staff registado.</p>
            : <div className="staff-list">{staff.map(s => (
                <div key={s.id} className="staff-item">
                  <div className="staff-avatar">{s.nome.charAt(0).toUpperCase()}</div>
                  <div className="staff-info"><span className="staff-nome">{s.nome}</span>{s.funcao && <span className="staff-funcao">{s.funcao}</span>}</div>
                  {s.contacto && <span className="staff-contacto">{s.contacto}</span>}
                </div>
              ))}</div>
          }
        </section>

        {/* ── Custos ── */}
        <section className="detalhe-section">
          <SectionHeader
            title="Custos"
            count={despesas.length}
            adding={addingDespesa}
            onAdd={() => setAddingDespesa(v => !v)}
          />

          {addingDespesa && <FormDespesa />}

          {/* painel de orçamento */}
          {resumoLoading && <div style={{ padding: '8px 0', fontSize: 13, color: 'var(--text)' }}>A calcular…</div>}
          {resumo && !resumoLoading && (
            <div className="budget-panel">
              <div className="budget-numbers">
                <div className="budget-number">
                  <span className="budget-label">Total gasto</span>
                  <span className="budget-value" style={{ color: over ? 'var(--danger)' : 'var(--text-h)' }}>
                    {fmtCurrency(resumo.totalGasto)}
                  </span>
                </div>
                <div className="budget-sep">de</div>
                <div className="budget-number">
                  <span className="budget-label">Orçamento</span>
                  <span className="budget-value">{fmtCurrency(resumo.orcamentoMaximo)}</span>
                </div>
                <div className="budget-pct" style={{ color: barColor }}>
                  {resumo.percentagemUtilizada.toFixed(1)}%
                  {over && <span className="budget-over-badge">EXCEDIDO</span>}
                </div>
              </div>

              <div className="budget-bar-track">
                <div
                  className="budget-bar-fill"
                  style={{ width: `${pct}%`, background: barColor }}
                />
              </div>

              {resumo.porCategoria && resumo.porCategoria.length > 0 && (
                <div className="budget-cats">
                  {resumo.porCategoria.map(c => (
                    <div key={c.categoria} className="budget-cat-row">
                      <span className="budget-cat-name">{c.categoria}</span>
                      <span className="budget-cat-qty">{c.quantidade}×</span>
                      <span className="budget-cat-total">{fmtCurrency(c.total)}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* lista de despesas */}
          {despesas.length === 0 && !addingDespesa && !resumoLoading && (
            <p className="section-empty" style={{ marginTop: resumo ? 12 : 0 }}>Nenhuma despesa registada.</p>
          )}
          {despesas.length > 0 && (
            <div className="despesas-list">
              {despesas.map(d => (
                <div key={d.id} className="despesa-item">
                  <div className="despesa-info">
                    <span className="despesa-desc">{d.descricao}</span>
                    {d.categoria && <span className="despesa-cat">{d.categoria}</span>}
                  </div>
                  <div className="despesa-right">
                    <span className="despesa-valor">{fmtCurrency(d.valor)}</span>
                    <span className="despesa-data">{fmtDate(d.data)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* ── Conflitos ── */}
        <section className="detalhe-section">
          <div className="section-title-row">
            <h3>Conflitos</h3>
            {!conflitosLoading && (
              <span className={`section-count${conflitos.length > 0 ? ' danger' : ''}`}>{conflitos.length}</span>
            )}
            <button type="button" className="btn-add" onClick={loadConflitos}>↻ Verificar</button>
          </div>
          {conflitosLoading && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '12px 0' }}>
              <span className="spinner" style={{ borderColor: 'var(--border)', borderTopColor: 'var(--accent)' }} />
              <span style={{ fontSize: 13, color: 'var(--text)' }}>A verificar conflitos…</span>
            </div>
          )}
          {!conflitosLoading && conflitos.length === 0 && (
            <p className="section-empty" style={{ color: 'var(--success)' }}>✓ Sem conflitos detetados.</p>
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
                    <span className="conflito-ids">Atividades #{c.atividadeAId} · #{c.atividadeBId}</span>
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
