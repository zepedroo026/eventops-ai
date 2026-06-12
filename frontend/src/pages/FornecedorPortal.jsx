import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useToast } from '../components/Toast';

const fmtBytes = b => b < 1024 ? `${b} B` : b < 1048576 ? `${(b/1024).toFixed(1)} KB` : `${(b/1048576).toFixed(1)} MB`;
const fmtDate  = s => new Date(s).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });

const TIPO_LABEL = { Fatura: 'Fatura', MaterialGrafico: 'Material Gráfico', Outro: 'Outro' };
const TIPO_ICON  = { Fatura: '🧾', MaterialGrafico: '🎨', Outro: '📎' };

const MAX_BYTES = 10 * 1024 * 1024;
const ALLOWED   = ['.pdf', '.png', '.jpg', '.jpeg', '.svg'];

export default function FornecedorPortal() {
  const navigate    = useNavigate();
  const toast       = useToast();
  const fileRef     = useRef(null);
  const [fornecedor, setFornecedor] = useState(null);
  const [ficheiros,  setFicheiros]  = useState([]);
  const [loading,    setLoading]    = useState(true);
  const [uploading,  setUploading]  = useState(false);
  const [dragging,   setDragging]   = useState(false);
  const [tipo,       setTipo]       = useState('Outro');
  const [error,      setError]      = useState('');

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  useEffect(() => {
    if (user.perfil !== 'Fornecedor') { navigate('/dashboard', { replace: true }); return; }
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      if (!token) { navigate('/login', { replace: true }); return; }
      // Resolve fornecedorId from token sub claim
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')));
      const fornecedorId = payload.fornecedorId ?? user.fornecedorId;
      if (!fornecedorId) { setError('Conta sem fornecedor associado.'); setLoading(false); return; }

      const [forn, fichs] = await Promise.all([
        api.get(`/fornecedores/${fornecedorId}`),
        api.get(`/fornecedores/ficheiros?fornecedorId=${fornecedorId}`),
      ]);
      setFornecedor(forn);
      setFicheiros(fichs ?? []);
    } catch {
      setError('Não foi possível carregar os dados.');
    } finally {
      setLoading(false);
    }
  }

  function validateFile(file) {
    const ext = '.' + file.name.split('.').pop().toLowerCase();
    if (!ALLOWED.includes(ext)) return `Extensão não permitida. Aceites: ${ALLOWED.join(', ')}`;
    if (file.size > MAX_BYTES) return 'O ficheiro não pode ter mais de 10 MB.';
    return null;
  }

  async function upload(file) {
    const err = validateFile(file);
    if (err) { toast(err, 'error'); return; }
    setUploading(true);
    try {
      const form = new FormData();
      form.append('ficheiro', file);
      form.append('tipo', tipo);
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/fornecedores/${fornecedor.id}/ficheiros`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        body: form,
      });
      if (!res.ok) { const t = await res.text(); throw new Error(t); }
      toast('Ficheiro carregado com sucesso.');
      await loadData();
    } catch (ex) {
      toast(ex.message || 'Erro ao carregar ficheiro.', 'error');
    } finally {
      setUploading(false);
    }
  }

  function onFileChange(e) {
    const file = e.target.files?.[0];
    if (file) upload(file);
    e.target.value = '';
  }

  function onDrop(e) {
    e.preventDefault(); setDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) upload(file);
  }

  async function handleDelete(id, nome) {
    if (!window.confirm(`Eliminar "${nome}"?`)) return;
    try {
      await api.delete(`/fornecedores/ficheiros/${id}`);
      setFicheiros(p => p.filter(f => f.id !== id));
      toast('Ficheiro eliminado.');
    } catch (ex) {
      toast(ex.message || 'Erro ao eliminar.', 'error');
    }
  }

  if (loading) return (
    <div className="dash-page">
      <div className="skeleton" style={{ height: 28, width: 200, marginBottom: 24 }} />
      <div className="skeleton" style={{ height: 160 }} />
    </div>
  );

  if (error) return (
    <div className="dash-state error"><p>{error}</p></div>
  );

  return (
    <div className="dash-page">
      <div className="dash-title-row">
        <h2>{fornecedor?.nome ?? 'Portal do Fornecedor'}</h2>
        <span className="badge">Fornecedor</span>
      </div>
      {fornecedor?.categoria && (
        <p style={{ color: 'var(--text)', fontSize: 14, marginBottom: 8 }}>{fornecedor.categoria}</p>
      )}

      {/* Upload zone */}
      <div
        className={`upload-zone${dragging ? ' dragging' : ''}`}
        onDragOver={e => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        onClick={() => fileRef.current?.click()}
        role="button"
        tabIndex={0}
        onKeyDown={e => e.key === 'Enter' && fileRef.current?.click()}
      >
        <input ref={fileRef} type="file" accept={ALLOWED.join(',')} style={{ display: 'none' }} onChange={onFileChange} />
        {uploading
          ? <><span className="spinner large" /><p style={{ color: 'var(--text)', marginTop: 12 }}>A carregar…</p></>
          : <>
              <span className="upload-zone-icon">📁</span>
              <p className="upload-zone-label">Arrasta um ficheiro aqui ou clica para selecionar</p>
              <p className="upload-zone-hint">PDF, PNG, JPG, SVG — máx. 10 MB</p>
            </>
        }
      </div>

      {/* Tipo selector */}
      <div className="field" style={{ maxWidth: 280, marginBottom: 24 }}>
        <label>Tipo de ficheiro</label>
        <select className="field-select" value={tipo} onChange={e => setTipo(e.target.value)}>
          {Object.entries(TIPO_LABEL).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
      </div>

      {/* File list */}
      <h3 style={{ fontSize: 15, marginBottom: 14 }}>Os Meus Ficheiros ({ficheiros.length})</h3>
      {ficheiros.length === 0
        ? <p className="section-empty">Nenhum ficheiro carregado ainda.</p>
        : (
          <div className="ficheiros-list">
            {ficheiros.map(f => (
              <div key={f.id} className="ficheiro-item">
                <span className="ficheiro-icon">{TIPO_ICON[f.tipo] ?? '📎'}</span>
                <div className="ficheiro-info">
                  <span className="ficheiro-nome">{f.nomeOriginal}</span>
                  <span className="ficheiro-meta">{TIPO_LABEL[f.tipo]} · {fmtBytes(f.tamanhoBytes)} · {fmtDate(f.dataUpload)}</span>
                </div>
                <a
                  href={`/api/fornecedores/ficheiros/${f.id}/download`}
                  className="btn-pdf"
                  download={f.nomeOriginal}
                  onClick={e => e.stopPropagation()}
                >Descarregar</a>
                <button className="btn-delete" onClick={() => handleDelete(f.id, f.nomeOriginal)} title="Eliminar">✕</button>
              </div>
            ))}
          </div>
        )
      }
    </div>
  );
}
