import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import client from "../api/client";
import { Spinner, StatusBadge, EmptyState, dateFmt } from "../components/ui";
import { IconList, IconChevron } from "../components/Icons";

export default function Applications() {
  const navigate = useNavigate();
  const [apps, setApps] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    client.get("/applications").then((r) => setApps(r.data)).finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in">
      <div className="row between" style={{ marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: "1.8rem" }}>My applications</h1>
          <p className="muted">Track every application and its documents in one place.</p>
        </div>
        <Link to="/products" className="btn btn-primary">New application</Link>
      </div>

      {apps.length === 0 ? (
        <EmptyState icon={<IconList size={34} />} title="No applications yet"
          text="Browse our products and submit your first application. It only takes a few minutes."
          action={<Link to="/products" className="btn btn-primary">Browse products</Link>} />
      ) : (
        <div className="card" style={{ overflow: "hidden" }}>
          <table className="table">
            <thead>
              <tr><th>Product</th><th>Status</th><th>Submitted</th><th>Reference</th><th></th></tr>
            </thead>
            <tbody>
              {apps.map((a) => (
                <tr key={a.id} onClick={() => navigate(`/applications/${a.id}`)}>
                  <td style={{ fontWeight: 550 }}>{a.productName}</td>
                  <td><StatusBadge status={a.status} /></td>
                  <td className="muted">{dateFmt(a.createdAt)}</td>
                  <td className="mono muted small">#{a.id.slice(-8)}</td>
                  <td style={{ textAlign: "right", color: "var(--text-muted)" }}><IconChevron size={17} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
