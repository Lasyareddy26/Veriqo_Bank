import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import AuthLayout from "./AuthLayout";
import { Spinner } from "../components/ui";
import GoogleButton from "../components/GoogleButton";

export default function Login() {
  const { login, googleLogin } = useAuth();
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
      navigate(u.role === "Admin" ? "/admin" : "/");
    } catch (err) {
      setError(err.response?.data?.message || "Unable to sign in.");
    } finally { setLoading(false); }
  };

  return (
    <AuthLayout>
      <h2 style={{ marginBottom: 6 }}>Welcome back</h2>
      <p className="muted" style={{ marginBottom: 26 }}>Sign in to continue to your dashboard.</p>

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
            onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" />
        </div>
        <button className="btn btn-primary btn-block btn-lg" disabled={loading} style={{ marginTop: 8 }}>
          {loading ? <Spinner /> : "Sign in"}
        </button>
      </form>

      <GoogleButton
        onCredential={async (cred) => { const u = await googleLogin(cred); navigate(u.role === "Admin" ? "/admin" : "/"); }}
        onError={setError} />

      <p className="muted" style={{ marginTop: 22, textAlign: "center", fontSize: "0.9rem" }}>
        New to Veriqo? <Link to="/register">Create an account</Link>
      </p>
      <p className="muted" style={{ marginTop: 10, textAlign: "center", fontSize: "0.82rem" }}>
        Bank staff? <Link to="/admin/login">Staff login</Link>
      </p>
    </AuthLayout>
  );
}
