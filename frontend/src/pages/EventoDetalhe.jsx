import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import EditEventoModal from '../components/EditEventoModal';
import EscalaModal from '../components/EscalaModal';
import { useToast } from '../components/Toast';

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
  const [tarefas,    setTarefas]    = useState([]);
  const [notasLocal, setNotasLocal] = useState('');
  const [notasSaving, setNotasSaving] = useState(false);
  const [notasSaved,  setNotasSaved]  = useState(false);

  const [alocacoes,        setAlocacoes]        = useState([]);

  const [conflitosLoading,    setConflitosLoading]    = useState(false);
  const [conflitosVerificados, setConflitosVerificados] = useState(false);
  const [resumoLoading,        setResumoLoading]        = useState(false);

  const [addingSala,      setAddingSala]      = useState(false);
  const [addingAtividade, setAddingAtividade] = useState(false);
  const [addingStaff,     setAddingStaff]     = useState(false);
  const [addingDespesa,   setAddingDespesa]   = useState(false);
  const [addingTarefa,    setAddingTarefa]    = useState(false);
  const [escalaOpen,      setEscalaOpen]      = useState(false);
  const [editOpen,        setEditOpen]        = useState(false);
  const [deletingIds,     setDeletingIds]     = useState(new Set());
  const [togglingTarefas, setTogglingTarefas] = useState(new Set());
  const notasDebounce = useRef(null);

  const toast = useToast();

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
        setTarefas([...(ev.tarefas ?? [])].sort((a, b) => a.id - b.id));
        setNotasLocal(ev.notas ?? '');
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

  /* ── load alocacoes ── */
  function loadAlocacoes() {
    api.get(`/alocacoes?eventoId=${eventoId}`)
      .then(setAlocacoes).catch(() => {});
  }
  useEffect(() => { if (evento) loadAlocacoes(); }, [evento]);

  /* ── load conflitos ── */
  function loadConflitos() {
    setConflitosLoading(true);
    api.get(`/atividades/conflitos?eventoId=${eventoId}`)
      .then(cs => { setConflitos(cs); setConflitosVerificados(true); })
      .catch(() => { setConflitos([]); setConflitosVerificados(true); })
      .finally(() => setConflitosLoading(false));
  }
  useEffect(() => { if (evento) loadConflitos(); }, [evento]);

  /* ── delete helper ── */
  async function confirmDelete(resource, id, label, onSuccess) {
    if (!window.confirm(`Remover "${label}"? Esta ação não pode ser desfeita.`)) return;
    const key = `${resource}-${id}`;
    setDeletingIds(prev => new Set([...prev, key]));
    try {
      await api.delete(`/${resource}/${id}`);
      toast(`"${label}" removido com sucesso.`);
      onSuccess();
    } catch (err) {
      toast(err.message || 'Erro ao remover.', 'error');
    } finally {
      setDeletingIds(prev => { const n = new Set(prev); n.delete(key); return n; });
    }
  }

  /* ── PDF export ── */
  async function exportarPDF() {
    const { default: html2pdf } = await import('html2pdf.js');
    const rows = atividades.map(a => {
      const staffNomes = alocacoes
        .filter(al => al.atividadeId === a.id)
        .map(al => staff.find(s => s.id === al.staffId)?.nome)
        .filter(Boolean)
        .join(', ');
      return `
        <tr>
          <td style="padding:8px 10px;border:1px solid #ddd;white-space:nowrap">
            ${fmtTime(a.horaInicio)} – ${fmtTime(a.horaFim)}
          </td>
          <td style="padding:8px 10px;border:1px solid #ddd;font-weight:600">${a.nome}</td>
          <td style="padding:8px 10px;border:1px solid #ddd;color:#7c3aed">${salaMap[a.salaId] || '—'}</td>
          <td style="padding:8px 10px;border:1px solid #ddd;color:#555">${staffNomes || '—'}</td>
        </tr>`;
    }).join('');
    const html = `
      <div style="font-family:Arial,sans-serif;color:#111;padding:24px">
        <h1 style="font-size:22px;margin:0 0 4px">${evento.nome}</h1>
        ${evento.localizacao ? `<p style="color:#666;font-size:13px;margin:0 0 2px">${evento.localizacao}</p>` : ''}
        <p style="color:#888;font-size:12px;margin:0 0 20px">
          ${fmtDate(evento.dataInicio)} → ${fmtDate(evento.dataFim)}
        </p>
        <h2 style="font-size:15px;border-bottom:2px solid #aa3bff;padding-bottom:6px;margin:0 0 14px">
          Run of Show
        </h2>
        <table style="width:100%;border-collapse:collapse;font-size:13px">
          <thead>
            <tr style="background:#f5f3ff">
              <th style="text-align:left;padding:8px 10px;border:1px solid #ddd">Hora</th>
              <th style="text-align:left;padding:8px 10px;border:1px solid #ddd">Atividade</th>
              <th style="text-align:left;padding:8px 10px;border:1px solid #ddd">Sala</th>
              <th style="text-align:left;padding:8px 10px;border:1px solid #ddd">Staff</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>`;
    html2pdf().set({
      margin: 10,
      filename: `${evento.nome} - Run of Show.pdf`,
      html2canvas: { scale: 2 },
      jsPDF: { unit: 'mm', format: 'a4', orientation: 'landscape' },
    }).from(html).save();
  }

  /* ── notas debounce ── */
  const handleNotasChange = useCallback((val) => {
    setNotasLocal(val);
    setNotasSaved(false);
    clearTimeout(notasDebounce.current);
    notasDebounce.current = setTimeout(async () => {
      setNotasSaving(true);
      try {
        await api.patch(`/eventos/${eventoId}/notas`, { notas: val || null });
        setNotasSaved(true);
      } catch { /* silent */ }
      finally { setNotasSaving(false); }
    }, 1000);
  }, [eventoId]);

  /* ── toggle tarefa ── */
  async function handleToggleTarefa(t) {
    setTogglingTarefas(p => new Set([...p, t.id]));
    try {
      const res = await api.put(`/tarefas/${t.id}/toggle`, {});
      setTarefas(p => p.map(x => x.id === t.id ? { ...x, concluida: res.concluida } : x));
    } catch (err) {
      toast(err.message || 'Erro ao atualizar tarefa.', 'error');
    } finally {
      setTogglingTarefas(p => { const n = new Set(p); n.delete(t.id); return n; });
    }
  }

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
        toast('Sala criada com sucesso.');
      } catch (ex) { setBusy_err(ex.message || 'Erro ao criar sala.'); toast(ex.message || 'Erro ao criar sala.', 'error'); }
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
        toast('Atividade criada com sucesso.');
      } catch (ex) { setErr(ex.message || 'Erro ao criar atividade.'); toast(ex.message || 'Erro ao criar atividade.', 'error'); }
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
    const [form,          setForm]          = useState({ nome: '', funcao: '', contacto: '' });
    const [err,           setErr]           = useState('');
    const [busy,          setBusy]          = useState(false);
    const [staffUsers,    setStaffUsers]    = useState([]);
    const [loadingUsers,  setLoadingUsers]  = useState(true);
    const [selectedId,    setSelectedId]    = useState('');
    const [modoManual,    setModoManual]    = useState(false);

    const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

    useEffect(() => {
      api.get('/utilizadores/staff')
        .then(setStaffUsers)
        .catch(() => {})
        .finally(() => setLoadingUsers(false));
    }, []);

    function handleSelectUser(e) {
      const val = e.target.value;
      setSelectedId(val);
      if (!val) { setForm({ nome: '', funcao: '', contacto: '' }); return; }
      const u = staffUsers.find(u => u.id === Number(val));
      if (u) setForm({ nome: u.nome, funcao: 'Staff', contacto: u.email });
    }

    function toggleModo() {
      setModoManual(v => !v);
      setSelectedId('');
      setForm({ nome: '', funcao: '', contacto: '' });
    }

    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const m = await api.post('/staff', { nome: form.nome, funcao: form.funcao || null, contacto: form.contacto || null, eventoId });
        setStaff(p => [...p, m]); setAddingStaff(false);
        toast('Membro de staff criado com sucesso.');
      } catch (ex) { setErr(ex.message || 'Erro ao criar membro de staff.'); toast(ex.message || 'Erro ao criar membro de staff.', 'error'); }
      finally { setBusy(false); }
    }

    const showDropdown = !modoManual && staffUsers.length > 0;

    return (
      <InlineForm onCancel={() => setAddingStaff(false)} onSubmit={submit} loading={busy} error={err}>
        {loadingUsers ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--text)' }}>
            <span className="spinner" style={{ borderColor: 'var(--border)', borderTopColor: 'var(--accent)' }} />
            A carregar utilizadores…
          </div>
        ) : (
          <>
            {showDropdown && (
              <div className="field" style={{ gridColumn: '1/-1' }}>
                <label>Utilizador registado</label>
                <select className="field-select" value={selectedId} onChange={handleSelectUser}>
                  <option value="">— Selecionar da lista —</option>
                  {staffUsers.map(u => (
                    <option key={u.id} value={u.id}>{u.nome} · {u.email}</option>
                  ))}
                </select>
              </div>
            )}

            <div className="inline-form-grid">
              <div className="field">
                <label>Nome *</label>
                <input type="text" value={form.nome} onChange={set('nome')} placeholder="Nome completo" required />
              </div>
              <div className="field">
                <label>Função</label>
                <input type="text" value={form.funcao} onChange={set('funcao')} placeholder="Ex: Técnico de Som" />
              </div>
              <div className="field" style={{ gridColumn: '1/-1' }}>
                <label>Contacto</label>
                <input type="text" value={form.contacto} onChange={set('contacto')} placeholder="Email ou telefone" />
              </div>
            </div>

            {staffUsers.length > 0 && (
              <button type="button" className="staff-mode-toggle" onClick={toggleModo}>
                {modoManual ? '← Selecionar da lista de utilizadores' : '+ Adicionar manualmente sem conta'}
              </button>
            )}
          </>
        )}
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
        toast('Despesa registada com sucesso.');
      } catch (ex) { setErr(ex.message || 'Erro ao registar despesa.'); toast(ex.message || 'Erro ao registar despesa.', 'error'); }
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

  function FormTarefa() {
    const [descricao, setDescricao] = useState('');
    const [err, setErr] = useState(''); const [busy, setBusy] = useState(false);
    async function submit(e) {
      e.preventDefault(); setErr(''); setBusy(true);
      try {
        const t = await api.post('/tarefas', { descricao, eventoId });
        setTarefas(p => [...p, t]); setAddingTarefa(false);
        toast('Tarefa adicionada.');
      } catch (ex) { setErr(ex.message || 'Erro ao criar tarefa.'); }
      finally { setBusy(false); }
    }
    return (
      <InlineForm onCancel={() => setAddingTarefa(false)} onSubmit={submit} loading={busy} error={err}>
        <div className="field">
          <label>Descrição *</label>
          <input type="text" value={descricao} onChange={e => setDescricao(e.target.value)} placeholder="Ex: Confirmar som" required />
        </div>
      </InlineForm>
    );
  }

  /* ── Timetable ── */
  function Timetable() {
    if (staff.length === 0 || atividades.length === 0) {
      return (
        <EmptyState
          icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M3 15h18M9 3v18M15 3v18"/></svg>}
          title="Timetable indisponível"
          hint="Adiciona staff e atividades para visualizar a grelha de alocações"
        />
      );
    }

    const allocSet = new Set(alocacoes.map(a => `${a.staffId}-${a.atividadeId}`));

    return (
      <div className="timetable-wrap">
        <table className="timetable">
          <thead>
            <tr>
              <th className="timetable-staff-col">Staff</th>
              {atividades.map(a => (
                <th key={a.id} className="timetable-time-col">
                  <div className="timetable-th-time">{fmtTime(a.horaInicio)}–{fmtTime(a.horaFim)}</div>
                  <div className="timetable-th-name">{a.nome}</div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {staff.map(s => (
              <tr key={s.id}>
                <td className="timetable-staff-col">
                  <span className="timetable-staff-name">{s.nome}</span>
                  {s.funcao && <span className="timetable-staff-funcao">{s.funcao}</span>}
                </td>
                {atividades.map(a => {
                  const alloc = allocSet.has(`${s.id}-${a.id}`);
                  return (
                    <td key={a.id} className={alloc ? 'timetable-cell-alloc' : 'timetable-cell-empty'}>
                      {alloc ? a.nome : null}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  /* ══════════════════════════════════════════════════════════ */
  /*  RENDER                                                    */
  /* ══════════════════════════════════════════════════════════ */

  function EmptyState({ icon, title, hint, onAction, actionLabel }) {
    return (
      <div className="section-empty-rich">
        <div className="section-empty-icon">{icon}</div>
        <p className="section-empty-title">{title}</p>
        {hint && <p className="section-empty-hint">{hint}</p>}
        {onAction && (
          <button className="btn-primary-sm" onClick={onAction}>{actionLabel}</button>
        )}
      </div>
    );
  }

  if (loading) return (
    <div className="detalhe-page">
      <div className="detalhe-header">
        <div className="skeleton" style={{ width: 80, height: 32, borderRadius: 8 }} />
        <div className="skeleton" style={{ width: 260, height: 26, marginTop: 16 }} />
        <div className="skeleton" style={{ width: 160, height: 14, marginTop: 8 }} />
        <div className="skeleton" style={{ width: 120, height: 13, marginTop: 6 }} />
      </div>
      <div className="detalhe-sections">
        {[['Salas', 80], ['Run of Show', 140], ['Staff', 100], ['Custos', 100]].map(([t, h]) => (
          <section key={t} className="detalhe-section">
            <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
              <div className="skeleton" style={{ width: 90, height: 20 }} />
              <div className="skeleton" style={{ width: 28, height: 20 }} />
            </div>
            <div className="skeleton" style={{ height: h }} />
          </section>
        ))}
      </div>
    </div>
  );

  if (pageError) return (
    <div className="dash-state error">
      <p>{pageError}</p>
      <button className="btn-secondary" onClick={() => navigate('/dashboard')}>← Voltar</button>
    </div>
  );

  const salaMap = Object.fromEntries(salas.map(s => [s.id, s.nome]));

  /* ── progresso do evento ── */
  const progCriteria = {
    sala:        salas.length > 0,
    atividade:   atividades.length > 0,
    staffAlocado: alocacoes.length > 0,
    semConflitos: conflitosVerificados && conflitos.length === 0,
    orcamento:   (evento.orcamentoMaximo ?? 0) > 0,
  };
  const progPts  = Object.values(progCriteria).filter(Boolean).length * 20;
  const progLabel = progPts === 100 ? 'Completo' : progPts >= 60 ? 'Em progresso' : 'Incompleto';
  const progColor = progPts === 100 ? 'var(--success)' : progPts >= 60 ? 'var(--accent)' : 'var(--danger)';

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
          <button className="btn-edit" onClick={() => setEditOpen(true)}>
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
              <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
            </svg>
            Editar
          </button>
        </div>
        <div className="detalhe-meta">
          <span className="detalhe-dates">{fmtDate(evento.dataInicio)} → {fmtDate(evento.dataFim)}</span>
          <span className="detalhe-budget">{fmtCurrency(evento.orcamentoMaximo)}</span>
          {evento.descricao && <p className="detalhe-desc">{evento.descricao}</p>}
        </div>

        {/* ── progress bar ── */}
        <div className="ev-progress">
          <div className="ev-progress-header">
            <span className="ev-progress-label" style={{ color: progColor }}>{progLabel}</span>
            <span className="ev-progress-pct" style={{ color: progColor }}>{progPts}%</span>
          </div>
          <div className="ev-progress-track">
            <div
              className="ev-progress-fill"
              style={{ width: `${progPts}%`, background: progColor }}
            />
          </div>
          <div className="ev-progress-criteria">
            {[
              { ok: progCriteria.sala,         label: 'Sala' },
              { ok: progCriteria.atividade,    label: 'Atividade' },
              { ok: progCriteria.staffAlocado, label: 'Staff alocado' },
              { ok: progCriteria.semConflitos, label: 'Sem conflitos', pending: !conflitosVerificados },
              { ok: progCriteria.orcamento,    label: 'Orçamento' },
            ].map(c => (
              <span key={c.label} className={`ev-progress-chip${c.ok ? ' ok' : ''}${c.pending ? ' pending' : ''}`}>
                {c.pending ? '…' : c.ok ? '✓' : '·'} {c.label}
              </span>
            ))}
          </div>
        </div>
      </div>

      <div className="detalhe-sections">

        {/* ── Salas ── */}
        <section className="detalhe-section">
          <SectionHeader title="Salas" count={salas.length} adding={addingSala} onAdd={() => setAddingSala(v => !v)} />
          {addingSala && <FormSala />}
          {salas.length === 0 && !addingSala
            ? <EmptyState
                icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M9 3v18M3 9h6M3 15h6"/></svg>}
                title="Nenhuma sala registada"
                hint="Adiciona os espaços e salas do evento"
                onAction={() => setAddingSala(true)}
                actionLabel="+ Adicionar sala"
              />
            : <div className="salas-grid">{salas.map(s => (
                <div key={s.id} className="sala-card">
                  <div className="sala-card-header">
                    <span className="sala-name">{s.nome}</span>
                    <button
                      className="btn-delete"
                      disabled={deletingIds.has(`salas-${s.id}`)}
                      onClick={() => confirmDelete('salas', s.id, s.nome, () => setSalas(p => p.filter(x => x.id !== s.id)))}
                    >✕</button>
                  </div>
                  <div className="sala-meta"><span>{s.capacidade} lugares</span>{s.localizacao && <span>{s.localizacao}</span>}</div>
                </div>
              ))}</div>
          }
        </section>

        {/* ── Run of Show ── */}
        <section className="detalhe-section">
          <SectionHeader
            title="Run of Show"
            count={atividades.length}
            adding={addingAtividade}
            onAdd={() => setAddingAtividade(v => !v)}
            extra={
              atividades.length > 0 && (
                <button type="button" className="btn-pdf" onClick={exportarPDF} title="Exportar cronograma para PDF">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="12" y1="18" x2="12" y2="12"/><polyline points="9 15 12 18 15 15"/>
                  </svg>
                  Exportar PDF
                </button>
              )
            }
          />
          {addingAtividade && <FormAtividade />}
          {atividades.length === 0 && !addingAtividade
            ? <EmptyState
                icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/><line x1="8" y1="14" x2="16" y2="14"/></svg>}
                title="Nenhuma atividade agendada"
                hint="Constrói o programa do evento com horários e salas"
                onAction={() => setAddingAtividade(true)}
                actionLabel="+ Adicionar atividade"
              />
            : <div className="ros-list">{atividades.map(a => (
                <div key={a.id} className="ros-item">
                  <div className="ros-time"><span>{fmtTime(a.horaInicio)}</span><span className="ros-time-sep">↓</span><span>{fmtTime(a.horaFim)}</span></div>
                  <div className="ros-bar" />
                  <div className="ros-body">
                    <span className="ros-name">{a.nome}</span>
                    {salaMap[a.salaId] && <span className="ros-sala">{salaMap[a.salaId]}</span>}
                    {a.descricao && <p className="ros-desc">{a.descricao}</p>}
                  </div>
                  <button
                    className="btn-delete"
                    style={{ alignSelf: 'center' }}
                    disabled={deletingIds.has(`atividades-${a.id}`)}
                    onClick={() => confirmDelete('atividades', a.id, a.nome, () => {
                      setAtividades(p => p.filter(x => x.id !== a.id));
                      loadConflitos();
                      loadAlocacoes();
                    })}
                  >✕</button>
                </div>
              ))}</div>
          }
        </section>

        {/* ── Staff ── */}
        <section className="detalhe-section">
          <SectionHeader
            title="Staff"
            count={staff.length}
            adding={addingStaff}
            onAdd={() => setAddingStaff(v => !v)}
            extra={
              <button
                type="button"
                className="btn-ai"
                onClick={() => setEscalaOpen(true)}
                title="Gerar alocações automáticas com IA"
              >
                ⚡ Gerar Escala
              </button>
            }
          />
          {addingStaff && <FormStaff />}
          {staff.length === 0 && !addingStaff
            ? <EmptyState
                icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><circle cx="12" cy="7" r="4"/><path d="M4 21c0-4 3.6-7 8-7s8 3 8 7"/></svg>}
                title="Nenhum membro de staff"
                hint="Adiciona a equipa e gera escalas automáticas com IA"
                onAction={() => setAddingStaff(true)}
                actionLabel="+ Adicionar staff"
              />
            : <div className="staff-list">{staff.map(s => (
                <div key={s.id} className="staff-item">
                  <div className="staff-avatar">{s.nome.charAt(0).toUpperCase()}</div>
                  <div className="staff-info"><span className="staff-nome">{s.nome}</span>{s.funcao && <span className="staff-funcao">{s.funcao}</span>}</div>
                  {s.contacto && <span className="staff-contacto">{s.contacto}</span>}
                  <button
                    className="btn-delete"
                    disabled={deletingIds.has(`staff-${s.id}`)}
                    onClick={() => confirmDelete('staff', s.id, s.nome, () => {
                      setStaff(p => p.filter(x => x.id !== s.id));
                      loadAlocacoes();
                    })}
                  >✕</button>
                </div>
              ))}</div>
          }
        </section>

        {/* ── Timetable do Staff ── */}
        <section className="detalhe-section">
          <SectionHeader title="Timetable do Staff" count={alocacoes.length} />
          <Timetable />
        </section>

        {escalaOpen && (
          <EscalaModal
            eventoId={eventoId}
            onClose={() => setEscalaOpen(false)}
            onApplied={() => {
              setEscalaOpen(false);
              api.get(`/staff?eventoId=${eventoId}`).then(setStaff).catch(() => {});
              loadConflitos();
              loadAlocacoes();
              toast('Escala aplicada com sucesso.');
            }}
          />
        )}

        {editOpen && (
          <EditEventoModal
            evento={evento}
            onClose={() => setEditOpen(false)}
            onSaved={updated => {
              setEvento(updated);
              setEditOpen(false);
              toast('Evento atualizado com sucesso.');
            }}
          />
        )}

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
            <EmptyState
              icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><circle cx="12" cy="12" r="9"/><path d="M12 7v10M9.5 9.5h3a1.5 1.5 0 010 3h-3a1.5 1.5 0 000 3H15"/></svg>}
              title="Nenhuma despesa registada"
              hint="Regista custos para controlar o orçamento"
              onAction={() => setAddingDespesa(true)}
              actionLabel="+ Adicionar despesa"
            />
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
                  <button
                    className="btn-delete"
                    disabled={deletingIds.has(`despesas-${d.id}`)}
                    onClick={() => confirmDelete('despesas', d.id, d.descricao, () => {
                      setDespesas(p => p.filter(x => x.id !== d.id));
                      loadResumo();
                    })}
                  >✕</button>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* ── Checklist ── */}
        <section className="detalhe-section">
          {(() => {
            const total = tarefas.length;
            const feitas = tarefas.filter(t => t.concluida).length;
            return (
              <SectionHeader
                title="Checklist"
                count={total > 0 ? `${feitas}/${total}` : 0}
                countClass={total > 0 && feitas === total ? 'success' : ''}
                adding={addingTarefa}
                onAdd={() => setAddingTarefa(v => !v)}
              />
            );
          })()}
          {addingTarefa && <FormTarefa />}
          {tarefas.length === 0 && !addingTarefa
            ? <EmptyState
                icon={<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.3"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11"/></svg>}
                title="Nenhuma tarefa"
                hint="Adiciona itens de checklist para acompanhar o progresso"
                onAction={() => setAddingTarefa(true)}
                actionLabel="+ Adicionar tarefa"
              />
            : (
              <div className="checklist-list">
                {tarefas.map(t => (
                  <div key={t.id} className={`checklist-item${t.concluida ? ' done' : ''}`}>
                    <button
                      className={`checklist-checkbox${t.concluida ? ' checked' : ''}`}
                      disabled={togglingTarefas.has(t.id)}
                      onClick={() => handleToggleTarefa(t)}
                      aria-label={t.concluida ? 'Marcar como pendente' : 'Marcar como concluída'}
                    >
                      {t.concluida && (
                        <svg width="10" height="10" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                          <polyline points="2 6 5 9 10 3" />
                        </svg>
                      )}
                    </button>
                    <span className="checklist-desc">{t.descricao}</span>
                    <button
                      className="btn-delete"
                      disabled={deletingIds.has(`tarefas-${t.id}`)}
                      onClick={() => confirmDelete('tarefas', t.id, t.descricao, () =>
                        setTarefas(p => p.filter(x => x.id !== t.id))
                      )}
                    >✕</button>
                  </div>
                ))}
              </div>
            )
          }
        </section>

        {/* ── Notas ── */}
        <section className="detalhe-section">
          <div className="section-title-row">
            <h3>Notas</h3>
            <span className="notas-status">
              {notasSaving && <><span className="spinner" style={{ width: 11, height: 11, borderWidth: 2 }} /> A guardar…</>}
              {!notasSaving && notasSaved && '✓ Guardado'}
            </span>
          </div>
          <textarea
            className="notas-textarea"
            value={notasLocal}
            onChange={e => handleNotasChange(e.target.value)}
            placeholder="Escreve notas livres sobre este evento…"
            rows={5}
          />
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
