import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import AuthHero from '../components/AuthHero';

const PERFIS = ['Organizador', 'Staff', 'Administrador'];

export default function Register() {
  const [form, setForm] = useState({ nome: '', email: '', password: '', confirmar: '', perfil: 'Organizador' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  function set(field) {
    return e => setForm(f => ({ ...f, [field]: e.target.value }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');

    if (form.password !== form.confirmar) {
      setError('As passwords não coincidem.');
      return;
    }
    if (form.password.length < 6) {
      setError('A password deve ter pelo menos 6 caracteres.');
      return;
    }

    setLoading(true);
    try {
      await api.post('/auth/register', {
        nome: form.nome,
        email: form.email,
        password: form.password,
        perfil: form.perfil,
      });
      navigate('/login');
    } catch (err) {
      if (err.message.toLowerCase().includes('conflict') || err.message.includes('409') || err.message.toLowerCase().includes('registado')) {
        setError('Este email já está registado.');
      } else {
        setError('Erro ao criar conta. Tenta novamente.');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <AuthHero />

      <div className="auth-form-side">
        <div className="login-card">
          <h1 className="login-title">Criar conta</h1>
          <p className="login-subtitle">Preenche os dados para te registares</p>

          <form onSubmit={handleSubmit} className="login-form" noValidate>
            <div className="field">
              <label htmlFor="nome">Nome</label>
              <input id="nome" type="text" value={form.nome} onChange={set('nome')}
                placeholder="O teu nome" required autoComplete="name" autoFocus />
            </div>
            <div className="field">
              <label htmlFor="email">Email</label>
              <input id="email" type="email" value={form.email} onChange={set('email')}
                placeholder="nome@exemplo.com" required autoComplete="email" />
            </div>
            <div className="field">
              <label htmlFor="perfil">Perfil</label>
              <select id="perfil" value={form.perfil} onChange={set('perfil')} className="field-select">
                {PERFIS.map(p => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div className="field">
              <label htmlFor="password">Password</label>
              <input id="password" type="password" value={form.password} onChange={set('password')}
                placeholder="Mínimo 6 caracteres" required autoComplete="new-password" />
            </div>
            <div className="field">
              <label htmlFor="confirmar">Confirmar password</label>
              <input id="confirmar" type="password" value={form.confirmar} onChange={set('confirmar')}
                placeholder="Repete a password" required autoComplete="new-password" />
            </div>
            {error && <div className="login-error" role="alert">{error}</div>}
            <button type="submit" className="btn-primary" disabled={loading}>
              {loading ? <span className="spinner" /> : 'Criar conta'}
            </button>
          </form>

          <p className="login-footer">
            Já tens conta? <Link to="/login">Entra aqui</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
