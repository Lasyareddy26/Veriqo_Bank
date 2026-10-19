import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import client from "../api/client";
import { useAuth } from "../context/AuthContext";
import { Spinner, money } from "../components/ui";
import { IconForProduct, IconCheck, IconChevron } from "../components/Icons";

export default function Products() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    client.get("/products").then((r) => setProducts(r.data)).finally(() => setLoading(false));
  }, []);

  const onApply = (key) => {
    if (!user.profileComplete) navigate("/profile");
    else navigate(`/apply/${key}`);
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in">
      <h1 style={{ fontSize: "1.8rem" }}>Banking products</h1>
      <p className="muted" style={{ marginBottom: 8 }}>Choose a product to apply. You’ll only upload documents you haven’t verified before.</p>

      {!user.profileComplete && (
        <div className="alert alert-warning" style={{ margin: "16px 0" }}>
          Complete your <Link to="/profile">profile</Link> first to apply for any product.
        </div>
      )}

      <div className="grid" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", marginTop: 24 }}>
        {products.map((p) => (
          <div key={p.key} className="card card-hover card-pad stack" style={{ justifyContent: "space-between" }}>
            <div>
              <div className="row between" style={{ marginBottom: 14 }}>
                <div style={{ width: 46, height: 46, borderRadius: 12, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)" }}>
                  {IconForProduct(p.icon, { size: 24 })}
                </div>
                <span className="badge badge-neutral">{p.category}</span>
              </div>
              <h3 style={{ marginBottom: 4 }}>{p.name}</h3>
              <p className="muted small" style={{ marginBottom: 16 }}>{p.tagline}</p>
              <div className="stack gap-2" style={{ marginBottom: 18 }}>
                {p.highlights.map((h) => (
                  <div key={h} className="row gap-2 small soft">
                    <IconCheck size={15} style={{ color: "var(--success)", flexShrink: 0 }} /> {h}
                  </div>
                ))}
              </div>
            </div>
            <div>
              <div className="divider" style={{ margin: "6px 0 14px" }} />
              <div className="row between">
                <div className="small muted">
                  {p.minAnnualIncome > 0 ? <>Min income <strong className="soft">{money(p.minAnnualIncome)}/yr</strong></> : "No income requirement"}
                </div>
                <button className="btn btn-primary btn-sm" onClick={() => onApply(p.key)}>
                  Apply <IconChevron size={15} />
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
