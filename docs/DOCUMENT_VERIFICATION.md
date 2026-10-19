# Document Verification Pipeline

> How Veriqo turns an uploaded KYC file into an **explainable, evidence-backed verdict** —
> *Appears genuine* · *Needs review* · *Likely fraudulent* — without ever auto-deciding a customer's fate.

Every document a customer uploads passes through a layered pipeline: file-safety →
duplicate detection → metadata/forensic tamper analysis → OCR → authenticity →
field-level cross-checks against the profile → an **official issuer-registry lookup**.
Each layer contributes **pass / warn / fail** checks with a penalty; the penalties sum to a
risk score, and the score maps to a verdict a human reviewer acts on.

---

## 1. Where it sits in the system

```mermaid
flowchart LR
    subgraph Client["🖥️  React SPA"]
        U["Customer uploads<br/>ID · address proof · payslip · …"]
    end

    subgraph API["⚙️  ASP.NET Core API"]
        DC["DocumentsController<br/>POST /api/documents/upload"]
        FV["FileValidator<br/>magic-byte + size"]
        FS["FileStorage<br/>private file store"]
        DA["DocumentAnalyzer<br/>orchestrates the pipeline"]
        AS["ApplicationService<br/>recompute risk · auto-advance"]
    end

    subgraph Stores["🗄️  Data"]
        DB[("MongoDB<br/>documents · profiles · registry")]
        FSX["Local disk<br/>Storage/uploads"]
    end

    subgraph ML["🧪  Python forensics microservice (:8000)"]
        FX["FastAPI /analyze<br/>ELA · copy-move · CNN"]
    end

    OCR["Tesseract OCR<br/>(CLI, per-word confidence)"]

    U --> DC --> FV --> FS --> DA
    FS --> FSX
    DA --> OCR
    DA --> FX
    DA --> DB
    DA --> AS --> DB

    classDef api fill:#E8E2D4,stroke:#6B5B47,color:#2B2824;
    classDef ext fill:#DCEAF5,stroke:#3A6EA5,color:#13293D;
    classDef store fill:#E6E6E6,stroke:#555,color:#1a1a1a;
    class DC,FV,FS,DA,AS api;
    class FX,OCR ext;
    class DB,FSX store;
```

The forensics microservice and OCR binary are **optional dependencies** — if either is
unavailable the pipeline *degrades gracefully* (that layer is skipped or flagged as a
warning) instead of blocking the upload.

---

## 2. The per-upload pipeline

```mermaid
flowchart TD
    A["📤 Upload received<br/>(≤ 6 MB, Customer role)"] --> B{"File-type valid?<br/>magic bytes = JPG/PNG/PDF"}
    B -- no --> BX["❌ Reject upload<br/>400 Bad Request"]
    B -- yes --> C["Save to private store<br/>+ SHA-256 hash"]

    C --> D{"1 · Duplicate?<br/>same hash exists"}
    D -- "other account" --> D1["🚩 FAIL +45"]
    D -- "own account" --> D2["⚠️ WARN +15"]
    D -- "no" --> D3["✅ PASS"]

    D1 & D2 & D3 --> E{"2 · EXIF metadata<br/>saved in an image editor?"}
    E -- "Photoshop / GIMP / …" --> E1["🚩 FAIL +40"]
    E -- "no" --> E2["✅ PASS"]

    E1 & E2 --> F["2b · ML forensics<br/>ELA + copy-move + CNN"]
    F -->|"likely_tampered"| F1["⚠️ WARN +15<br/>(supporting signal)"]
    F -->|"suspicious"| F2["⚠️ WARN +8"]
    F -->|"clean / service down"| F3["✅ PASS / skip"]

    F1 & F2 & F3 --> G["3 · OCR (Tesseract)<br/>text + mean word confidence"]
    G --> H{"Enough readable text?"}
    H -- no --> HX["🚩 FAIL +35 → stop & score"]
    H -- "yes, low conf <55%" --> H1["⚠️ WARN +12"]
    H -- "yes" --> H2["✅ PASS"]

    H1 & H2 --> I{"4 · Looks like the<br/>claimed document type?"}
    I -- "no markers" --> I1["🚩 FAIL +35"]
    I -- "markers found" --> I2["✅ PASS"]

    I1 & I2 --> J{"5 · Profile name<br/>on the document?"}
    J -- "no" --> J1["🚩 FAIL +25"]
    J -- "yes ≥50%" --> J2["✅ PASS"]

    J1 & J2 --> K["6 · Type-specific checks"]
    K --> L["🏛️ Official registry lookup<br/>(Government ID only)"]
    L --> M["Score → Verdict"]
    HX --> M

    M --> N{"Risk score"}
    N -- "0–24" --> N1["🟢 Appears genuine"]
    N -- "25–54" --> N2["🟡 Needs review"]
    N -- "≥ 55" --> N3["🔴 Likely fraudulent"]

    N1 & N2 & N3 --> O["Persist KycDocument<br/>+ recompute application risk"]

    classDef pass fill:#DAF0DD,stroke:#2E7D4F,color:#15371F;
    classDef warn fill:#FBEFD3,stroke:#B8860B,color:#4A3609;
    classDef fail fill:#F6D9D7,stroke:#B23A33,color:#4A1512;
    classDef proc fill:#E8E2D4,stroke:#6B5B47,color:#2B2824;
    class D3,E2,F3,H2,I2,J2,N1 pass;
    class D2,F1,F2,H1 warn;
    class BX,D1,E1,HX,I1,J1,N3 fail;
    class N2 warn;
    class A,C,F,G,K,L,M,O proc;
```

### Scoring at a glance

| Layer | Check | Trigger | Penalty |
|-------|-------|---------|--------:|
| File safety | Magic-byte + size | Not JPG/PNG/PDF, or > 6 MB | *rejected outright* |
| 1 · Duplicate | SHA-256 across **all** accounts | Match on another account | **+45** |
| | | Re-upload on own account | +15 |
| 2 · Metadata | EXIF "Software"/"Creator Tool" | Known image editor | **+40** |
| 2b · ML forensics | ELA + copy-move + CNN | `likely_tampered` / `suspicious` | +15 / +8 *(warn-only)* |
| 3 · Readability | OCR mean word confidence | Too little text | **+35** *(stops here)* |
| | | Confidence < 55% | +12 |
| 4 · Authenticity | Expected keyword markers | Zero markers for the type | **+35** |
| 5 · Name match | Token overlap with profile | < 50% of name tokens found | +25 |
| 6 · Government ID | ID number on document | Profile ID not found | +22 |
| | Date of birth | DOB mismatch | +15 |
| | **Expiry** | ID expired | **+50** |
| 6 · Address proof | Issue date recency | Older than 3 months | +20 |
| 6 · Salary slip | Income vs. declared | Gap > 15% | +25 |
| 🏛️ Registry | Issuer-registry existence | ID absent / holder mismatch | **+40 – +45** |

**Verdict mapping:** `score < 25` → *Appears genuine* · `25–54` → *Needs review* ·
`≥ 55` → *Likely fraudulent*. The score is capped at 100. The system **never
auto-approves or auto-rejects** — it hands the reviewer an itemised checklist and a verdict.

---

## 3. The official registry check — the authoritative signal

For a government ID, the single strongest signal is simply: **does this ID actually
exist in the issuer's registry, and does the registered holder match the profile?**
This is the DigiLocker-style existence check.

```mermaid
flowchart LR
    A["Government ID<br/>idNumber · name · dob"] --> B{"Registry provider<br/>configured?"}
    B -- "real API key set" --> C["POST /verify<br/>→ KYC aggregator / DigiLocker"]
    B -- "mock (default)" --> D["Look up seeded<br/>registry in MongoDB"]
    C --> E{"exists &&<br/>nameMatch && dobMatch"}
    D --> E
    E -- "yes" --> F["✅ PASS — ID genuine,<br/>holder confirmed"]
    E -- "exists, details differ" --> G["🚩 FAIL +40"]
    E -- "not found" --> H["🚩 FAIL +45 (real)<br/>⚠️ WARN +15 (mock)"]
    E -- "registry unreachable" --> I["⚠️ WARN +5<br/>could not confirm"]

    classDef pass fill:#DAF0DD,stroke:#2E7D4F,color:#15371F;
    classDef warn fill:#FBEFD3,stroke:#B8860B,color:#4A3609;
    classDef fail fill:#F6D9D7,stroke:#B23A33,color:#4A1512;
    classDef proc fill:#E8E2D4,stroke:#6B5B47,color:#2B2824;
    class F pass;
    class I,H warn;
    class G fail;
    class A,C,D proc;
```

`IdentityVerifier` is interface-shaped: out of the box it uses a **seeded local
registry** (so the whole flow works offline for the demo), and setting a provider +
API key switches it to a **real government/KYC source** — the contract
(`{idNumber, name, dob}` → `{exists, nameMatch, dobMatch}`) stays identical.

---

## 4. Inside the ML forensics microservice

A standalone **Python / FastAPI** service (`ml-service/`) runs the heavier image
forensics so the .NET API stays lean. It is a **supporting** signal (warn-only): a model
miss can never flip a genuine document to "fraudulent" on its own — the registry and OCR
rules remain authoritative.

```mermaid
flowchart TD
    A["POST /analyze<br/>(image bytes)"] --> B["Decode to RGB"]
    B --> C["Error-Level Analysis<br/>re-compress @ JPEG90,<br/>amplify the difference"]
    B --> D["Copy-move detection<br/>ORB keypoint self-matching"]
    B --> E["Noise inconsistency<br/>block-wise residual variance"]

    C --> F["ELA image → 128×128 tensor"]
    F --> G["🧠 TamperCNN<br/>4 conv blocks → sigmoid<br/>P(tampered)"]

    G --> H["Combine:<br/>CNN prob is primary;<br/>extreme copy-move nudges +0.15"]
    D --> H
    E --> H
    H --> I{"probability"}
    I -- "≥ 0.60" --> I1["likely_tampered"]
    I -- "0.35–0.59" --> I2["suspicious"]
    I -- "< 0.35" --> I3["clean"]

    I1 & I2 & I3 --> J["Return tamperProbability,<br/>label, raw signals"]

    classDef proc fill:#DCEAF5,stroke:#3A6EA5,color:#13293D;
    classDef model fill:#E7DBF0,stroke:#6B4C8A,color:#2E1A42;
    class A,B,C,D,E,F,H,J proc;
    class G model;
```

**`TamperCNN`** (PyTorch) takes the ELA image as input — edited/spliced regions
compress differently and "light up" under ELA, which the CNN learns to recognise:

```
ELA(128×128×3)
  → Conv(3→16)  BN  ReLU  MaxPool   → 64×64
  → Conv(16→32) BN  ReLU  MaxPool   → 32×32
  → Conv(32→64) BN  ReLU  MaxPool   → 16×16
  → Conv(64→64) BN  ReLU  MaxPool   → 8×8
  → AdaptiveAvgPool → FC(64→32) → FC(32→1)
  → sigmoid = P(tampered)
```

Classic forensics run alongside the CNN: **copy-move** (duplicated regions via ORB
self-matching) and **noise inconsistency** (spliced regions carry different sensor
noise). See [`ml-service/README.md`](../ml-service/README.md) for training and running.

---

## 5. End-to-end sequence

```mermaid
sequenceDiagram
    autonumber
    participant C as Customer (SPA)
    participant API as DocumentsController
    participant DA as DocumentAnalyzer
    participant OCR as Tesseract
    participant ML as Forensics (:8000)
    participant REG as IdentityVerifier
    participant DB as MongoDB
    participant AD as Admin

    C->>API: POST /documents/upload (file, type, applicationId)
    API->>API: validate magic bytes + size, SHA-256, store file
    API->>DA: AnalyzeAsync(bytes, type, profile)
    DA->>DB: duplicate-hash lookup
    DA->>ML: POST /analyze (ELA + copy-move + CNN)
    ML-->>DA: tamperProbability + label
    DA->>OCR: extract text + confidence
    OCR-->>DA: text, mean word confidence
    DA->>DA: authenticity + name/DOB/expiry/income checks
    DA->>REG: VerifyId(idNumber, name, dob)
    REG-->>DA: exists / nameMatch / dobMatch
    DA-->>API: checklist + risk score + verdict
    API->>DB: save KycDocument, recompute application risk
    Note over API,DB: all required docs present → auto-advance to "Under Review"
    AD->>API: review queue → per-document Verify / Reject
```

---

## 6. Design principles

- **Explainable, not a black box.** Every verdict is a list of named pass/warn/fail
  checks with human-readable detail — the reviewer sees *why*, not just a number.
- **Human-in-the-loop.** The pipeline scores and flags; a person decides. It never
  auto-approves or auto-rejects.
- **Defence in depth.** No single layer is trusted alone. The ML model is a *supporting*
  signal; the registry lookup and OCR field-checks are authoritative.
- **Fail-soft.** A missing OCR binary or a downed forensics service degrades to a warning,
  never a blocked upload.
- **Swappable.** Storage, OCR, forensics, and the registry all sit behind interfaces —
  swap in cloud OCR, blob storage, or a real DigiLocker provider without touching controllers.

---

### Key source files

| Concern | File |
|---------|------|
| Upload endpoint | [`backend/Controllers/DocumentsController.cs`](../backend/Controllers/DocumentsController.cs) |
| Pipeline orchestrator | [`backend/Services/DocumentAnalyzer.cs`](../backend/Services/DocumentAnalyzer.cs) |
| File-safety validation | [`backend/Services/FileValidator.cs`](../backend/Services/FileValidator.cs) |
| OCR engine | [`backend/Services/OcrEngine.cs`](../backend/Services/OcrEngine.cs) |
| Registry / DigiLocker check | [`backend/Services/IdentityVerifier.cs`](../backend/Services/IdentityVerifier.cs) |
| Forensics client | [`backend/Services/ForensicsClient.cs`](../backend/Services/ForensicsClient.cs) |
| ML microservice | [`ml-service/`](../ml-service/) (`app.py`, `forensics.py`, `model.py`) |
