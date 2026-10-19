import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import AuthLayout from "./AuthLayout";
import { Spinner } from "../components/ui";
import GoogleButton from "../components/GoogleButton";
import OtpVerify from "../components/OtpVerify";

const rules = [
  { test: (p) => p.length >= 8, label: "At least 8 characters" },
  { test: (p) => /[A-Z]/.test(p), label: "One uppercase letter" },
  { test: (p) => /[0-9]/.test(p), label: "One number" },
  { test: (p) => /[^A-Za-z0-9]/.test(p), label: "One symbol" },
];

export default function Register() {
  const { register, googleLogin } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [otp, setOtp] = useState(null); // { email, devOtp } once registered

  const allPass = rules.every((r) => r.test(password));

  const submit = async (e) => {
    e.preventDefault();
    setError(""); setLoading(true);
    try {
      const data = await register(email, password);
      if (data.requiresVerification) setOtp({ email: data.email, devOtp: data.devOtp });
      else navigate("/profile?welcome=1");
    } catch (err) {
      setError(err.response?.data?.message || "Unable to create account.");
    } finally { setLoading(false); }
  };

  if (otp) {
    return (
      <AuthLayout>
        <OtpVerify email={otp.email} initialDevOtp={otp.devOtp}
          onVerified={() => navigate("/profile?welcome=1")} />
      </AuthLayout>
    );
  }

  return (
    <AuthLayout>
      <h2 style={{ marginBottom: 6 }}>Create your account</h2>
      <p className="muted" style={{ marginBottom: 26 }}>Start your application in just a few steps.</p>

      {error && <div className="alert alert-error" style={{ marginBottom: 18 }}>{error}</div>}

      <form onSubmit={submit}>
        <div className="field">
          <label className="label">Email</label>
          <input className="input" type="email" value={email} required
            onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" />
        </div>
        <div className="field">
          <label className="label">Password</label>
          <input className="input" type="password" value={password} required
            onChange={(e) => setPassword(e.target.value)} placeholder="Create a strong password" />
        </div>

        <div className="grid" style={{ gridTemplateColumns: "1fr 1fr", gap: 6, marginBottom: 18 }}>
          {rules.map((r) => {
            const ok = r.test(password);
            return (
              <div key={r.label} className="row gap-2 small" style={{ color: ok ? "var(--success)" : "var(--text-muted)" }}>
                <span style={{ width: 15, height: 15, borderRadius: "50%", display: "grid", placeItems: "center", background: ok ? "var(--success-bg)" : "var(--bg-alt)", fontSize: 10 }}>{ok ? "✓" : ""}</span>
                {r.label}
              </div>
            );
          })}
        </div>

        <button className="btn btn-primary btn-block btn-lg" disabled={loading || !allPass}>
          {loading ? <Spinner /> : "Create account"}
        </button>
      </form>

      <GoogleButton
        onCredential={async (cred) => { const u = await googleLogin(cred); navigate(u.profileComplete ? "/" : "/profile?welcome=1"); }}
        onError={setError} />

      <p className="muted" style={{ marginTop: 22, textAlign: "center", fontSize: "0.9rem" }}>
        Already have an account? <Link to="/login">Sign in</Link>
      </p>
    </AuthLayout>
  );
}
