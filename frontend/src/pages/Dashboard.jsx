import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import client from "../api/client";
import { useAuth } from "../context/AuthContext";
import { Spinner, StatusBadge, Avatar, dateFmt } from "../components/ui";
import { IconUser, IconWallet, IconList, IconChevron, IconCheck } from "../components/Icons";

export default function Dashboard() {
  const { user } = useAuth();
  const [apps, setApps] = useState([]);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      client.get("/applications").then((r) => setApps(r.data)),
      client.get("/profile").then((r) => setProfile(r.data)).catch(() => {}),
    ]).finally(() => setLoading(false));
  }, []);

  const firstName = profile?.fullName?.trim()?.split(" ")[0];

  const counts = {
    total: apps.length,
    active: apps.filter((a) => ["Submitted", "UnderReview", "ActionRequired"].includes(a.status)).length,
    approved: apps.filter((a) => a.status === "Approved").length,
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;

  return (
    <div className="fade-in">
      <div className="row gap-3" style={{ marginBottom: 24 }}>
        <Avatar src={profile?.pictureUrl} name={profile?.fullName} email={user?.email} size={52} />
        <div>
          <h1 style={{ fontSize: "1.9rem" }}>Welcome back{firstName ? `, ${firstName}` : ""}</h1>
          <p className="muted">{user?.email}</p>
        </div>
      </div>

      {!user?.profileComplete && (
        <div className="card card-pad" style={{ marginBottom: 24, background: "linear-gradient(120deg, var(--primary-soft), var(--surface))", borderColor: "var(--primary)" }}>
          <div className="row between">
            <div className="row gap-3">
              <div style={{ width: 42, height: 42, borderRadius: 11, background: "var(--surface)", display: "grid", placeItems: "center", color: "var(--primary-hover)" }}><IconUser /></div>
              <div>
                <h3 style={{ marginBottom: 2 }}>Complete your profile</h3>
                <p className="muted small">You need a complete profile before applying for products.</p>
              </div>
            </div>
            <Link to="/profile" className="btn btn-primary">Complete now</Link>
          </div>
        </div>
      )}

      {/* Stats */}
      <div className="grid" style={{ gridTemplateColumns: "repeat(3, 1fr)", marginBottom: 28 }}>
        {[
          { label: "Total applications", value: counts.total, icon: IconList },
          { label: "In progress", value: counts.active, icon: IconWallet },
          { label: "Approved", value: counts.approved, icon: IconCheck },
        ].map((s) => (
          <div key={s.label} className="card card-pad">
            <div className="row between">
              <div>
                <div className="muted small" style={{ marginBottom: 6 }}>{s.label}</div>
                <div style={{ fontSize: "2rem", fontFamily: "var(--font-serif)", fontWeight: 600 }}>{s.value}</div>
              </div>
              <div style={{ width: 40, height: 40, borderRadius: 11, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)" }}><s.icon size={20} /></div>
            </div>
          </div>
        ))}
      </div>

      {/* Recent applications — full width */}
      <div className="card card-pad">
        <div className="row between" style={{ marginBottom: 16 }}>
          <h3>Recent applications</h3>
          <div className="row gap-3">
            {apps.length > 0 && <Link to="/applications" className="small">View all</Link>}
            <Link to="/products" className="btn btn-primary btn-sm">Browse products</Link>
          </div>
        </div>
        {apps.length === 0 ? (
          <p className="muted small">No applications yet. <Link to="/products">Browse products →</Link></p>
        ) : (
          <div className="stack gap-2">
            {apps.slice(0, 6).map((a) => (
              <Link key={a.id} to={`/applications/${a.id}`} className="row between"
                style={{ padding: "14px 16px", borderRadius: 10, textDecoration: "none", color: "inherit", border: "1px solid var(--border)" }}>
                <div className="row gap-3" style={{ minWidth: 0 }}>
                  <div style={{ width: 38, height: 38, borderRadius: 9, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)", flexShrink: 0 }}>
                    <IconList size={18} />
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontWeight: 550, fontSize: "0.95rem" }}>{a.productName}</div>
                    <div className="small muted">Submitted {dateFmt(a.createdAt)}</div>
                  </div>
                </div>
                <div className="row gap-4">
                  <StatusBadge status={a.status} />
                  <IconChevron size={17} className="muted" />
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
