export default function Logo({ size = 28, showText = true }) {
  return (
    <span className="logo-wrap" aria-label="EventOps">
      <svg width={size} height={size} viewBox="0 0 28 28" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id="eo-g" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#c084fc" />
            <stop offset="100%" stopColor="#60a5fa" />
          </linearGradient>
        </defs>
        <polygon points="16,2 7,16 13,16 12,26 21,12 15,12" fill="url(#eo-g)" />
      </svg>
      {showText && <span className="logo-text">EventOps</span>}
    </span>
  );
}
