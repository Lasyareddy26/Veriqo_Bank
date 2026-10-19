import { useCallback, useEffect, useState } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import client from "../../api/client";
import {
  Spinner, StatusBadge, RiskBadge, DocStatusBadge, money, dateFmt, dateTimeFmt, VerificationChecks,
} from "../../components/ui";
import {
  IconArrowLeft, IconFile, IconShield, IconAlert, IconCheck, IconDownload, IconUser,
} from "../../components/Icons";

const API = "http://localhost:5105/api";
const DOC_LABELS = {
  GovernmentId: "Government ID", AddressProof: "Address proof", SalarySlip: "Salary slip",
  BankStatement: "Bank statement", EmploymentLetter: "Employment letter",
};

function Row({ label, value }) {
  return (
    <div className="row between" style={{ padding: "7px 0", borderBottom: "1px solid var(--border)" }}>
      <span className="small muted">{label}</span>
      <span className="small soft" style={{ fontWeight: 500, textAlign: "right" }}>{value || "—"}</span>
    </div>
  );
}

function AdminDocCard({ doc, onReview }) {
  const [reason, setReason] = useState("");
  const [showReject, setShowReject] = useState(false);
  const [busy, setBusy] = useState(false);

  const view = () => {
    fetch(`${API}/documents/${doc.id}/file`, { headers: { Authorization: `Bearer ${localStorage.getItem("veriqo_token")}` } })
      .then((r) => r.blob()).then((b) => window.open(URL.createObjectURL(b), "_blank"));
  };
  const act = async (decision) => {
    setBusy(true);
    await onReview(doc.id, decision, decision === "Rejected" ? reason : "");
    setBusy(false); setShowReject(false); setReason("");
  };
  const entries = Object.entries(doc.extractedData || {}).filter(([k]) => !["ocrEngine", "ocrSucceeded"].includes(k));

  return (
    <div className="card" style={{ padding: 16 }}>
      <div className="row between">
        <div className="row gap-3">
          <div style={{ width: 38, height: 38, borderRadius: 9, background: "var(--surface-2)", border: "1px solid var(--border)", display: "grid", placeItems: "center" }}><IconFile size={18} /></div>
          <div>
            <div style={{ fontWeight: 600, fontSize: "0.9rem" }}>{DOC_LABELS[doc.type] || doc.type}</div>
            <div className="small muted">{doc.fileName} · {(doc.size / 1024).toFixed(0)} KB</div>
          </div>
        </div>
        <div className="row gap-2"><RiskBadge level={doc.riskLevel} /><DocStatusBadge status={doc.status} /></div>
      </div>

      <VerificationChecks data={doc.extractedData} />

      {doc.ocrPreview && (
        <details style={{ marginTop: 10 }}>
          <summary className="small muted" style={{ cursor: "pointer" }}>Raw OCR text</summary>
          <pre style={{ fontSize: "0.72rem", color: "var(--text-muted)", whiteSpace: "pre-wrap", marginTop: 6, maxHeight: 120, overflow: "auto" }}>{doc.ocrPreview}</pre>
        </details>
      )}

      <div className="divider" style={{ margin: "14px 0" }} />
      <div className="row between">
        <button className="btn btn-ghost btn-sm" onClick={view}><IconDownload size={15} /> View file</button>
        {doc.status === "Pending" ? (
          <div className="row gap-2">
            <button className="btn btn-danger btn-sm" disabled={busy} onClick={() => setShowReject((s) => !s)}>Reject</button>
            <button className="btn btn-success btn-sm" disabled={busy} onClick={() => act("Verified")}>{busy ? <Spinner size={14} /> : <><IconCheck size={15} /> Verify</>}</button>
          </div>
        ) : (
          <span className="small muted">Reviewed by {doc.reviewedBy} · {doc.reviewedAt ? dateFmt(doc.reviewedAt) : ""}</span>
        )}
      </div>
      {showReject && (
        <div className="stack gap-2" style={{ marginTop: 12 }}>
          <input className="input" placeholder="Reason for rejection (shown to customer)" value={reason} onChange={(e) => setReason(e.target.value)} />
          <button className="btn btn-danger btn-sm" disabled={!reason || busy} onClick={() => act("Rejected")} style={{ alignSelf: "flex-start" }}>Confirm rejection</button>
        </div>
      )}
    </div>
  );
}

export default function AdminApplicationDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState("");
  const [banner, setBanner] = useState(null); // { type, text }

  const load = useCallback(() => {
    client.get(`/admin/applications/${id}`).then((r) => setData(r.data)).finally(() => setLoading(false));
  }, [id]);
  useEffect(() => { load(); }, [load]);

  const reviewDoc = async (docId, decision, reason) => {
    await client.put(`/admin/documents/${docId}/review`, { decision, reason });
    load();
  };

  const decide = async (decision) => {
    if ((decision === "Rejected" || decision === "ActionRequired") && !note.trim()) {
      alert("Please add a note explaining the decision for the customer.");
      return;
    }
    setBusy(decision);
    try {
      const res = await client.put(`/admin/applications/${id}/decision`, { decision, note });
      const { emailSent, emailTo } = res.data || {};
      if (decision === "Approved") {
        setBanner(emailSent
          ? { type: "success", text: `Application approved — a confirmation email was sent to ${emailTo}.` }
          : { type: "info", text: "Application approved. Confirmation email was not sent (email isn’t configured on the server)." });
      } else {
        setBanner({ type: "success", text: `Decision saved — the customer was notified in-app.` });
      }
      window.scrollTo({ top: 0, behavior: "smooth" });
    } catch {
      setBanner({ type: "error", text: "Could not save the decision. Please try again." });
    } finally {
      setBusy(""); load();
    }
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;
  if (!data) return <div className="alert alert-error">Not found.</div>;

  const { application: app, profile, email, product, documents } = data;
  const answers = app.answers || {};
  const fieldLabel = (name) => product?.fields?.find((f) => f.name === name)?.label || name;

  return (
    <div className="fade-in" style={{ maxWidth: 1000 }}>
      <Link to="/admin" className="row gap-2 small muted" style={{ marginBottom: 18 }}><IconArrowLeft size={16} /> Back to queue</Link>

      {banner && (
        <div className={`alert alert-${banner.type} fade-in row between`} style={{ marginBottom: 18 }}>
          <span className="row gap-2">
            {banner.type === "success" && <IconCheck size={16} />}
            {banner.text}
          </span>
          <button onClick={() => setBanner(null)} aria-label="Dismiss"
            style={{ background: "none", border: "none", color: "inherit", cursor: "pointer", fontSize: 18, lineHeight: 1, opacity: 0.7 }}>×</button>
        </div>
      )}

      <div className="row between" style={{ marginBottom: 20 }}>
        <div>
          <div className="row gap-3" style={{ marginBottom: 4 }}>
            <h1 style={{ fontSize: "1.6rem" }}>{app.productName}</h1>
            <StatusBadge status={app.status} />
            <RiskBadge level={app.riskLevel} />
          </div>
          <div className="small muted mono">Ref #{app.id.slice(-8).toUpperCase()} · {dateTimeFmt(app.createdAt)}</div>
        </div>
      </div>

      <div className="grid" style={{ gridTemplateColumns: "1.5fr 1fr", alignItems: "start" }}>
        {/* Left: documents + checks */}
        <div className="stack gap-4">
          {app.riskFlags?.length > 0 && (
            <div className="card card-pad">
              <div className="row gap-2" style={{ marginBottom: 12 }}><IconShield size={18} style={{ color: "var(--primary-hover)" }} /><h3>Automated checks · score {app.riskScore}/100</h3></div>
              <div className="stack gap-2">
                {app.riskFlags.map((f, i) => (
                  <div key={i} className="row gap-2 small soft"><IconAlert size={14} style={{ color: "var(--warning)", flexShrink: 0, marginTop: 2 }} />{f}</div>
                ))}
              </div>
            </div>
          )}

          <div>
            <h3 style={{ marginBottom: 12 }}>Documents ({documents.length})</h3>
            {documents.length === 0 ? (
              <div className="card card-pad muted">No documents uploaded yet.</div>
            ) : (
              <div className="stack gap-3">
                {documents.map((d) => <AdminDocCard key={d.id} doc={d} onReview={reviewDoc} />)}
              </div>
            )}
          </div>
        </div>

        {/* Right: customer + answers + decision */}
        <div className="stack gap-4">
          <div className="card card-pad">
            <div className="row gap-2" style={{ marginBottom: 12 }}><IconUser size={17} className="muted" /><h3>Customer</h3></div>
            <Row label="Name" value={profile?.fullName} />
            <Row label="Email" value={email} />
            <Row label="Date of birth" value={profile?.dateOfBirth ? dateFmt(profile.dateOfBirth) : "—"} />
            <Row label="Nationality" value={profile?.nationality} />
            <Row label="Govt ID" value={profile?.governmentIdNumber} />
            <Row label="Phone" value={profile?.phone} />
            <Row label="Address" value={profile?.address ? `${profile.address.city}, ${profile.address.state}` : "—"} />
            <Row label="Employment" value={profile?.employmentStatus} />
            <Row label="Annual income" value={profile ? money(profile.annualIncome) : "—"} />
          </div>

          <div className="card card-pad">
            <h3 style={{ marginBottom: 12 }}>Application answers</h3>
            {Object.entries(answers).map(([k, v]) => <Row key={k} label={fieldLabel(k)} value={String(v)} />)}
          </div>

          <div className="card card-pad" style={{ position: "sticky", top: 20 }}>
            <h3 style={{ marginBottom: 6 }}>Decision</h3>
            <p className="muted small" style={{ marginBottom: 12 }}>A note is required for rejection or action-needed.</p>
            <textarea className="textarea" placeholder="Note to customer…" value={note} onChange={(e) => setNote(e.target.value)} style={{ minHeight: 70, marginBottom: 12 }} />
            <div className="stack gap-2">
              <button className="btn btn-success btn-block" disabled={!!busy} onClick={() => decide("Approved")}>{busy === "Approved" ? <Spinner /> : "Approve application"}</button>
              <div className="row gap-2">
                <button className="btn btn-ghost btn-block" disabled={!!busy} onClick={() => decide("UnderReview")}>{busy === "UnderReview" ? <Spinner /> : "Mark reviewing"}</button>
                <button className="btn btn-soft btn-block" disabled={!!busy} onClick={() => decide("ActionRequired")}>{busy === "ActionRequired" ? <Spinner /> : "Request info"}</button>
              </div>
              <button className="btn btn-danger btn-block" disabled={!!busy} onClick={() => decide("Rejected")}>{busy === "Rejected" ? <Spinner /> : "Reject application"}</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
