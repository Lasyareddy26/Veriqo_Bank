import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import client from "../api/client";
import { useAuth } from "../context/AuthContext";
import { Spinner, Avatar, dateFmt, money } from "../components/ui";
import { IconUser, IconCheck } from "../components/Icons";

const empty = {
  fullName: "", pictureUrl: "", dateOfBirth: "", gender: "", nationality: "", governmentIdNumber: "",
  phone: "", employmentStatus: "", employerName: "", annualIncome: "",
  address: { line1: "", line2: "", city: "", state: "", postalCode: "", country: "" },
};

function Detail({ label, value }) {
  return (
    <div>
      <div className="small muted" style={{ marginBottom: 2 }}>{label}</div>
      <div style={{ fontWeight: 500 }}>{value || "—"}</div>
    </div>
  );
}

export default function Profile() {
  const { user, refreshProfileStatus } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const welcome = params.get("welcome");
  const [form, setForm] = useState(empty);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [editing, setEditing] = useState(false);
  const [complete, setComplete] = useState(false);

  const loadProfile = () => client.get("/profile").then((r) => {
    const p = r.data;
    setForm({
      fullName: p.fullName || "", pictureUrl: p.pictureUrl || "",
      dateOfBirth: p.dateOfBirth ? p.dateOfBirth.substring(0, 10) : "",
      gender: p.gender || "", nationality: p.nationality || "",
      governmentIdNumber: p.governmentIdNumber || "", phone: p.phone || "",
      employmentStatus: p.employmentStatus || "", employerName: p.employerName || "",
      annualIncome: p.annualIncome || "",
      address: { ...empty.address, ...(p.address || {}) },
    });
    setComplete(!!p.isComplete);
    setEditing(!p.isComplete); // new/incomplete profile opens straight into edit mode
  });

  useEffect(() => { loadProfile().finally(() => setLoading(false)); }, []);

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const setAddr = (k) => (e) => setForm((f) => ({ ...f, address: { ...f.address, [k]: e.target.value } }));

  const submit = async (e) => {
    e.preventDefault();
    setError(""); setSaving(true);
    try {
      await client.put("/profile", { ...form, annualIncome: Number(form.annualIncome) });
      refreshProfileStatus(true);
      setComplete(true);
      setEditing(false);
      if (welcome) navigate("/products");
      else window.scrollTo({ top: 0, behavior: "smooth" });
    } catch (err) {
      setError(err.response?.data?.message || "Could not save profile.");
    } finally { setSaving(false); }
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  // ---------- VIEW MODE (saved profile card) ----------
  if (complete && !editing) {
    const a = form.address;
    return (
      <div className="fade-in" style={{ maxWidth: 760 }}>
        <h1 style={{ fontSize: "1.7rem", marginBottom: 20 }}>Your profile</h1>

        <div className="card" style={{ overflow: "hidden" }}>
          <div style={{ height: 88, background: "linear-gradient(120deg, #3A3029, #4A3528)" }} />
          <div className="card-pad" style={{ paddingTop: 0 }}>
            <div className="row between" style={{ alignItems: "flex-end", marginTop: -34, marginBottom: 18 }}>
              <div className="row gap-3" style={{ alignItems: "flex-end" }}>
                <div style={{ border: "4px solid var(--surface)", borderRadius: "50%", background: "var(--surface)" }}>
                  <Avatar src={form.pictureUrl} name={form.fullName} email={user?.email} size={76} />
                </div>
                <div style={{ paddingBottom: 6 }}>
                  <h2 style={{ fontSize: "1.4rem" }}>{form.fullName}</h2>
                  <div className="small muted">{user?.email}</div>
                </div>
              </div>
              <button className="btn btn-ghost btn-sm" onClick={() => setEditing(true)}>Edit profile</button>
            </div>

            <div className="grid" style={{ gridTemplateColumns: "1fr 1fr 1fr", gap: 18, paddingTop: 6 }}>
              <Detail label="Date of birth" value={form.dateOfBirth ? dateFmt(form.dateOfBirth) : "—"} />
              <Detail label="Gender" value={form.gender} />
              <Detail label="Nationality" value={form.nationality} />
              <Detail label="Government ID" value={form.governmentIdNumber} />
              <Detail label="Phone" value={form.phone} />
              <Detail label="Annual income" value={form.annualIncome ? money(form.annualIncome) : "—"} />
              <Detail label="Employment" value={form.employmentStatus} />
              <Detail label="Employer" value={form.employerName} />
            </div>

            <div className="divider" />
            <div className="small muted" style={{ marginBottom: 8 }}>Address</div>
            <div style={{ fontWeight: 500 }}>
              {[a.line1, a.line2, a.city, a.state, a.postalCode, a.country].filter(Boolean).join(", ")}
            </div>
          </div>
        </div>

        <div className="row gap-2" style={{ marginTop: 16 }}>
          <span className="badge badge-success"><IconCheck size={13} /> Verified profile</span>
          <span className="small muted">Used to verify your identity across all applications.</span>
        </div>
      </div>
    );
  }

  // ---------- EDIT MODE (form) ----------
  return (
    <div className="fade-in" style={{ maxWidth: 720 }}>
      <div className="row gap-3" style={{ marginBottom: 6 }}>
        <div style={{ width: 40, height: 40, borderRadius: 11, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)" }}><IconUser /></div>
        <div>
          <h1 style={{ fontSize: "1.7rem" }}>{complete ? "Edit profile" : "Your profile"}</h1>
          <p className="muted">We use these details to verify your identity for every application.</p>
        </div>
      </div>

      {welcome && <div className="alert alert-info" style={{ margin: "18px 0" }}>Welcome to Veriqo! Complete your profile to unlock product applications.</div>}
      {error && <div className="alert alert-error" style={{ margin: "18px 0" }}>{error}</div>}

      <form onSubmit={submit} className="stack gap-4" style={{ marginTop: 20 }}>
        <section className="card card-pad">
          <h3 style={{ marginBottom: 16 }}>Personal details</h3>
          <div className="grid" style={{ gridTemplateColumns: "1fr 1fr" }}>
            <div className="field" style={{ gridColumn: "1 / -1" }}>
              <label className="label">Full legal name *</label>
              <input className="input" required value={form.fullName} onChange={set("fullName")} placeholder="As printed on your ID" />
            </div>
            <div className="field">
              <label className="label">Date of birth *</label>
              <input className="input" type="date" required value={form.dateOfBirth} onChange={set("dateOfBirth")} max={new Date().toISOString().slice(0, 10)} />
            </div>
            <div className="field">
              <label className="label">Gender</label>
              <select className="select" value={form.gender} onChange={set("gender")}>
                <option value="">Prefer not to say</option><option>Female</option><option>Male</option><option>Other</option>
              </select>
            </div>
            <div className="field">
              <label className="label">Nationality *</label>
              <input className="input" required value={form.nationality} onChange={set("nationality")} placeholder="Indian" />
            </div>
            <div className="field">
              <label className="label">Government ID number *</label>
              <input className="input" required value={form.governmentIdNumber} onChange={set("governmentIdNumber")} placeholder="Passport / National ID" />
              <div className="help">This is matched against your uploaded ID document.</div>
            </div>
          </div>
        </section>

        <section className="card card-pad">
          <h3 style={{ marginBottom: 16 }}>Contact & address</h3>
          <div className="grid" style={{ gridTemplateColumns: "1fr 1fr" }}>
            <div className="field">
              <label className="label">Phone *</label>
              <input className="input" required value={form.phone} onChange={set("phone")} placeholder="+91 ..." />
            </div>
            <div className="field" />
            <div className="field" style={{ gridColumn: "1 / -1" }}>
              <label className="label">Address line 1 *</label>
              <input className="input" required value={form.address.line1} onChange={setAddr("line1")} />
            </div>
            <div className="field" style={{ gridColumn: "1 / -1" }}>
              <label className="label">Address line 2</label>
              <input className="input" value={form.address.line2} onChange={setAddr("line2")} />
            </div>
            <div className="field"><label className="label">City *</label><input className="input" required value={form.address.city} onChange={setAddr("city")} /></div>
            <div className="field"><label className="label">State *</label><input className="input" required value={form.address.state} onChange={setAddr("state")} /></div>
            <div className="field"><label className="label">Postal code *</label><input className="input" required value={form.address.postalCode} onChange={setAddr("postalCode")} /></div>
            <div className="field"><label className="label">Country *</label><input className="input" required value={form.address.country} onChange={setAddr("country")} /></div>
          </div>
        </section>

        <section className="card card-pad">
          <h3 style={{ marginBottom: 16 }}>Employment</h3>
          <div className="grid" style={{ gridTemplateColumns: "1fr 1fr" }}>
            <div className="field">
              <label className="label">Employment status *</label>
              <select className="select" required value={form.employmentStatus} onChange={set("employmentStatus")}>
                <option value="">Select…</option><option>Salaried</option><option>Self-employed</option><option>Student</option><option>Unemployed</option><option>Retired</option>
              </select>
            </div>
            <div className="field">
              <label className="label">Employer name</label>
              <input className="input" value={form.employerName} onChange={set("employerName")} />
            </div>
            <div className="field">
              <label className="label">Annual income (₹) *</label>
              <input className="input" type="number" min="0" required value={form.annualIncome} onChange={set("annualIncome")} placeholder="600000" />
              <div className="help">Used for eligibility and income verification.</div>
            </div>
          </div>
        </section>

        <div className="row gap-3">
          <button className="btn btn-primary btn-lg" disabled={saving}>
            {saving ? <Spinner /> : welcome ? "Save & continue" : "Save profile"}
          </button>
          {complete && <button type="button" className="btn btn-ghost btn-lg" onClick={() => { setEditing(false); loadProfile(); }}>Cancel</button>}
        </div>
      </form>
    </div>
  );
}
