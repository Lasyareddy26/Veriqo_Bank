import { useEffect, useState } from "react";
import client from "../../api/client";
import { Spinner, dateTimeFmt } from "../../components/ui";

export default function AdminAudit() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    client.get("/admin/audit").then((r) => setLogs(r.data)).finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in">
      <h1 style={{ fontSize: "1.8rem" }}>Audit log</h1>
      <p className="muted" style={{ marginBottom: 24 }}>Every review decision is recorded here for compliance.</p>

      <div className="card" style={{ overflow: "hidden" }}>
        {logs.length === 0 ? (
          <div className="muted" style={{ padding: 40, textAlign: "center" }}>No activity recorded yet.</div>
        ) : (
          <table className="table">
            <thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Entity</th><th>Details</th></tr></thead>
            <tbody>
              {logs.map((l) => (
                <tr key={l.id} style={{ cursor: "default" }}>
                  <td className="muted small">{dateTimeFmt(l.at)}</td>
                  <td className="small" style={{ fontWeight: 550 }}>{l.actorEmail}</td>
                  <td className="small">{l.action}</td>
                  <td className="small muted">{l.entityType} #{l.entityId.slice(-6)}</td>
                  <td className="small muted">{l.details || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
