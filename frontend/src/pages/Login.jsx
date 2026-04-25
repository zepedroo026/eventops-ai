import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import AuthHero from '../components/AuthHero';

export default function Login() {
  const [email,    setEmail]    = useState('');
  const [password, setPassword] = useState('');
  const [error,    setError]    = useState('');
  const [loading,  setLoading]  = useState(false);
  const navigate = useNavigate();

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const data = await api.post('/auth/login', { email, password });
      localStorage.setItem('token', data.token);
      localStorage.setItem('user', JSON.stringify({ nome: data.nome, email: data.email, perfil: data.perfil }));
      navigate('/dashboard');
    } catch (err) {
      setError(err.message.includes('401') || err.message.toLowerCase().includes('incorretos')
        ? 'Email ou password incorretos.'
        : 'Erro ao ligar ao servidor.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <AuthHero />

      <div className="auth-form-side">
        <div className="login-card">
          <h1 className="login-title">Bem-vindo de volta</h1>
          <p className="login-subtitle">Inicia sessão para continuar</p>

          <form onSubmit={handleSubmit} className="login-form" noValidate>
            <div className="field">
              <label htmlFor="email">Email</label>
              <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)}
                placeholder="nome@exemplo.com" required autoComplete="email" autoFocus />
            </div>
            <div className="field">
              <label htmlFor="password">Password</label>
              <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)}
                placeholder="••••••••" required autoComplete="current-password" />
            </div>
            {error && <div className="login-error" role="alert">{error}</div>}
            <button type="submit" className="btn-primary" disabled={loading}>
              {loading ? <span className="spinner" /> : 'Entrar'}
            </button>
          </form>

          <p className="login-footer">
            Ainda não tens conta? <Link to="/register">Criar conta</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
