import { useEffect, useState } from "react";
import { NavLink, useNavigate, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import client from "../api/client";
import {
  IconGrid, IconWallet, IconList, IconUser, IconBell, IconLogout,
  IconShield, IconChat,
} from "./Icons";
import { Avatar } from "./ui";

function Logo() {
  return (
    <Link to="/" className="row gap-2" style={{ textDecoration: "none" }}>
      <div style={{ width: 30, height: 30, borderRadius: 8, background: "var(--primary)", display: "grid", placeItems: "center", color: "#fff", fontFamily: "var(--font-serif)", fontWeight: 600, fontSize: 17 }}>I</div>
      <span style={{ fontFamily: "var(--font-serif)", fontSize: "1.15rem", fontWeight: 600, color: "var(--text)" }}>Veriqo</span>
    </Link>
  );
}

const customerNav = [
  { to: "/home", label: "Dashboard", icon: IconGrid, end: true },
  { to: "/products", label: "Products", icon: IconWallet },
  { to: "/applications", label: "My Applications", icon: IconList },
  { to: "/tickets", label: "Support", icon: IconChat },
  { to: "/profile", label: "Profile", icon: IconUser },
];

const adminNav = [
  { to: "/admin", label: "Review Queue", icon: IconShield, end: true },
  { to: "/admin/tickets", label: "Tickets", icon: IconChat },
  { to: "/admin/audit", label: "Audit Log", icon: IconList },
];

export default function Layout({ children }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [unread, setUnread] = useState(0);
  const [profile, setProfile] = useState(null);
  const isAdmin = user?.role === "Admin";
  const nav = isAdmin ? adminNav : customerNav;

  useEffect(() => {
    if (isAdmin) return;
    let active = true;
    const load = () =>
      client.get("/notifications").then((r) => {
        if (active) setUnread(r.data.filter((n) => !n.isRead).length);
      }).catch(() => {});
    load();
    client.get("/profile").then((r) => { if (active) setProfile(r.data); }).catch(() => {});
    const t = setInterval(load, 20000);
    return () => { active = false; clearInterval(t); };
  }, [isAdmin]);

  const doLogout = () => { logout(); navigate(isAdmin ? "/admin/login" : "/login"); };

  return (
    <div style={{ display: "flex", minHeight: "100vh" }}>
      <aside style={{ width: 244, background: var_surface(), borderRight: "1px solid var(--border)", padding: "22px 16px", position: "sticky", top: 0, height: "100vh", display: "flex", flexDirection: "column" }}>
        <div style={{ padding: "0 8px 20px" }}><Logo /></div>

        {isAdmin && (
          <div className="badge badge-primary" style={{ margin: "0 8px 14px", alignSelf: "flex-start" }}>Admin console</div>
        )}

        <nav className="stack gap-2" style={{ flex: 1 }}>
          {nav.map((n) => (
            <NavLink key={n.to} to={n.to} end={n.end}
              className={({ isActive }) => "nav-link" + (isActive ? " nav-active" : "")}
              style={({ isActive }) => ({
                display: "flex", alignItems: "center", gap: 11, padding: "9px 12px",
                borderRadius: 9, fontSize: "0.9rem", fontWeight: 500,
                color: isActive ? "var(--primary-hover)" : "var(--text-soft)",
                background: isActive ? "var(--primary-soft)" : "transparent",
                textDecoration: "none",
              })}>
              <n.icon size={18} /> {n.label}
            </NavLink>
          ))}
          {!isAdmin && (
            <NavLink to="/notifications"
              style={({ isActive }) => ({
                display: "flex", alignItems: "center", gap: 11, padding: "9px 12px",
                borderRadius: 9, fontSize: "0.9rem", fontWeight: 500,
                color: isActive ? "var(--primary-hover)" : "var(--text-soft)",
                background: isActive ? "var(--primary-soft)" : "transparent", textDecoration: "none",
              })}>
              <IconBell size={18} /> Notifications
              {unread > 0 && <span className="badge badge-error" style={{ marginLeft: "auto", padding: "1px 7px" }}>{unread}</span>}
            </NavLink>
          )}
        </nav>

        <div style={{ borderTop: "1px solid var(--border)", paddingTop: 14, marginTop: 10 }}>
          <div className="row gap-2" style={{ padding: "0 8px 10px" }}>
            <Avatar src={profile?.pictureUrl} name={profile?.fullName} email={user?.email} size={32} />
            <div style={{ overflow: "hidden" }}>
              <div className="small" style={{ fontWeight: 600, whiteSpace: "nowrap", textOverflow: "ellipsis", overflow: "hidden" }}>
                {profile?.fullName || user?.email}
              </div>
              <div className="small muted">{user?.role}</div>
            </div>
          </div>
          <button className="btn btn-ghost btn-sm btn-block" onClick={doLogout}>
            <IconLogout size={16} /> Sign out
          </button>
        </div>
      </aside>

      <main style={{ flex: 1, minWidth: 0 }}>
        <div className="container" style={{ padding: "36px 32px 60px" }}>{children}</div>
      </main>
    </div>
  );
}

function var_surface() { return "var(--surface)"; }
