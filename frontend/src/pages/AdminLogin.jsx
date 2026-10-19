import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { Spinner } from "../components/ui";
import { IconShield } from "../components/Icons";

export default function AdminLogin() {
  const { login, logout } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setError(""); setLoading(true);
    try {
      const u = await login(email, password);
      if (u.role !== "Admin") {
        logout();
        setError("This is not a staff account. Please use the customer login.");
        return;
      }
      navigate("/admin");
    } catch (err) {
      setError(err.response?.data?.message || "Unable to sign in.");
    } finally { setLoading(false); }
  };

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "linear-gradient(160deg, #2B2824 0%, #3A3029 60%, #4A3528 100%)", padding: 24 }}>
      <div style={{ width: "100%", maxWidth: 400 }} className="fade-in">
        <div className="row gap-2" style={{ justifyContent: "center", marginBottom: 20 }}>
          <div style={{ width: 34, height: 34, borderRadius: 9, background: "var(--primary)", display: "grid", placeItems: "center", color: "#fff", fontFamily: "var(--font-serif)", fontWeight: 600, fontSize: 19 }}>I</div>
          <span style={{ fontFamily: "var(--font-serif)", fontSize: "1.3rem", fontWeight: 600, color: "#F4F2EC" }}>Veriqo</span>
        </div>

        <div className="card card-pad">
          <div className="row gap-2" style={{ marginBottom: 6 }}>
            <IconShield size={20} style={{ color: "var(--primary-hover)" }} />
            <h2 style={{ fontSize: "1.3rem" }}>Staff portal</h2>
          </div>
          <p className="muted" style={{ marginBottom: 22, fontSize: "0.9rem" }}>Authorized bank staff only.</p>

          {error && <div className="alert alert-error" style={{ marginBottom: 16 }}>{error}</div>}

          <form onSubmit={submit}>
            <div className="field">
              <label className="label">Work email</label>
              <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@workmail.com" />
            </div>
            <div className="field">
              <label className="label">Password</label>
              <input className="input" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" />
            </div>
            <button className="btn btn-primary btn-block btn-lg" disabled={loading} style={{ marginTop: 8 }}>
              {loading ? <Spinner /> : "Sign in to console"}
            </button>
          </form>
        </div>

        <p style={{ marginTop: 18, textAlign: "center", fontSize: "0.85rem" }}>
          <Link to="/login" style={{ color: "rgba(244,242,236,0.75)" }}>← Customer login</Link>
        </p>
      </div>
    </div>
  );
}
