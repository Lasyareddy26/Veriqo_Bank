import { useEffect, useRef, useState } from "react";
import client from "../api/client";

/**
 * Renders Google's official "Sign in with Google" button when a Google OAuth
 * Client ID is configured on the backend. If it isn't, the component renders
 * nothing — so the app works fine without Google set up.
 */
export default function GoogleButton({ onCredential, onError }) {
  const ref = useRef(null);
  const [clientId, setClientId] = useState("");

  useEffect(() => {
    client.get("/auth/config").then((r) => {
      if (r.data.googleEnabled) setClientId(r.data.googleClientId);
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (!clientId) return;

    const init = () => {
      if (!window.google || !ref.current) return;
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: async (resp) => {
          try {
            await onCredential(resp.credential);
          } catch (e) {
            onError?.(e.response?.data?.message || "Google sign-in failed.");
          }
        },
      });
      window.google.accounts.id.renderButton(ref.current, {
        theme: "outline", size: "large", width: 360, text: "continue_with",
      });
    };

    if (window.google) { init(); return; }
    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.onload = init;
    document.body.appendChild(script);
  }, [clientId]);

  if (!clientId) return null;

  return (
    <div>
      <div className="row gap-3" style={{ margin: "18px 0" }}>
        <div style={{ flex: 1, height: 1, background: "var(--border)" }} />
        <span className="small muted">or</span>
        <div style={{ flex: 1, height: 1, background: "var(--border)" }} />
      </div>
      <div ref={ref} style={{ display: "flex", justifyContent: "center" }} />
    </div>
  );
}
