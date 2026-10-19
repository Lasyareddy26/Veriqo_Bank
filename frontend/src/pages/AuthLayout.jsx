import { IconShield, IconCheck } from "../components/Icons";

export default function AuthLayout({ children }) {
  return (
    <div style={{ display: "flex", minHeight: "100vh" }}>
      {/* Left brand panel */}
      <div style={{
        flex: "1 1 46%", background: "linear-gradient(160deg, #2B2824 0%, #3A3029 55%, #4A3528 100%)",
        color: "#F4F2EC", padding: "56px 56px", display: "flex", flexDirection: "column", justifyContent: "space-between",
      }} className="auth-brand">
        <div className="row gap-2">
          <div style={{ width: 34, height: 34, borderRadius: 9, background: "var(--primary)", display: "grid", placeItems: "center", color: "#fff", fontFamily: "var(--font-serif)", fontWeight: 600, fontSize: 19 }}>I</div>
          <span style={{ fontFamily: "var(--font-serif)", fontSize: "1.3rem", fontWeight: 600 }}>Veriqo Bank</span>
        </div>

        <div>
          <h1 style={{ color: "#fff", fontSize: "2.5rem", lineHeight: 1.15, marginBottom: 18 }}>
            Banking onboarding,<br />done in minutes.
          </h1>
          <p style={{ color: "rgba(244,242,236,0.75)", fontSize: "1.05rem", maxWidth: 420, marginBottom: 32 }}>
            Apply for accounts, cards and loans. Upload your documents once — we verify them securely and keep you posted at every step.
          </p>
          <div className="stack gap-3">
            {["Bank-grade security & encrypted documents", "Automated KYC document verification", "Real-time application tracking"].map((t) => (
              <div key={t} className="row gap-3">
                <div style={{ width: 22, height: 22, borderRadius: "50%", background: "rgba(217,119,87,0.22)", display: "grid", placeItems: "center", color: "var(--primary)", flexShrink: 0 }}>
                  <IconCheck size={14} />
                </div>
                <span style={{ color: "rgba(244,242,236,0.88)", fontSize: "0.95rem" }}>{t}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="row gap-2" style={{ color: "rgba(244,242,236,0.55)", fontSize: "0.82rem" }}>
          <IconShield size={15} /> Your data is protected with industry-standard encryption.
        </div>
      </div>

      {/* Right form panel */}
      <div style={{ flex: "1 1 54%", display: "flex", alignItems: "center", justifyContent: "center", padding: "40px 24px", background: "var(--bg)" }}>
        <div style={{ width: "100%", maxWidth: 400 }} className="fade-in">{children}</div>
      </div>
    </div>
  );
}
