import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useSearchParams, Link } from "react-router-dom";
import client from "../api/client";
import {
  Spinner, StatusBadge, RiskBadge, DocStatusBadge, dateTimeFmt, VerificationChecks,
} from "../components/ui";
import {
  IconArrowLeft, IconUpload, IconFile, IconCheck, IconAlert,
  IconShield, IconClock, IconDownload,
} from "../components/Icons";

const API = "http://localhost:5105/api";

const STEPS = ["Submitted", "UnderReview", "Approved"];
const STEP_LABEL = { Submitted: "Submitted", UnderReview: "Under review", Approved: "Decision" };

function Timeline({ status, history }) {
  const rejected = status === "Rejected";
  const actionReq = status === "ActionRequired";
  const idx = status === "Approved" ? 2 : status === "UnderReview" || actionReq ? 1 : 0;

  return (
    <div className="row" style={{ gap: 0, marginBottom: 6 }}>
      {STEPS.map((s, i) => {
        const done = i <= idx;
        const isLast = i === STEPS.length - 1;
        const color = rejected && isLast ? "var(--error)" : done ? "var(--primary)" : "var(--border-strong)";
        return (
          <div key={s} className="row" style={{ flex: isLast ? "0 0 auto" : 1 }}>
            <div className="stack" style={{ alignItems: "center", gap: 6 }}>
              <div style={{ width: 30, height: 30, borderRadius: "50%", background: done ? color : "var(--surface)", border: `2px solid ${color}`, display: "grid", placeItems: "center", color: done ? "#fff" : "var(--text-muted)" }}>
                {rejected && isLast ? <IconAlert size={15} /> : done ? <IconCheck size={15} /> : i + 1}
              </div>
              <span className="small" style={{ color: done ? "var(--text)" : "var(--text-muted)", fontWeight: 500 }}>
                {rejected && isLast ? "Rejected" : STEP_LABEL[s]}
              </span>
            </div>
            {!isLast && <div style={{ flex: 1, height: 2, background: i < idx ? "var(--primary)" : "var(--border-strong)", margin: "0 8px", marginBottom: 22 }} />}
          </div>
        );
      })}
    </div>
  );
}

function ExtractedData({ data }) {
  const entries = Object.entries(data || {}).filter(([k]) => !["ocrEngine", "ocrSucceeded"].includes(k));
  if (entries.length === 0) return null;
  const pretty = (k) => k.replace(/([A-Z])/g, " $1").replace(/^./, (c) => c.toUpperCase());
  const fmt = (v) => (typeof v === "boolean" ? (v ? "Yes" : "No") : Array.isArray(v) ? v.join(", ") : String(v));
  return (
    <div className="grid" style={{ gridTemplateColumns: "1fr 1fr", gap: "6px 18px", marginTop: 10 }}>
      {entries.map(([k, v]) => (
        <div key={k} className="small"><span className="muted">{pretty(k)}: </span><span className="soft" style={{ fontWeight: 500 }}>{fmt(v)}</span></div>
      ))}
    </div>
  );
}

function DocCard({ doc }) {
  const [open, setOpen] = useState(false);
  const view = () => {
    fetch(`${API}/documents/${doc.id}/file`, { headers: { Authorization: `Bearer ${localStorage.getItem("veriqo_token")}` } })
      .then((r) => r.blob()).then((b) => window.open(URL.createObjectURL(b), "_blank"));
  };
  return (
    <div className="card" style={{ padding: 16, background: "var(--surface-2)" }}>
      <div className="row between">
        <div className="row gap-3">
          <div style={{ width: 38, height: 38, borderRadius: 9, background: "var(--surface)", border: "1px solid var(--border)", display: "grid", placeItems: "center", color: "var(--text-soft)" }}><IconFile size={19} /></div>
          <div>
            <div style={{ fontWeight: 550, fontSize: "0.92rem" }}>{doc.fileName}</div>
            <div className="small muted">{(doc.size / 1024).toFixed(0)} KB · {dateTimeFmt(doc.uploadedAt)}</div>
          </div>
        </div>
        <div className="row gap-2">
          <RiskBadge level={doc.riskLevel} />
          <DocStatusBadge status={doc.status} />
        </div>
      </div>

      {doc.riskFlags?.length > 0 && (
        <div className="stack gap-2" style={{ marginTop: 12 }}>
          {doc.riskFlags.map((f, i) => (
            <div key={i} className="row gap-2 small" style={{ color: "var(--warning)" }}>
              <IconAlert size={14} style={{ flexShrink: 0, marginTop: 2 }} /> {f}
            </div>
          ))}
        </div>
      )}
      {doc.status === "Rejected" && doc.rejectReason && (
        <div className="alert alert-error small" style={{ marginTop: 10 }}>Rejected: {doc.rejectReason}</div>
      )}

      <div className="row gap-3" style={{ marginTop: 12 }}>
        <button className="btn btn-ghost btn-sm" onClick={view}><IconDownload size={15} /> View file</button>
        <button className="btn btn-ghost btn-sm" onClick={() => setOpen((o) => !o)}>
          {open ? "Hide" : "Show"} verification data
        </button>
      </div>
      {open && (
        <div style={{ marginTop: 10, borderTop: "1px solid var(--border)", paddingTop: 10 }}>
          <div className="small muted row gap-2"><IconShield size={14} /> Automated verification · risk score {doc.riskScore}/100</div>
          <VerificationChecks data={doc.extractedData} />
          {doc.ocrPreview && (
            <details style={{ marginTop: 8 }}>
              <summary className="small muted" style={{ cursor: "pointer" }}>Raw OCR text</summary>
              <pre style={{ fontSize: "0.72rem", color: "var(--text-muted)", whiteSpace: "pre-wrap", marginTop: 6, maxHeight: 120, overflow: "auto" }}>{doc.ocrPreview}</pre>
            </details>
          )}
        </div>
      )}
    </div>
  );
}

function UploadRow({ req, applicationId, onDone }) {
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");
  const inputRef = useRef();

  const upload = async (file) => {
    if (!file) return;
    setBusy(true); setErr("");
    const fd = new FormData();
    fd.append("applicationId", applicationId);
    fd.append("type", req.type);
    fd.append("file", file);
    try {
      await client.post("/documents/upload", fd, { headers: { "Content-Type": "multipart/form-data" } });
      onDone();
    } catch (e) {
      setErr(e.response?.data?.message || "Upload failed.");
    } finally { setBusy(false); if (inputRef.current) inputRef.current.value = ""; }
  };

  return (
    <div className="card" style={{ padding: 14, border: "1px dashed var(--border-strong)" }}>
      <div className="row between">
        <div className="row gap-2">
          <IconFile size={17} style={{ color: "var(--text-muted)" }} />
          <span style={{ fontWeight: 550, fontSize: "0.9rem" }}>{req.label}</span>
          <span className="badge badge-warning">Required</span>
        </div>
        <button className="btn btn-soft btn-sm" disabled={busy} onClick={() => inputRef.current?.click()}>
          {busy ? <Spinner size={14} /> : <><IconUpload size={15} /> Upload</>}
        </button>
        <input ref={inputRef} type="file" accept="image/jpeg,image/png,application/pdf" hidden
          onChange={(e) => upload(e.target.files[0])} />
      </div>
      {err && <div className="alert alert-error small" style={{ marginTop: 10 }}>{err}</div>}
    </div>
  );
}

export default function ApplicationDetail() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const isNew = params.get("new");
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    client.get(`/applications/${id}`).then((r) => setData(r.data)).finally(() => setLoading(false));
  }, [id]);

  useEffect(() => { load(); }, [load]);

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;
  if (!data) return <div className="alert alert-error">Application not found.</div>;

  const { application: app, documents, requirements = [] } = data;
  const satisfiedTypes = new Set(documents.filter((d) => d.status !== "Rejected").map((d) => d.type));
  const pending = requirements.filter((r) => !satisfiedTypes.has(r.type));
  const allUploaded = pending.length === 0;

  return (
    <div className="fade-in" style={{ maxWidth: 820 }}>
      <Link to="/applications" className="row gap-2 small muted" style={{ marginBottom: 18 }}>
        <IconArrowLeft size={16} /> All applications
      </Link>

      {isNew && (
        <div className="alert alert-success" style={{ marginBottom: 18 }}>
          Application submitted! Upload your documents below so our team can review them.
        </div>
      )}

      {/* Header */}
      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <div className="row between" style={{ marginBottom: 22 }}>
          <div>
            <div className="row gap-3" style={{ marginBottom: 4 }}>
              <h1 style={{ fontSize: "1.6rem" }}>{app.productName}</h1>
              <StatusBadge status={app.status} />
            </div>
            <div className="small muted mono">Reference #{app.id.slice(-8).toUpperCase()} · submitted {dateTimeFmt(app.createdAt)}</div>
          </div>
          <RiskBadge level={app.riskLevel} />
        </div>
        <Timeline status={app.status} history={app.history} />
        {app.reviewNote && app.status !== "Submitted" && (
          <div className={`alert ${app.status === "Rejected" ? "alert-error" : app.status === "Approved" ? "alert-success" : "alert-warning"}`} style={{ marginTop: 16 }}>
            <strong>Reviewer note:</strong>&nbsp;{app.reviewNote}
          </div>
        )}
      </div>

      {/* Documents */}
      <div className="card card-pad" style={{ marginBottom: 20 }}>
        <div className="row between" style={{ marginBottom: 4 }}>
          <h3>Documents</h3>
          <span className="small muted">{satisfiedTypes.size}/{requirements.length} provided</span>
        </div>
        <p className="muted small" style={{ marginBottom: 18 }}>
          Upload JPG, PNG or PDF (max 5 MB). Each file is automatically checked with OCR before review.
        </p>

        {pending.length > 0 && (
          <div className="stack gap-3" style={{ marginBottom: documents.length ? 20 : 0 }}>
            {pending.map((r) => <UploadRow key={r.type} req={r} applicationId={id} onDone={load} />)}
          </div>
        )}

        {allUploaded && documents.length > 0 && (
          <div className="alert alert-success" style={{ marginBottom: 18 }}>
            <IconCheck size={16} /> All required documents uploaded. Our team will review them shortly.
          </div>
        )}

        {documents.length > 0 && (
          <div className="stack gap-3">
            {documents.map((d) => <DocCard key={d.id} doc={d} />)}
          </div>
        )}
      </div>

      {/* Verification summary */}
      {app.riskFlags?.length > 0 && (
        <div className="card card-pad">
          <div className="row gap-2" style={{ marginBottom: 12 }}>
            <IconShield size={18} style={{ color: "var(--primary-hover)" }} />
            <h3>Automated checks</h3>
          </div>
          <div className="stack gap-2">
            {app.riskFlags.map((f, i) => (
              <div key={i} className="row gap-2 small soft"><IconAlert size={14} style={{ color: "var(--warning)", flexShrink: 0, marginTop: 2 }} /> {f}</div>
            ))}
          </div>
          <div className="help" style={{ marginTop: 12 }}>These flags assist our reviewers — a human makes the final decision.</div>
        </div>
      )}

      {/* History */}
      <div className="card card-pad" style={{ marginTop: 20 }}>
        <div className="row gap-2" style={{ marginBottom: 14 }}><IconClock size={18} className="muted" /><h3>Activity</h3></div>
        <div className="stack gap-3">
          {[...app.history].reverse().map((h, i) => (
            <div key={i} className="row gap-3">
              <div style={{ width: 9, height: 9, borderRadius: "50%", background: "var(--primary)", marginTop: 6, flexShrink: 0 }} />
              <div>
                <div className="small" style={{ fontWeight: 550 }}>{h.note || h.status}</div>
                <div className="small muted">{dateTimeFmt(h.at)} · {h.by}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
