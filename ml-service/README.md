# Veriqo Forensics Microservice

A Python/FastAPI service that detects document tampering, called by the .NET backend
during KYC verification. It combines classic image forensics with a trained CNN:

- **Error Level Analysis (ELA)** — re-compresses the image and amplifies the difference;
  edited/spliced regions compress differently and stand out.
- **Copy-move detection** — ORB keypoint self-matching finds duplicated regions.
- **Noise inconsistency** — block-wise high-frequency residual spread (splices differ).
- **Tamper CNN** — a small convolutional net trained on ELA images of genuine vs
  tampered documents, outputs P(tampered).

## Setup

```bash
cd ml-service
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

## Train the model — ONE TIME ONLY (produces model.pt)

The model is trained on the **CASIA 2.0** image-tampering dataset (~12k authentic +
tampered images). Download it once with the Kaggle CLI, then train:

```bash
kaggle datasets download -d divg07/casia-20-image-tampering-detection-dataset -p dataset_casia --unzip
.venv/bin/python train.py --per-class 2500 --epochs 10
```

This writes **`model.pt`**. You only ever run this once — the trained weights are saved
to disk. The service below just **loads** `model.pt`; it never retrains on startup, so
running the app is fast every time. (`generate_dataset.py` + the synthetic path still
exist as a fallback if you don't want to download CASIA.)

## Run the service (port 8000)

```bash
.venv/bin/uvicorn app:app --port 8000
```

- `GET /health` → service + model status
- `POST /analyze` (multipart `file`) → `{ tamperProbability, label, cnnProbability, signals }`

## How the .NET backend uses it
`ForensicsClient` POSTs each uploaded image here and adds a **"Forensic analysis (ML)"**
check to the document's verification report. If this service is down, the backend
**degrades gracefully** — verification continues without the ML tamper check.
Configure the URL in `backend/appsettings.json` → `Forensics:Url`.

## Notes
- The model is trained on the **CASIA 2.0** real tampering benchmark (authentic vs
  spliced/copy-move images). For a document-specific model you could retrain on **DocTamper**
  — the architecture, `train.py`, and the .NET integration stay identical.
- Works on images (JPG/PNG). PDFs are skipped by the forensic step (handled by OCR).
