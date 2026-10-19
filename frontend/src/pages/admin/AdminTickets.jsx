import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import client from "../../api/client";
import { Spinner, TicketStatusBadge, dateFmt } from "../../components/ui";
import { IconChevron } from "../../components/Icons";

const FILTERS = ["", "Open", "PendingCustomer", "Resolved", "Closed"];
const LABEL = { "": "All", Open: "Open", PendingCustomer: "Awaiting customer", Resolved: "Resolved", Closed: "Closed" };

export default function AdminTickets() {
  const navigate = useNavigate();
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const q = status ? `?status=${status}` : "";
    client.get(`/admin/tickets${q}`).then((r) => setRows(r.data)).finally(() => setLoading(false));
  }, [status]);

  return (
    <div className="fade-in">
      <h1 style={{ fontSize: "1.8rem" }}>Support tickets</h1>
      <p className="muted" style={{ marginBottom: 20 }}>Respond to customer queries and complaints.</p>

      <div className="row gap-2" style={{ marginBottom: 14, flexWrap: "wrap" }}>
        {FILTERS.map((f) => (
          <button key={f} onClick={() => setStatus(f)} className={"btn btn-sm " + (status === f ? "btn-primary" : "btn-ghost")}>{LABEL[f]}</button>
        ))}
      </div>

      <div className="card" style={{ overflow: "hidden" }}>
        {loading ? (
          <div className="row" style={{ justifyContent: "center", padding: 50 }}><Spinner size={26} /></div>
        ) : rows.length === 0 ? (
          <div className="muted" style={{ padding: 40, textAlign: "center" }}>No tickets here.</div>
        ) : (
          <table className="table">
            <thead><tr><th>Subject</th><th>Customer</th><th>Category</th><th>Status</th><th>Replies</th><th>Updated</th><th></th></tr></thead>
            <tbody>
              {rows.map((t) => (
                <tr key={t.id} onClick={() => navigate(`/admin/tickets/${t.id}`)}>
                  <td style={{ fontWeight: 550 }}>{t.subject}</td>
                  <td className="muted">{t.customerEmail}</td>
                  <td className="muted">{t.category}</td>
                  <td><TicketStatusBadge status={t.status} /></td>
                  <td className="muted">{t.messages}</td>
                  <td className="muted">{dateFmt(t.updatedAt)}</td>
                  <td style={{ textAlign: "right", color: "var(--text-muted)" }}><IconChevron size={16} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
