# Veriqo Bank — Digital Onboarding Portal

A secure, full-stack banking onboarding application. Customers register, build a
verified profile, apply for banking products, upload KYC documents that are
automatically checked with OCR, and track their application status. Bank admins
review applications, verify documents, and approve/reject — with a full audit trail.

Both parts of the WM Joiners assessment are implemented: **Part 1** (customer portal +
KYC + admin review) and **Part 2** (an AI support assistant for admins — ticket
summarization, sentiment, suggested replies, next-best-actions).

## Tech stack

| Layer      | Technology                                             |
|------------|--------------------------------------------------------|
| Frontend   | React 19 + Vite, React Router, Axios (Claude-themed UI)|
| Backend    | ASP.NET Core 10 Web API (C#)                           |
| Database   | MongoDB (via the official MongoDB .NET driver)         |
| Auth       | ASP.NET Core Identity (PBKDF2 hashing, lockout, policy) on MongoDB, JWT for the SPA, roles |
| OCR        | Tesseract 5 (shelled out), with a file-safety + rules engine |
| Forensics  | Python/FastAPI microservice — ELA + copy-move + a trained tamper **CNN** (PyTorch) |
| Storage    | Local private file store (swappable for S3 / Azure Blob) |

## Features

**Security & auth**
- **ASP.NET Core Identity** manages users on MongoDB: PBKDF2 password hashing, enforced
  password policy, and **account lockout after 5 failed attempts** (brute-force protection)
- JWT bearer tokens secure the API; role-based access (Customer vs Admin)
- Optional **Continue with Google** (real Google ID-token verification server-side)
- **Separate staff portal** at `/admin/login`; the admin email is reserved (no self-register, no Google)

**Customer**
- Register / login with strong-password enforcement, plus optional **Continue with Google**
- Complete a KYC profile (personal, address, employment) — gates applications
- Browse 6 products (Savings, Current Account, Credit Card, Personal Loan, Home Loan,
  Fixed Deposit) with per-product dynamic forms
- A proper profile card (with avatar — Google photo when available) that you can edit & re-save
- Applications auto-advance to **Under Review** once every required document is uploaded
- Upload documents (JPG/PNG/PDF, 5 MB max) — only the ones still required
- Real-time status tracking with a timeline, automated-check flags, and activity log
- In-app notifications on every status change
- **Raise support tickets**, linked to a specific application, and chat with an agent

**Admin AI support assistant (Part 2) — multi-agent + RAG + NLP**
- "Analyze with AI" runs a **multi-agent pipeline**: (1) a **triage/NLP agent** extracts
  **intent, entities, sentiment**; (2) a **RAG** step retrieves the most relevant **bank
  policy/FAQ** snippets from a knowledge base; (3) a **drafting agent** writes a reply
  **grounded in the retrieved policy** (the UI shows the **sources** it used); (4) a
  **next-best-action agent** proposes concrete steps. It also uses the linked application's
  real state (status, risk flags, missing documents), so output is specific, not generic.
- PII is masked before the call; the assistant only *suggests* — the agent reviews,
  edits ("Use this reply"), and sends. Provider is pluggable (**Groq** / OpenAI-compatible)
  with a deterministic mock fallback; RAG retrieval is behind an interface (TF-IDF keyword
  now, swappable for semantic embeddings).

**Admin**
- Review queue with live stats and status/risk filters
- Full application view: customer profile, answers, documents
- Per-document **Verify / Reject** and per-application **Approve / Reject / Request info**
- Append-only audit log of every decision

**Automated KYC verification (per upload) — robust & explainable**
- Magic-byte file-type validation + size limit
- SHA-256 hashing + duplicate detection across accounts
- **Image metadata / tamper inspection** (flags files last saved in an image editor)
- Tesseract OCR with **per-word confidence scoring** (poor scans are flagged)
- **Document-type authenticity** — confirms the file reads like the claimed document
- Cross-checks vs. the profile: name match, DOB match, ID-number match
- **Real expiry detection** — reads "valid thru / expiry" dates and rejects expired IDs
- Address-proof recency + salary-slip income vs. declared income
- **Official registry verification (DigiLocker-style)** — the authoritative "does this ID
  actually exist" check: the government ID is looked up against the issuer registry and the
  holder's name/DOB are confirmed. This is the **primary genuine-vs-fraud signal**.
- **ML forensic tamper detection** — a Python microservice runs Error Level Analysis,
  copy-move detection, and a trained **CNN** as a *supporting* tamper signal
- Produces a per-document **verdict** — *Appears genuine* / *Needs review* / *Likely
  fraudulent* — with an itemised pass/warn/fail checklist. The system never auto-decides.

### Forensics microservice (optional but recommended)
The ML tamper detector lives in [`ml-service/`](ml-service/). Train once, then run it:
```bash
cd ml-service
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/python generate_dataset.py && .venv/bin/python train.py   # one-time
.venv/bin/uvicorn app:app --port 8000
```
The backend calls it automatically and **degrades gracefully if it isn't running**
(the rest of verification continues). See [ml-service/README.md](ml-service/README.md).

## Configuration

Edit `backend/appsettings.json`:

- **Admin account** — exactly one email is the admin; it's seeded on startup, can't be
  self-registered, and can't sign in via Google:
  ```json
  "Admin": { "Email": "admin@veriqo.com", "Password": "Admin@123" }
  ```
- **Google sign-in** (optional) — paste an OAuth Client ID to enable the Google button:
  ```json
  "Google": { "ClientId": "<your-client-id>.apps.googleusercontent.com" }
  ```
  Get one at [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services →
  Credentials → Create OAuth client ID (Web application). Add `http://localhost:5173` as an
  authorized JavaScript origin. Leave `ClientId` empty to hide the Google button entirely.

- **Official registry / DigiLocker verification** — government IDs are checked against an
  issuer registry (does the ID exist, does the holder match). Out of the box it uses a
  **seeded local registry** (`mock`) so the flow works offline. To use a real government
  source, set a provider + credentials:
  ```json
  "Registry": { "Provider": "digilocker", "BaseUrl": "<kyc-api-base-url>", "ApiKey": "...", "ApiSecret": "..." }
  ```
  Real DigiLocker/PAN/Aadhaar access is **consent-based and requires org onboarding** — the
  practical route is a KYC aggregator that resells it with a sandbox + API key (e.g. **Setu**,
  **Sandbox.co.in**, **Signzy**, **Cashfree**) or direct **DigiLocker Partner / APISetu**
  registration. The `IdentityVerifier` posts `{idNumber, name, dob}` and reads back
  `{exists, nameMatch, dobMatch}`; point `BaseUrl` at your provider's verify endpoint.

- **Email OTP verification** — on registration a 6-digit code is emailed; sign-in is blocked
  until it's verified. Configure SMTP to send real emails:
  ```json
  "Smtp": { "Host": "smtp.gmail.com", "Port": 587, "User": "you@gmail.com",
            "Password": "<gmail app password>", "FromEmail": "you@gmail.com", "UseStartTls": true }
  ```
  For Gmail, create an **App Password** (Google Account → Security → 2-Step Verification →
  App passwords). **If SMTP is left empty**, the app runs in dev mode: the code is logged and
  shown on the verification screen so the flow stays fully testable without mail credentials.

- **AI support assistant** — set a provider + key to use a real LLM for ticket analysis:
  ```json
  "Ai": { "Provider": "groq", "BaseUrl": "https://api.groq.com/openai/v1", "ApiKey": "...", "Model": "openai/gpt-oss-120b" }
  ```
  Groq is OpenAI-compatible (get a free key at console.groq.com; pick any chat model your
  key can access — `GET /openai/v1/models` lists them). **If `ApiKey` is empty**, a built-in
  deterministic mock produces realistic analysis so the feature is fully demoable offline.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [MongoDB](https://www.mongodb.com/try/download/community) running on `localhost:27017`
- [Tesseract OCR](https://tesseract-ocr.github.io/) — `brew install tesseract` (macOS).
  The path is set in `backend/appsettings.json` → `Ocr:TesseractPath`.

## Running it

**1. Backend** (http://localhost:5105)
```bash
cd backend
dotnet run --launch-profile http
```
On first run it seeds the three products and a default admin account.

**2. Frontend** (http://localhost:5173)
```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173.

## Demo accounts

| Role     | Where            | Email               | Password    |
|----------|------------------|---------------------|-------------|
| Admin    | **/admin/login** | `admin@veriqo.com`  | `Admin@123` |
| Customer | /login           | register your own   | —           |

## Sample documents

`sample-documents/generate.py` creates test KYC images (a matching government ID and
address proof, plus a salary slip with a deliberate income mismatch to demonstrate
the risk flags). Run `python3 sample-documents/generate.py` and upload the PNGs.

## Project structure

```
backend/
  Controllers/   Auth, Profile, Products, Applications, Documents, Admin, Notifications
  Models/        User, CustomerProfile, Product, Application, KycDocument, Notification, AuditLog
  Services/      TokenService, FileStorage, OcrEngine (Tesseract), DocumentAnalyzer,
                 ApplicationService (eligibility + risk), NotificationService, AuditService
  Data/          MongoDbContext, DbSeeder
frontend/
  src/pages/     Login, Register, Dashboard, Profile, Products, Apply,
                 Applications, ApplicationDetail, Notifications, admin/*
  src/components/ Layout, Icons, ui (badges/helpers), ProtectedRoute
```

## Notes & production path
- File storage, OCR, and notifications sit behind interfaces (`IFileStorage`,
  `IOcrEngine`, `NotificationService`) — swap in S3/Azure Blob, Textract/Azure Document
  Intelligence, and SES/Azure Communication Services without touching controllers.
- Secrets (JWT key, connection string) are in `appsettings.json` for local dev; move
  them to a secret store for production.
