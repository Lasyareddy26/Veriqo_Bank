import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import ProtectedRoute from "./components/ProtectedRoute";
import Layout from "./components/Layout";

import Login from "./pages/Login";
import AdminLogin from "./pages/AdminLogin";
import Register from "./pages/Register";
import Dashboard from "./pages/Dashboard";
import Profile from "./pages/Profile";
import Products from "./pages/Products";
import Apply from "./pages/Apply";
import Applications from "./pages/Applications";
import ApplicationDetail from "./pages/ApplicationDetail";
import Notifications from "./pages/Notifications";
import Tickets from "./pages/Tickets";
import TicketDetail from "./pages/TicketDetail";
import AdminDashboard from "./pages/admin/AdminDashboard";
import AdminApplicationDetail from "./pages/admin/AdminApplicationDetail";
import AdminAudit from "./pages/admin/AdminAudit";
import AdminTickets from "./pages/admin/AdminTickets";
import AdminTicketDetail from "./pages/admin/AdminTicketDetail";

function CustomerShell({ children }) {
  return <ProtectedRoute role="Customer"><Layout>{children}</Layout></ProtectedRoute>;
}
function AdminShell({ children }) {
  return <ProtectedRoute role="Admin"><Layout>{children}</Layout></ProtectedRoute>;
}

function RootRedirect() {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Navigate to={user.role === "Admin" ? "/admin" : "/home"} replace />;
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/register" element={<Register />} />

          {/* Customer */}
          <Route path="/" element={<RootRedirect />} />
          <Route path="/home" element={<CustomerShell><Dashboard /></CustomerShell>} />
          <Route path="/profile" element={<CustomerShell><Profile /></CustomerShell>} />
          <Route path="/products" element={<CustomerShell><Products /></CustomerShell>} />
          <Route path="/apply/:key" element={<CustomerShell><Apply /></CustomerShell>} />
          <Route path="/applications" element={<CustomerShell><Applications /></CustomerShell>} />
          <Route path="/applications/:id" element={<CustomerShell><ApplicationDetail /></CustomerShell>} />
          <Route path="/notifications" element={<CustomerShell><Notifications /></CustomerShell>} />
          <Route path="/tickets" element={<CustomerShell><Tickets /></CustomerShell>} />
          <Route path="/tickets/:id" element={<CustomerShell><TicketDetail /></CustomerShell>} />

          {/* Admin */}
          <Route path="/admin" element={<AdminShell><AdminDashboard /></AdminShell>} />
          <Route path="/admin/applications/:id" element={<AdminShell><AdminApplicationDetail /></AdminShell>} />
          <Route path="/admin/tickets" element={<AdminShell><AdminTickets /></AdminShell>} />
          <Route path="/admin/tickets/:id" element={<AdminShell><AdminTicketDetail /></AdminShell>} />
          <Route path="/admin/audit" element={<AdminShell><AdminAudit /></AdminShell>} />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
