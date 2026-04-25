import Logo from './Logo';

const features = [
  { icon: '▦', text: 'Planeamento visual do programa' },
  { icon: '◈', text: 'Gestão inteligente de staff' },
  { icon: '◉', text: 'Controlo de orçamento em tempo real' },
];

export default function AuthHero() {
  return (
    <div className="auth-hero">
      <div className="auth-hero-content">
        <Logo size={40} />

        <div className="auth-hero-text">
          <h2 className="auth-hero-title">
            Gestão profissional<br />de eventos ao seu alcance
          </h2>
          <p className="auth-hero-subtitle">
            Tudo o que precisa para coordenar equipas, espaços e orçamentos num só lugar.
          </p>
        </div>

        <ul className="auth-hero-features">
          {features.map(f => (
            <li key={f.text} className="auth-hero-feature">
              <span className="auth-hero-feature-icon">{f.icon}</span>
              <span>{f.text}</span>
            </li>
          ))}
        </ul>

        <p className="auth-hero-quote">
          "A ferramenta Back-of-House que os profissionais de eventos merecem."
        </p>
      </div>
    </div>
  );
}
