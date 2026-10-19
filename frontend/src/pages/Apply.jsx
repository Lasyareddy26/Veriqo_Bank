import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import client from "../api/client";
import { Spinner } from "../components/ui";
import { IconArrowLeft, IconForProduct, IconFile } from "../components/Icons";

const DOC_LABELS = {
  GovernmentId: "Government ID", AddressProof: "Address proof", SalarySlip: "Salary slip",
  BankStatement: "Bank statement", EmploymentLetter: "Employment letter",
};

export default function Apply() {
  const { key } = useParams();
  const navigate = useNavigate();
  const [product, setProduct] = useState(null);
  const [answers, setAnswers] = useState({});
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    client.get(`/products/${key}`).then((r) => {
      setProduct(r.data);
      const init = {};
      r.data.fields.forEach((f) => { init[f.name] = f.type === "select" ? (f.options[0] || "") : ""; });
      setAnswers(init);
    }).catch(() => setError("Product not found.")).finally(() => setLoading(false));
  }, [key]);

  const set = (name) => (e) => setAnswers((a) => ({ ...a, [name]: e.target.value }));

  const submit = async (e) => {
    e.preventDefault();
    setError(""); setSubmitting(true);
    try {
      const { data } = await client.post("/applications", { productKey: key, answers });
      navigate(`/applications/${data.id}?new=1`);
    } catch (err) {
      setError(err.response?.data?.message || "Could not submit application.");
      setSubmitting(false);
    }
  };

  if (loading) return <div className="row" style={{ justifyContent: "center", padding: 60 }}><Spinner size={28} /></div>;
  if (!product) return <div className="alert alert-error">{error || "Not found"}</div>;

  return (
    <div className="fade-in" style={{ maxWidth: 680 }}>
      <Link to="/products" className="row gap-2 small muted" style={{ marginBottom: 18 }}>
        <IconArrowLeft size={16} /> Back to products
      </Link>

      <div className="card card-pad" style={{ marginBottom: 20, display: "flex", gap: 16, alignItems: "center" }}>
        <div style={{ width: 52, height: 52, borderRadius: 13, background: "var(--primary-soft)", display: "grid", placeItems: "center", color: "var(--primary-hover)", flexShrink: 0 }}>
          {IconForProduct(product.icon, { size: 26 })}
        </div>
        <div>
          <h2 style={{ fontSize: "1.4rem" }}>{product.name}</h2>
          <p className="muted small">{product.description}</p>
        </div>
      </div>

      {error && <div className="alert alert-error" style={{ marginBottom: 18 }}>{error}</div>}

      <form onSubmit={submit} className="card card-pad">
        <h3 style={{ marginBottom: 4 }}>Application details</h3>
        <p className="muted small" style={{ marginBottom: 20 }}>Fill in the details below. You’ll upload documents on the next screen.</p>

        <div className="grid" style={{ gridTemplateColumns: "1fr 1fr" }}>
          {product.fields.map((f) => (
            <div className="field" key={f.name} style={{ gridColumn: f.type === "textarea" ? "1 / -1" : "auto" }}>
              <label className="label">{f.label}{f.required && " *"}</label>
              {f.type === "select" ? (
                <select className="select" value={answers[f.name] || ""} onChange={set(f.name)} required={f.required}>
                  {f.options.map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              ) : f.type === "textarea" ? (
                <textarea className="textarea" value={answers[f.name] || ""} onChange={set(f.name)} required={f.required} placeholder={f.placeholder} />
              ) : (
                <input className="input" type={f.type} value={answers[f.name] || ""} onChange={set(f.name)}
                  required={f.required} placeholder={f.placeholder}
                  min={f.min ?? undefined} max={f.max ?? undefined} />
              )}
              {f.helpText && <div className="help">{f.helpText}</div>}
            </div>
          ))}
        </div>

        <div className="divider" />
        <div className="row gap-2 small muted" style={{ marginBottom: 16 }}>
          <IconFile size={16} /> Required documents:&nbsp;
          <span className="soft">{product.requiredDocuments.map((d) => DOC_LABELS[d] || d).join(" · ")}</span>
        </div>

        <button className="btn btn-primary btn-lg" disabled={submitting}>
          {submitting ? <Spinner /> : "Submit & upload documents"}
        </button>
      </form>
    </div>
  );
}
