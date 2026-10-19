export function Spinner({ size = 18 }) {
  return (
    <svg className="spin" width={size} height={size} viewBox="0 0 24 24" fill="none">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity="0.2" strokeWidth="3" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

const STATUS_MAP = {
  Draft: ["badge-neutral", "Draft"],
  Submitted: ["badge-info", "Submitted"],
  UnderReview: ["badge-primary", "Under Review"],
  ActionRequired: ["badge-warning", "Action Required"],
  Approved: ["badge-success", "Approved"],
  Rejected: ["badge-error", "Rejected"],
};

export function StatusBadge({ status }) {
  const [cls, label] = STATUS_MAP[status] || ["badge-neutral", status];
  return <span className={`badge ${cls}`}><span className="badge-dot" />{label}</span>;
}

const RISK_MAP = {
  Low: ["badge-success", "Low risk"],
  Medium: ["badge-warning", "Medium risk"],
  High: ["badge-error", "High risk"],
};

export function RiskBadge({ level }) {
  const [cls, label] = RISK_MAP[level] || ["badge-neutral", level];
  return <span className={`badge ${cls}`}>{label}</span>;
}

const DOC_STATUS = {
  Pending: ["badge-warning", "Pending review"],
  Verified: ["badge-success", "Verified"],
  Rejected: ["badge-error", "Rejected"],
};

export function DocStatusBadge({ status }) {
  const [cls, label] = DOC_STATUS[status] || ["badge-neutral", status];
  return <span className={`badge ${cls}`}>{label}</span>;
}

const CHECK_COLOR = { pass: "var(--success)", warn: "var(--warning)", fail: "var(--error)" };
const CHECK_BG = { pass: "var(--success-bg)", warn: "var(--warning-bg)", fail: "var(--error-bg)" };

export function VerificationChecks({ data }) {
  const checks = data?.checks || [];
  const verdict = data?.verdict;
  const conf = data?.ocrConfidence;
  if (checks.length === 0) return null;
  return (
    <div style={{ marginTop: 10 }}>
      {verdict && (() => {
        const v = verdict.toLowerCase();
        const color = (v.includes("fraud") || v.includes("tamper")) ? "var(--error)"
          : v.includes("review") ? "var(--warning)" : "var(--success)";
        return (
          <div className="row gap-2" style={{ marginBottom: 10 }}>
            <span className="small muted">Verdict:</span>
            <span style={{ fontWeight: 600, fontSize: "0.85rem", color }}>{verdict}</span>
            {conf != null && <span className="small muted">· OCR {Math.round(conf)}%</span>}
          </div>
        );
      })()}
      <div className="stack gap-2">
        {checks.map((c, i) => (
          <div key={i} className="row gap-2" style={{ fontSize: "0.82rem" }}>
            <span style={{ width: 16, height: 16, borderRadius: "50%", flexShrink: 0, marginTop: 1, display: "grid", placeItems: "center", background: CHECK_BG[c.status], color: CHECK_COLOR[c.status], fontSize: 10, fontWeight: 700 }}>
              {c.status === "pass" ? "✓" : c.status === "warn" ? "!" : "✕"}
            </span>
            <span style={{ fontWeight: 550, minWidth: 110 }}>{c.label}</span>
            <span className="muted" style={{ flex: 1 }}>{c.detail}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

const TICKET_MAP = {
  Open: ["badge-info", "Open"],
  PendingCustomer: ["badge-warning", "Awaiting you"],
  Resolved: ["badge-success", "Resolved"],
  Closed: ["badge-neutral", "Closed"],
};
export function TicketStatusBadge({ status }) {
  const [cls, label] = TICKET_MAP[status] || ["badge-neutral", status];
  return <span className={`badge ${cls}`}>{label}</span>;
}

export function BotAvatar({ size = 40 }) {
  return (
    <div className="bot-avatar" style={{ width: size, height: size }} aria-label="Support assistant">
      <svg viewBox="0 0 40 40" width={size} height={size} fill="none">
        {/* antenna */}
        <line x1="20" y1="4" x2="20" y2="10" stroke="var(--primary)" strokeWidth="2" strokeLinecap="round" />
        <circle className="bot-antenna" cx="20" cy="4" r="2.4" fill="var(--primary)" />
        {/* head */}
        <rect x="7" y="10" width="26" height="22" rx="7" fill="var(--primary)" />
        <rect x="7" y="10" width="26" height="22" rx="7" fill="url(#bg)" opacity="0.15" />
        {/* eyes */}
        <circle className="bot-eye" cx="15.5" cy="20" r="2.6" fill="#fff" />
        <circle className="bot-eye" cx="24.5" cy="20" r="2.6" fill="#fff" />
        {/* smile */}
        <path d="M15.5 25.5c1.3 1.4 7.7 1.4 9 0" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" fill="none" />
        {/* ears */}
        <rect x="4" y="18" width="3" height="6" rx="1.5" fill="var(--primary)" />
        <rect x="33" y="18" width="3" height="6" rx="1.5" fill="var(--primary)" />
        <defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop stopColor="#fff" /><stop offset="1" stopColor="#000" /></linearGradient></defs>
      </svg>
    </div>
  );
}

export function Avatar({ src, name, email, size = 44 }) {
  const letter = (name?.trim()?.[0] || email?.[0] || "?").toUpperCase();
  const style = { width: size, height: size, borderRadius: "50%", flexShrink: 0, objectFit: "cover" };
  if (src) return <img src={src} alt={name || "avatar"} style={style} referrerPolicy="no-referrer" />;
  return (
    <div style={{ ...style, background: "var(--primary-soft)", color: "var(--primary-hover)", display: "grid", placeItems: "center", fontWeight: 600, fontSize: size * 0.4, fontFamily: "var(--font-serif)" }}>
      {letter}
    </div>
  );
}

export function EmptyState({ icon, title, text, action }) {
  return (
    <div className="card card-pad fade-in" style={{ textAlign: "center", padding: "48px 24px" }}>
      <div style={{ color: "var(--text-muted)", display: "flex", justifyContent: "center", marginBottom: 14 }}>{icon}</div>
      <h3 style={{ marginBottom: 6 }}>{title}</h3>
      <p className="muted" style={{ maxWidth: 380, margin: "0 auto 18px" }}>{text}</p>
      {action}
    </div>
  );
}

export const money = (n) =>
  "₹" + Number(n || 0).toLocaleString("en-IN", { maximumFractionDigits: 0 });

export const dateFmt = (d) =>
  new Date(d).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" });

export const dateTimeFmt = (d) =>
  new Date(d).toLocaleString("en-IN", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
