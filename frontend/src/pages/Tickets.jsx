import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import client from "../api/client";
import { Spinner, TicketStatusBadge, EmptyState, dateFmt } from "../components/ui";
import { IconChat, IconChevron } from "../components/Icons";

const CATEGORIES = ["General", "Application", "KYC Documents", "Account", "Complaint"];

export default function Tickets() {
  const navigate = useNavigate();
  const [tickets, setTickets] = useState([]);
  const [apps, setApps] = useState([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ subject: "", category: "General", message: "", applicationId: "" });
  const [submitting, setSubmitting] = useState(false);
  const [justCreated, setJustCreated] = useState(false);

  const load = () => client.get("/tickets").then((r) => setTickets(r.data)).finally(() => setLoading(false));
  useEffect(() => {
    load();
    client.get("/applications").then((r) => setApps(r.data)).catch(() => {});
  }, []);

  const statusLabel = (s) => ({ Submitted: "Submitted", UnderReview: "Under review", ActionRequired: "Action required", Approved: "Approved", Rejected: "Rejected" }[s] || s);

  const submit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await client.post("/tickets", form);
      setForm({ subject: "", category: "General", message: "", applicationId: "" });
      setCreating(false);
      setJustCreated(true);
      await load();                 // refresh the list so the new ticket shows
      setTimeout(() => setJustCreated(false), 4000);
    } finally { setSubmitting(false); }
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in" style={{ maxWidth: 820 }}>
      <div className="row between" style={{ marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: "1.8rem" }}>Support</h1>
          <p className="muted">Raise a ticket and our team will help you out.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setCreating((c) => !c)}>
          {creating ? "Cancel" : "New ticket"}
        </button>
      </div>

      {justCreated && (
        <div className="alert alert-success" style={{ marginBottom: 16 }}>
          Ticket raised. You can raise another any time — one per product if you like.
        </div>
      )}

      {creating && (
        <form onSubmit={submit} className="card card-pad fade-in" style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 14 }}>Raise a new ticket</h3>
          <div className="grid" style={{ gridTemplateColumns: "2fr 1fr" }}>
            <div className="field">
              <label className="label">Subject</label>
              <input className="input" required value={form.subject} onChange={(e) => setForm({ ...form, subject: e.target.value })} placeholder="Brief summary of your issue" />
            </div>
            <div className="field">
              <label className="label">Category</label>
              <select className="select" value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
                {CATEGORIES.map((c) => <option key={c}>{c}</option>)}
              </select>
            </div>
          </div>
          <div className="field">
            <label className="label">Which application is this about?</label>
            <select className="select" value={form.applicationId} onChange={(e) => setForm({ ...form, applicationId: e.target.value })}>
              <option value="">General — not about a specific application</option>
              {apps.map((a) => (
                <option key={a.id} value={a.id}>{a.productName} — {statusLabel(a.status)}</option>
              ))}
            </select>
            <div className="help">Linking a ticket helps our team (and the AI assistant) see your exact situation.</div>
          </div>
          <div className="field">
            <label className="label">Describe your issue</label>
            <textarea className="textarea" required value={form.message} onChange={(e) => setForm({ ...form, message: e.target.value })} placeholder="Tell us what's going on…" />
          </div>
          <button className="btn btn-primary" disabled={submitting}>{submitting ? <Spinner /> : "Submit ticket"}</button>
        </form>
      )}

      {tickets.length === 0 ? (
        <EmptyState icon={<IconChat size={34} />} title="No tickets yet" text="Raise a ticket if you need help with your application or account." />
      ) : (
        <div className="card" style={{ overflow: "hidden" }}>
          <table className="table">
            <thead><tr><th>Subject</th><th>Category</th><th>Status</th><th>Updated</th><th></th></tr></thead>
            <tbody>
              {tickets.map((t) => (
                <tr key={t.id} onClick={() => navigate(`/tickets/${t.id}`)}>
                  <td style={{ fontWeight: 550 }}>{t.subject}</td>
                  <td className="muted">{t.category}</td>
                  <td><TicketStatusBadge status={t.status} /></td>
                  <td className="muted">{dateFmt(t.updatedAt)}</td>
                  <td style={{ textAlign: "right", color: "var(--text-muted)" }}><IconChevron size={16} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
