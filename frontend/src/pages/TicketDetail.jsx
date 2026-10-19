import { useCallback, useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import client from "../api/client";
import { Spinner, TicketStatusBadge, dateTimeFmt, BotAvatar } from "../components/ui";
import { IconArrowLeft, IconSend, IconUser } from "../components/Icons";

export function Thread({ messages, viewer = "customer" }) {
  return (
    <div className="stack gap-4">
      {messages.map((m, i) => {
        const agent = m.sender === "Agent";
        const mine = viewer === "customer" ? !agent : agent; // the viewer's own messages sit on the right
        const label = mine ? "You" : agent ? "Support assistant" : "Customer";
        return (
          <div key={i} className="row" style={{ gap: 12, alignItems: "flex-end", flexDirection: mine ? "row-reverse" : "row" }}>
            {agent ? (
              <BotAvatar size={40} />
            ) : (
              <div style={{ width: 40, height: 40, borderRadius: "50%", background: "var(--bg-alt)", color: "var(--text-soft)", display: "grid", placeItems: "center", flexShrink: 0 }}>
                <IconUser size={20} />
              </div>
            )}
            <div style={{
              maxWidth: "68%", minWidth: 0,
              background: mine ? "var(--primary-soft)" : "var(--surface-2)",
              border: "1px solid var(--border)", borderRadius: 16,
              borderBottomRightRadius: mine ? 4 : 16, borderBottomLeftRadius: mine ? 16 : 4,
              padding: "12px 16px",
            }}>
              <div className="row gap-2" style={{ marginBottom: 4 }}>
                <span className="small" style={{ fontWeight: 600, color: mine ? "var(--primary-hover)" : agent ? "var(--info)" : "var(--text)" }}>{label}</span>
                <span className="small muted">· {dateTimeFmt(m.at)}</span>
              </div>
              <div style={{ fontSize: "0.92rem", whiteSpace: "pre-wrap", lineHeight: 1.55 }}>{m.body}</div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default function TicketDetail() {
  const { id } = useParams();
  const [t, setT] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reply, setReply] = useState("");
  const [sending, setSending] = useState(false);

  const load = useCallback(() => {
    client.get(`/tickets/${id}`).then((r) => setT(r.data)).finally(() => setLoading(false));
  }, [id]);
  useEffect(() => { load(); }, [load]);

  const send = async (e) => {
    e.preventDefault();
    if (!reply.trim()) return;
    setSending(true);
    try {
      await client.post(`/tickets/${id}/messages`, { body: reply });
      setReply(""); load();
    } finally { setSending(false); }
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;
  if (!t) return <div className="alert alert-error">Ticket not found.</div>;
  const closed = t.status === "Closed";

  return (
    <div className="fade-in">
      <Link to="/tickets" className="row gap-2 small muted" style={{ marginBottom: 18 }}><IconArrowLeft size={16} /> All tickets</Link>

      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <div className="row between">
          <div>
            <h1 style={{ fontSize: "1.5rem" }}>{t.subject}</h1>
            <div className="small muted">{t.category} · opened {dateTimeFmt(t.createdAt)}</div>
          </div>
          <TicketStatusBadge status={t.status} />
        </div>
      </div>

      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <Thread messages={t.messages} />
      </div>

      {closed ? (
        <div className="alert alert-info">This ticket is closed. Raise a new one if you still need help.</div>
      ) : (
        <form onSubmit={send} className="card card-pad">
          <label className="label">Add a reply</label>
          <textarea className="textarea" value={reply} onChange={(e) => setReply(e.target.value)} placeholder="Type your message…" style={{ marginBottom: 12 }} />
          <button className="btn btn-primary" disabled={sending || !reply.trim()}>{sending ? <Spinner /> : <><IconSend size={15} /> Send</>}</button>
        </form>
      )}
    </div>
  );
}
