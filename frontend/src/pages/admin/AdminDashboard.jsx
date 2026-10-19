import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import client from "../../api/client";
import { Spinner, StatusBadge, RiskBadge, dateFmt } from "../../components/ui";
import { IconChevron, IconUsers, IconList, IconAlert, IconCheck } from "../../components/Icons";

const STATUS_FILTERS = ["", "Submitted", "UnderReview", "ActionRequired", "Approved", "Rejected"];
const FILTER_LABEL = { "": "All", Submitted: "Submitted", UnderReview: "Under review", ActionRequired: "Action required", Approved: "Approved", Rejected: "Rejected" };

export default function AdminDashboard() {
  const navigate = useNavigate();
  const [stats, setStats] = useState(null);
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState("");
  const [risk, setRisk] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => { client.get("/admin/stats").then((r) => setStats(r.data)); }, []);

  useEffect(() => {
    setLoading(true);
    const q = new URLSearchParams();
    if (status) q.set("status", status);
    if (risk) q.set("risk", risk);
    client.get(`/admin/applications?${q}`).then((r) => setRows(r.data)).finally(() => setLoading(false));
  }, [status, risk]);

  const statCards = stats ? [
    { label: "Total applications", value: stats.total, icon: IconList },
    { label: "Awaiting review", value: stats.submitted + stats.underReview, icon: IconAlert, accent: true },
    { label: "High risk", value: stats.highRisk, icon: IconAlert },
    { label: "Approved", value: stats.approved, icon: IconCheck },
    { label: "Customers", value: stats.customers, icon: IconUsers },
  ] : [];

  return (
    <div className="fade-in">
      <h1 style={{ fontSize: "1.8rem" }}>Review queue</h1>
      <p className="muted" style={{ marginBottom: 24 }}>Review applications, verify documents and make decisions.</p>

      <div className="grid" style={{ gridTemplateColumns: "repeat(5, 1fr)", marginBottom: 28 }}>
        {statCards.map((s) => (
          <div key={s.label} className="card card-pad" style={s.accent ? { borderColor: "var(--primary)" } : {}}>
            <div className="muted small" style={{ marginBottom: 6 }}>{s.label}</div>
            <div className="row between">
              <div style={{ fontSize: "1.7rem", fontFamily: "var(--font-serif)", fontWeight: 600 }}>{s.value}</div>
              <div style={{ color: s.accent ? "var(--primary-hover)" : "var(--text-muted)" }}><s.icon size={19} /></div>
            </div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="row between" style={{ marginBottom: 14, flexWrap: "wrap", gap: 12 }}>
        <div className="row gap-2" style={{ flexWrap: "wrap" }}>
          {STATUS_FILTERS.map((s) => (
            <button key={s} onClick={() => setStatus(s)}
              className={"btn btn-sm " + (status === s ? "btn-primary" : "btn-ghost")}>
              {FILTER_LABEL[s]}
            </button>
          ))}
        </div>
        <select className="select" style={{ width: 160 }} value={risk} onChange={(e) => setRisk(e.target.value)}>
          <option value="">All risk levels</option>
          <option value="High">High risk</option>
          <option value="Medium">Medium risk</option>
          <option value="Low">Low risk</option>
        </select>
      </div>

      <div className="card" style={{ overflow: "hidden" }}>
        {loading ? (
          <div className="row" style={{ justifyContent: "center", padding: 50 }}><Spinner size={26} /></div>
        ) : rows.length === 0 ? (
          <div className="muted" style={{ padding: 40, textAlign: "center" }}>No applications match these filters.</div>
        ) : (
          <table className="table">
            <thead>
              <tr><th>Customer</th><th>Product</th><th>Status</th><th>Risk</th><th>Flags</th><th>Submitted</th><th></th></tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} onClick={() => navigate(`/admin/applications/${r.id}`)}>
                  <td>
                    <div style={{ fontWeight: 550 }}>{r.customerName}</div>
                    <div className="small muted">{r.customerEmail}</div>
                  </td>
                  <td>{r.productName}</td>
                  <td><StatusBadge status={r.status} /></td>
                  <td><RiskBadge level={r.riskLevel} /></td>
                  <td className="muted">{r.flagCount > 0 ? `${r.flagCount} flag${r.flagCount > 1 ? "s" : ""}` : "—"}</td>
                  <td className="muted">{dateFmt(r.createdAt)}</td>
                  <td style={{ textAlign: "right", color: "var(--text-muted)" }}><IconChevron size={17} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
