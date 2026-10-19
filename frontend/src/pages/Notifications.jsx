import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import client from "../api/client";
import { Spinner, EmptyState, dateTimeFmt } from "../components/ui";
import { IconBell, IconCheck } from "../components/Icons";

const TYPE_COLOR = {
  success: "var(--success)", warning: "var(--warning)", error: "var(--error)", info: "var(--info)",
};

export default function Notifications() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  const load = () => client.get("/notifications").then((r) => setItems(r.data)).finally(() => setLoading(false));
  useEffect(() => { load(); }, []);

  const markAll = async () => { await client.put("/notifications/read-all"); load(); };
  const markOne = async (id) => { await client.put(`/notifications/${id}/read`); load(); };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in" style={{ maxWidth: 720 }}>
      <div className="row between" style={{ marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: "1.8rem" }}>Notifications</h1>
          <p className="muted">Updates on your applications and documents.</p>
        </div>
        {items.some((n) => !n.isRead) && <button className="btn btn-ghost btn-sm" onClick={markAll}><IconCheck size={15} /> Mark all read</button>}
      </div>

      {items.length === 0 ? (
        <EmptyState icon={<IconBell size={34} />} title="No notifications" text="You're all caught up. We'll let you know when something changes." />
      ) : (
        <div className="stack gap-3">
          {items.map((n) => (
            <div key={n.id} className="card" style={{ padding: 16, borderLeft: `3px solid ${TYPE_COLOR[n.type] || "var(--border)"}`, background: n.isRead ? "var(--surface)" : "var(--surface-2)" }}
              onClick={() => !n.isRead && markOne(n.id)}>
              <div className="row between">
                <div style={{ fontWeight: 600, fontSize: "0.95rem" }}>{n.title}</div>
                <div className="small muted">{dateTimeFmt(n.createdAt)}</div>
              </div>
              <p className="soft small" style={{ marginTop: 4 }}>{n.message}</p>
              {n.link && <Link to={n.link} className="small" style={{ marginTop: 6, display: "inline-block" }}>View application →</Link>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
