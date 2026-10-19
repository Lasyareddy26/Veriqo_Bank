# AI Support Assistant

> A **multi-agent, retrieval-grounded** assistant that helps a human support agent answer a
> customer ticket — it reads the ticket *and* the linked application, retrieves the bank's own
> policy, and drafts a reply the agent reviews, edits, and sends. It **suggests; the human decides.**

When an admin opens a ticket and clicks **Analyze**, the backend runs a small pipeline of
specialised agents instead of a single generic LLM call. Each agent has one job — triage,
retrieval, drafting, next-best-actions — and the drafting agent is **grounded in retrieved
bank policy** so timelines and procedures are specific and correct, not invented.

---

## 1. Where it sits in the system

```mermaid
flowchart LR
    subgraph Client["🖥️  Admin console (React)"]
        T["Ticket view → 'Analyze'"]
        P["AI panel:<br/>summary · sentiment · reply · actions · sources"]
    end

    subgraph API["⚙️  ASP.NET Core API"]
        AC["AdminController<br/>POST /admin/tickets/{id}/analyze"]
        CTX["Build AiContext<br/>+ PII masking"]
        MA["MultiAgentAssistant<br/>(orchestrator)"]
        KR["KeywordRetriever<br/>(RAG · TF-IDF)"]
    end

    subgraph Data["🗄️  MongoDB"]
        TK[("tickets")]
        AP[("applications")]
        KB[("knowledge base<br/>14 policy / FAQ articles")]
    end

    LLM["☁️  Groq<br/>OpenAI-compatible<br/>chat completions (JSON mode)"]

    T --> AC --> CTX --> MA
    MA --> KR --> KB
    MA --> LLM
    AC --> TK
    AC --> AP
    MA --> AC --> P

    classDef api fill:#E8E2D4,stroke:#6B5B47,color:#2B2824;
    classDef ext fill:#DCEAF5,stroke:#3A6EA5,color:#13293D;
    classDef store fill:#E6E6E6,stroke:#555,color:#1a1a1a;
    class AC,CTX,MA,KR api;
    class LLM ext;
    class TK,AP,KB store;
```

The context the orchestrator works from is **not just the chat thread** — it also pulls the
linked application's live state (product, status, risk level, automated flags, and which
required documents are still missing), so the answer is specific to *this* customer's situation.

---

## 2. The multi-agent + RAG pipeline

```mermaid
flowchart TD
    A["🎫 Ticket + linked application"] --> B["PII masking<br/>ID numbers → [ID]<br/>long digit runs → [REDACTED]"]
    B --> C["AiContext<br/>thread + app status + missing docs"]

    C --> D["1 · Triage agent (NLP)"]
    D --> D1["intent · entities ·<br/>sentiment · search query"]

    D1 --> E["2 · Retrieval (RAG)"]
    E --> E1["TF-IDF over knowledge base<br/>→ top-3 policy snippets"]

    D1 --> F["3 · Drafting agent"]
    E1 --> F
    C --> F
    F --> F1["summary +<br/>policy-grounded reply"]

    D1 --> G["4 · Next-best-action agent"]
    E1 --> G
    G --> G1["2–4 concrete steps<br/>for the human agent"]

    F1 --> H["AiAnalysis"]
    G1 --> H
    D1 --> H
    E1 --> H
    H --> I["Persist on ticket +<br/>render in admin panel<br/>(incl. the sources used)"]

    classDef agent fill:#E7DBF0,stroke:#6B4C8A,color:#2E1A42;
    classDef rag fill:#DAF0DD,stroke:#2E7D4F,color:#15371F;
    classDef proc fill:#E8E2D4,stroke:#6B5B47,color:#2B2824;
    class D,F,G agent;
    class E,E1 rag;
    class A,B,C,D1,F1,G1,H,I proc;
```

### What each agent does

| # | Agent | Input | Output |
|---|-------|-------|--------|
| 1 | **Triage / NLP** | Masked ticket thread | `intent`, `entities`, `sentiment {label, score, urgency}`, a `searchQuery` for the KB |
| 2 | **Retrieval (RAG)** | The triage `searchQuery` | Top-3 relevant bank-policy snippets + their titles (shown as **sources**) |
| 3 | **Drafting** | Thread + intent + **retrieved policy** + app state | A 2–3 sentence `summary` and a `suggestedReply` grounded in policy |
| 4 | **Next-best-action** | Thread + intent + policy + status | 2–4 concrete steps for the *agent* (not the customer) |

Agents 1, 3 and 4 are separate **JSON-mode** LLM calls to Groq; step 2 is pure retrieval
(no LLM). If the LLM is unavailable at the triage or drafting step, the whole request falls
back to a **deterministic offline assistant** that still produces every field — so the
feature is fully demoable with no API key.

---

## 3. Why RAG? (grounding, not guessing)

Without retrieval, an LLM asked "how long does a personal loan review take?" will *invent* a
plausible-sounding timeline. With RAG, the drafting agent is handed the bank's **actual**
policy snippet and told to ground every specific in it — so the reply says the real
**2–3 business day** SLA, and the UI shows exactly **which policy article** it used.

```mermaid
flowchart LR
    Q["searchQuery<br/>'personal loan review delay'"] --> T["Tokenize<br/>drop stop-words"]
    T --> S["Score each article<br/>TF-IDF"]
    S --> B["Title + tag matches<br/>weighted ×2.2"]
    B --> R["Rank → top 3"]
    R --> O["Snippets → drafting agent<br/>+ titles → 'Sources' chips"]

    KB[("14 seeded policy /<br/>FAQ articles")] --> S

    classDef rag fill:#DAF0DD,stroke:#2E7D4F,color:#15371F;
    classDef store fill:#E6E6E6,stroke:#555,color:#1a1a1a;
    class Q,T,S,B,R,O rag;
    class KB store;
```

Retrieval sits behind the `IKnowledgeRetriever` interface, so the current **TF-IDF keyword**
retriever can be swapped for a **semantic (embeddings)** retriever without touching the
assistant. The knowledge base covers review timelines, required documents, status meanings,
rejection reasons, income verification, complaint escalation, and more.

---

## 4. End-to-end sequence

```mermaid
sequenceDiagram
    autonumber
    participant AD as Admin (console)
    participant API as AdminController
    participant MA as MultiAgentAssistant
    participant G as Groq (LLM)
    participant KB as KeywordRetriever
    participant DB as MongoDB

    AD->>API: POST /admin/tickets/{id}/analyze
    API->>DB: load ticket + linked application + missing docs
    API->>API: mask PII (IDs, long digit runs)
    API->>MA: AnalyzeAsync(context)

    MA->>G: Triage prompt → intent, entities, sentiment, searchQuery
    G-->>MA: JSON
    MA->>KB: retrieve(searchQuery, k=3)
    KB-->>MA: top-3 policy snippets (+ titles)
    MA->>G: Draft prompt (thread + policy + status) → summary + reply
    G-->>MA: JSON
    MA->>G: Action prompt → next-best-actions
    G-->>MA: JSON
    MA-->>API: AiAnalysis (summary, sentiment, reply, actions, intent, entities, sources, pipeline)
    API->>DB: persist LastAiAnalysis + audit log
    API-->>AD: render panel
    Note over AD: Agent reviews, edits, clicks "Use this reply" → sends
```

If any LLM call returns nothing, `MultiAgentAssistant` hands off to `MockAiAssistant`,
which reproduces the same pipeline shape (rules-based triage → RAG → template reply) so the
response is never empty.

---

## 5. What the assistant returns

A single `AiAnalysis` object drives the whole panel:

| Field | Meaning |
|-------|---------|
| `Summary` | 2–3 sentence recap of the ticket |
| `Sentiment` | `{ label, score, urgency }` from the triage agent |
| `SuggestedReply` | Draft the agent can edit and send ("Use this reply") |
| `NextBestActions` | 2–4 concrete steps for the agent |
| `Intent` / `Entities` | NLP triage output |
| `Sources` | Titles of the policy snippets RAG used (shown as chips) |
| `Pipeline` | The steps that actually ran (surfaced as transparency chips) |
| `Provider` | e.g. `groq:openai/gpt-oss-120b · multi-agent + RAG`, or `mock …` |

---

## 6. Safety & design principles

- **Human-in-the-loop.** The assistant only *suggests*. The agent reviews, edits, and sends —
  nothing is auto-sent to a customer.
- **PII never leaves raw.** Government ID numbers and long digit runs (card/account numbers)
  are masked *before* any text is sent to the LLM.
- **Grounded, not hallucinated.** The drafting agent must base specifics on retrieved policy
  and is told not to invent policy or promise approval; the UI shows the sources it used.
- **Graceful degradation.** No API key, or the LLM is down → a deterministic mock produces
  realistic, fully-populated analysis so the feature always works.
- **Pluggable.** The LLM provider (any OpenAI-compatible endpoint) and the retriever
  (keyword now, embeddings later) are both behind interfaces.

---

### Key source files

| Concern | File |
|---------|------|
| Analyze endpoint + PII masking | [`backend/Controllers/AdminController.cs`](../backend/Controllers/AdminController.cs) |
| Orchestrator + agents + Groq client | [`backend/Services/AiAssistant.cs`](../backend/Services/AiAssistant.cs) |
| RAG retriever | [`backend/Services/KnowledgeRetriever.cs`](../backend/Services/KnowledgeRetriever.cs) |
| Analysis model | [`backend/Models/AiAnalysis.cs`](../backend/Models/AiAnalysis.cs) |
| Knowledge base seed | [`backend/Data/DbSeeder.cs`](../backend/Data/DbSeeder.cs) |
