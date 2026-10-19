"""Document-forensics microservice.

POST /analyze  (multipart file)  ->  tamper probability + forensic signals.
The verdict combines a trained CNN over Error-Level-Analysis with classic
copy-move / noise forensics. Called by the .NET backend during KYC verification.
"""
import os
import torch
from fastapi import FastAPI, UploadFile, File
from fastapi.responses import JSONResponse

from forensics import to_pil, ela_tensor, all_signals
from model import TamperCNN

ROOT = os.path.dirname(os.path.abspath(__file__))
app = FastAPI(title="Veriqo Forensics", version="1.0")

_model = None


def get_model():
    global _model
    if _model is None:
        m = TamperCNN()
        path = os.path.join(ROOT, "model.pt")
        if os.path.exists(path):
            m.load_state_dict(torch.load(path, map_location="cpu"))
            m.eval()
            _model = m
    return _model


@app.get("/health")
def health():
    return {"status": "ok", "modelLoaded": os.path.exists(os.path.join(ROOT, "model.pt"))}


@app.post("/analyze")
async def analyze(file: UploadFile = File(...)):
    data = await file.read()
    try:
        img = to_pil(data)
    except Exception:
        return JSONResponse(status_code=400, content={"error": "unreadable image"})

    signals = all_signals(img)

    model = get_model()
    cnn_prob = None
    if model is not None:
        with torch.no_grad():
            t = torch.tensor(ela_tensor(img)).unsqueeze(0)
            cnn_prob = float(torch.sigmoid(model(t)).item())

    # The trained CNN is the primary detector. Copy-move on text documents is noisy
    # (repeated glyphs match), so it only nudges the score when extremely high — it
    # never drives the verdict on its own.
    prob = cnn_prob if cnn_prob is not None else 0.0
    if signals["copyMoveMatches"] >= 140:
        prob = min(prob + 0.15, 1.0)
    prob = round(prob, 3)

    label = "likely_tampered" if prob >= 0.6 else "suspicious" if prob >= 0.35 else "clean"

    return {
        "modelLoaded": model is not None,
        "tamperProbability": prob,
        "cnnProbability": round(cnn_prob, 3) if cnn_prob is not None else None,
        "label": label,
        "signals": signals,
    }
