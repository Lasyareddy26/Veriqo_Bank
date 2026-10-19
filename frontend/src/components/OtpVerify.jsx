import { useEffect, useRef, useState } from "react";
import { useAuth } from "../context/AuthContext";
import { Spinner } from "./ui";
import { IconBell } from "./Icons";

/**
 * 6-digit email OTP entry. Calls verifyOtp on submit and onVerified(user) on success.
 * Shows a dev code hint when the backend returns one (SMTP not configured).
 */
export default function OtpVerify({ email, initialDevOtp, onVerified }) {
  const { verifyOtp, resendOtp } = useAuth();
  const [digits, setDigits] = useState(["", "", "", "", "", ""]);
  const [devOtp, setDevOtp] = useState(initialDevOtp);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [cooldown, setCooldown] = useState(0);
  const refs = useRef([]);

  useEffect(() => { refs.current[0]?.focus(); }, []);
  useEffect(() => {
    if (cooldown <= 0) return;
    const t = setTimeout(() => setCooldown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [cooldown]);

  const code = digits.join("");

  const setDigit = (i, v) => {
    const chars = v.replace(/\D/g, "");
    if (chars.length <= 1) {
      setDigits((d) => { const n = [...d]; n[i] = chars; return n; });
      if (chars && i < 5) refs.current[i + 1]?.focus();
      return;
    }
    // multiple digits (paste / autofill / fast entry) — spread across the boxes
    setDigits((d) => {
      const n = [...d];
      for (let k = 0; k < chars.length && i + k < 6; k++) n[i + k] = chars[k];
      return n;
    });
    refs.current[Math.min(i + chars.length, 5)]?.focus();
  };
  const onKey = (i, e) => {
    if (e.key === "Backspace" && !digits[i] && i > 0) refs.current[i - 1]?.focus();
  };
  const onPaste = (e) => {
    const txt = (e.clipboardData.getData("text") || "").replace(/\D/g, "").slice(0, 6);
    if (txt) { setDigits(txt.padEnd(6, "").split("").slice(0, 6).map((c) => c || "")); e.preventDefault(); refs.current[Math.min(txt.length, 5)]?.focus(); }
  };

  const submit = async (e) => {
    e?.preventDefault();
    if (code.length !== 6) return;
    setError(""); setLoading(true);
    try {
      const u = await verifyOtp(email, code);
      onVerified(u);
    } catch (err) {
      setError(err.response?.data?.message || "Verification failed.");
      setDigits(["", "", "", "", "", ""]); refs.current[0]?.focus();
    } finally { setLoading(false); }
  };

  const resend = async () => {
    setResending(true); setError("");
    try {
      const r = await resendOtp(email);
      setDevOtp(r.devOtp);
      setCooldown(30);
    } catch (err) {
      setError(err.response?.data?.message || "Could not resend code.");
    } finally { setResending(false); }
  };

  return (
    <div className="fade-in">
      <div style={{ width: 44, height: 44, borderRadius: 12, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)", marginBottom: 14 }}>
        <IconBell size={22} />
      </div>
      <h2 style={{ marginBottom: 6 }}>Verify your email</h2>
      <p className="muted" style={{ marginBottom: 20 }}>
        We sent a 6-digit code to <strong className="soft">{email}</strong>. Enter it below to continue.
      </p>

      {devOtp && (
        <div className="alert alert-info" style={{ marginBottom: 16 }}>
          Email isn’t configured, so here’s your code for testing: <strong className="mono">{devOtp}</strong>
        </div>
      )}
      {error && <div className="alert alert-error" style={{ marginBottom: 16 }}>{error}</div>}

      <form onSubmit={submit}>
        <div className="row gap-2" style={{ justifyContent: "space-between", marginBottom: 18 }} onPaste={onPaste}>
          {digits.map((d, i) => (
            <input key={i} ref={(el) => (refs.current[i] = el)} className="input" inputMode="numeric"
              value={d} onChange={(e) => setDigit(i, e.target.value)} onKeyDown={(e) => onKey(i, e)}
              style={{ width: 48, height: 56, textAlign: "center", fontSize: "1.4rem", fontWeight: 600, padding: 0 }} />
          ))}
        </div>
        <button className="btn btn-primary btn-block btn-lg" disabled={loading || code.length !== 6}>
          {loading ? <Spinner /> : "Verify & continue"}
        </button>
      </form>

      <p className="muted" style={{ marginTop: 18, textAlign: "center", fontSize: "0.9rem" }}>
        Didn’t get it?{" "}
        {cooldown > 0 ? <span className="muted">Resend in {cooldown}s</span>
          : <button type="button" onClick={resend} disabled={resending} style={{ background: "none", border: "none", color: "var(--primary-hover)", cursor: "pointer", fontWeight: 600 }}>
              {resending ? "Sending…" : "Resend code"}
            </button>}
      </p>
    </div>
  );
}
