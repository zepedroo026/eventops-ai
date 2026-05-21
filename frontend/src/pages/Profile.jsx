import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useToast } from '../components/Toast';

export default function Profile() {
  const navigate = useNavigate();
  const toast    = useToast();

  const user = (() => {
    try { return JSON.parse(localStorage.getItem('user') || '{}'); }
    catch { return {}; }
  })();

  const [nome,              setNome]              = useState(user.nome || '');
  const [passwordAtual,     setPasswordAtual]     = useState('');
  const [novaPassword,      setNovaPassword]      = useState('');
  const [confirmarPassword, setConfirmarPassword] = useState('');
  const [loading,           setLoading]           = useState(false);
  const [error,             setError]             = useState('');

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');

    if (novaPassword && novaPassword !== confirmarPassword) {
      setError('As passwords não coincidem.');
      return;
    }
    if (novaPassword && novaPassword.length < 6) {
      setError('A nova password deve ter pelo menos 6 caracteres.');
      return;
    }

    setLoading(true);
    try {
      const res = await api.put('/auth/perfil', {
        nome: nome.trim(),
        passwordAtual: passwordAtual || null,
        novaPassword:  novaPassword  || null,
      });
      const stored = JSON.parse(localStorage.getItem('user') || '{}');
      localStorage.setItem('user', JSON.stringify({ ...stored, nome: res.nome }));
      toast('Perfil atualizado com sucesso.');
      setPasswordAtual('');
      setNovaPassword('');
      setConfirmarPassword('');
    } catch (ex) {
      setError(ex.message || 'Erro ao atualizar perfil.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="dash-page">
      <div className="dash-title-row">
        <h2>O Meu Perfil</h2>
        {user.perfil && <span className="badge">{user.perfil}</span>}
      </div>

      <div className="profile-card">
        <form className="profile-form" onSubmit={handleSubmit} noValidate>

          <p className="profile-section-title">Informação pessoal</p>

          <div className="field">
            <label>Nome</label>
            <input
              type="text"
              value={nome}
              onChange={e => setNome(e.target.value)}
              required
            />
          </div>

          <div className="field">
            <label>Email</label>
            <input type="email" value={user.email || ''} disabled />
          </div>

          <div className="profile-divider" />

          <p className="profile-section-title">
            Alterar password
            <span className="profile-optional">opcional</span>
          </p>

          <div className="field">
            <label>Password atual</label>
            <input
              type="password"
              value={passwordAtual}
              onChange={e => setPasswordAtual(e.target.value)}
              placeholder="••••••••"
              autoComplete="current-password"
            />
          </div>

          <div className="field">
            <label>Nova password</label>
            <input
              type="password"
              value={novaPassword}
              onChange={e => setNovaPassword(e.target.value)}
              placeholder="••••••••"
              autoComplete="new-password"
            />
          </div>

          <div className="field">
            <label>Confirmar nova password</label>
            <input
              type="password"
              value={confirmarPassword}
              onChange={e => setConfirmarPassword(e.target.value)}
              placeholder="••••••••"
              autoComplete="new-password"
            />
          </div>

          {error && <p className="profile-error">{error}</p>}

          <div className="profile-actions">
            <button type="button" className="btn-secondary" onClick={() => navigate(-1)}>
              Cancelar
            </button>
            <button type="submit" className="btn-primary" disabled={loading || !nome.trim()}>
              {loading ? <span className="spinner" /> : 'Guardar alterações'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
