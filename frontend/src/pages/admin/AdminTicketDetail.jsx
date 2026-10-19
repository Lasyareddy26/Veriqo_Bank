import { useCallback, useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import client from "../../api/client";
import { Spinner, TicketStatusBadge, dateTimeFmt } from "../../components/ui";
import { Thread } from "../TicketDetail";
import { IconArrowLeft, IconSend, IconSpark, IconCheck } from "../../components/Icons";

function SentimentBadge({ s }) {
  if (!s) return null;
  const map = { Negative: "badge-error", Positive: "badge-success", Neutral: "badge-neutral" };
  return <span className={`badge ${map[s.label] || "badge-neutral"}`}>{s.label} · {s.score > 0 ? "+" : ""}{s.score}</span>;
}
function UrgencyBadge({ u }) {
  if (!u) return null;
  const map = { Urgent: "badge-error", High: "badge-warning", Normal: "badge-neutral", Low: "badge-neutral" };
  return <span className={`badge ${map[u] || "badge-neutral"}`}>{u} urgency</span>;
}

function AiPanel({ ticketId, initial, onUseReply }) {
  const [analysis, setAnalysis] = useState(initial || null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");

  const run = async () => {
    setBusy(true); setErr("");
    try {
      const { data } = await client.post(`/admin/tickets/${ticketId}/analyze`);
      setAnalysis(data);
    } catch (e) {
      setErr(e.response?.data?.message || "Analysis failed.");
    } finally { setBusy(false); }
  };

  return (
    <div className="card card-pad" style={{ marginBottom: 20, borderColor: "var(--primary)", background: "linear-gradient(180deg, var(--primary-soft) 0%, var(--surface) 42%)" }}>
      <div className="row between" style={{ marginBottom: analysis ? 16 : 4 }}>
        <div className="row gap-2">
          <div style={{ width: 30, height: 30, borderRadius: 8, background: "var(--primary)", display: "grid", placeItems: "center", color: "#fff" }}><IconSpark size={18} /></div>
          <h3>AI Assistant</h3>
          {analysis?.provider && <span className="badge badge-neutral small">{analysis.provider}</span>}
        </div>
        <button className="btn btn-primary btn-sm" onClick={run} disabled={busy}>
          {busy ? <Spinner size={15} /> : <><IconSpark size={15} /> {analysis ? "Re-analyze" : "Analyze with AI"}</>}
        </button>
      </div>

      {!analysis && !busy && (
        <p className="muted small">Summarize the ticket, gauge sentiment, draft a grounded reply, and get next-best actions — one click.</p>
      )}
      {err && <div className="alert alert-error small" style={{ marginTop: 10 }}>{err}</div>}

      {analysis && (
        <div className="stack gap-4 fade-in">
          {analysis.pipeline?.length > 0 && (
            <div className="row gap-2" style={{ flexWrap: "wrap", fontSize: "0.72rem" }}>
              {analysis.pipeline.map((p, i) => (
                <span key={i} className="row gap-2" style={{ color: "var(--text-muted)" }}>
                  {i > 0 && <span style={{ opacity: 0.5 }}>→</span>}
                  <span style={{ padding: "2px 8px", background: "var(--surface)", border: "1px solid var(--border)", borderRadius: 999 }}>{p}</span>
                </span>
              ))}
            </div>
          )}
          <div className="row gap-2" style={{ flexWrap: "wrap" }}>
            <SentimentBadge s={analysis.sentiment} />
            <UrgencyBadge u={analysis.sentiment?.urgency} />
            {analysis.intent && <span className="badge badge-primary">intent: {analysis.intent}</span>}
            {analysis.entities?.map((e, i) => <span key={i} className="badge badge-neutral">{e}</span>)}
          </div>

          <div>
            <div className="small muted" style={{ marginBottom: 4, fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>Summary</div>
            <p style={{ fontSize: "0.92rem" }}>{analysis.summary}</p>
          </div>

          <div>
            <div className="row between" style={{ marginBottom: 6 }}>
              <div className="small muted" style={{ fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>Suggested reply</div>
              <button className="btn btn-soft btn-sm" onClick={() => onUseReply(analysis.suggestedReply)}><IconCheck size={14} /> Use this reply</button>
            </div>
            <div className="card" style={{ background: "var(--surface-2)", padding: 14, fontSize: "0.9rem", whiteSpace: "pre-wrap", lineHeight: 1.55 }}>{analysis.suggestedReply}</div>
          </div>

          {analysis.nextBestActions?.length > 0 && (
            <div>
              <div className="small muted" style={{ marginBottom: 8, fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>Next-best actions</div>
              <div className="stack gap-2">
                {analysis.nextBestActions.map((a, i) => (
                  <div key={i} className="row gap-2 small" style={{ padding: "8px 12px", background: "var(--surface)", border: "1px solid var(--border)", borderRadius: 8 }}>
                    <span style={{ width: 20, height: 20, borderRadius: "50%", background: "var(--primary-soft)", color: "var(--primary-hover)", display: "grid", placeItems: "center", fontSize: 11, fontWeight: 700, flexShrink: 0 }}>{i + 1}</span>
                    {a}
                  </div>
                ))}
              </div>
            </div>
          )}
          {analysis.sources?.length > 0 && (
            <div>
              <div className="small muted" style={{ marginBottom: 8, fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>Sources (RAG) — policies used to ground the reply</div>
              <div className="row gap-2" style={{ flexWrap: "wrap" }}>
                {analysis.sources.map((src, i) => (
                  <span key={i} className="badge badge-info" style={{ fontWeight: 500 }}>📄 {src}</span>
                ))}
              </div>
            </div>
          )}
          <div className="help">AI suggestions — review and edit before sending. Generated {analysis.generatedAt ? dateTimeFmt(analysis.generatedAt) : "just now"}.</div>
        </div>
      )}
    </div>
  );
}

export default function AdminTicketDetail() {
  const { id } = useParams();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reply, setReply] = useState("");
  const [nextStatus, setNextStatus] = useState("PendingCustomer");
  const [sending, setSending] = useState(false);

  const load = useCallback(() => {
    client.get(`/admin/tickets/${id}`).then((r) => setData(r.data)).finally(() => setLoading(false));
  }, [id]);
  useEffect(() => { load(); }, [load]);

  const send = async (e) => {
    e.preventDefault();
    if (!reply.trim()) return;
    setSending(true);
    try {
      await client.post(`/admin/tickets/${id}/reply`, { body: reply, status: nextStatus });
      setReply(""); load();
    } finally { setSending(false); }
  };

  const setStatus = async (status) => {
    await client.put(`/admin/tickets/${id}/status`, { status });
    load();
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;
  if (!data) return <div className="alert alert-error">Not found.</div>;
  const { ticket: t, customerEmail } = data;

  return (
    <div className="fade-in" style={{ maxWidth: 940 }}>
      <Link to="/admin/tickets" className="row gap-2 small muted" style={{ marginBottom: 18 }}><IconArrowLeft size={16} /> All tickets</Link>

      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <div className="row between">
          <div>
            <h1 style={{ fontSize: "1.5rem" }}>{t.subject}</h1>
            <div className="small muted">{customerEmail} · {t.category} · opened {dateTimeFmt(t.createdAt)}</div>
          </div>
          <div className="row gap-2">
            <TicketStatusBadge status={t.status} />
            {t.status !== "Closed" && <button className="btn btn-ghost btn-sm" onClick={() => setStatus("Closed")}>Close</button>}
            {t.status !== "Resolved" && t.status !== "Closed" && <button className="btn btn-ghost btn-sm" onClick={() => setStatus("Resolved")}>Resolve</button>}
          </div>
        </div>
      </div>

      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <Thread messages={t.messages} viewer="agent" />
      </div>

      <AiPanel ticketId={id} initial={t.lastAiAnalysis} onUseReply={(txt) => { setReply(txt); window.scrollTo({ top: document.body.scrollHeight, behavior: "smooth" }); }} />

      {t.status !== "Closed" && (
        <form onSubmit={send} className="card card-pad">
          <label className="label">Reply to customer</label>
          <textarea className="textarea" value={reply} onChange={(e) => setReply(e.target.value)} placeholder="Type your response…" style={{ marginBottom: 12, minHeight: 130 }} />
          <div className="row between">
            <select className="select" style={{ width: 200 }} value={nextStatus} onChange={(e) => setNextStatus(e.target.value)}>
              <option value="PendingCustomer">Set: Awaiting customer</option>
              <option value="Open">Set: Open</option>
              <option value="Resolved">Set: Resolved</option>
            </select>
            <button className="btn btn-primary" disabled={sending || !reply.trim()}>{sending ? <Spinner /> : <><IconSend size={15} /> Send reply</>}</button>
          </div>
        </form>
      )}
    </div>
  );
}
