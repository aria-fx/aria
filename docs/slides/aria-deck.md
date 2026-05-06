---
marp: true
theme: aria
paginate: true
backgroundColor: #1E1B2E
color: #E5E7EB
style: |
  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;700&display=swap');

  :root {
    --purple: #6B21A8;
    --purple-light: #EEEDFE;
    --dark: #1E1B2E;
    --mid: #D1D5DB;
    --accent: #0891B2;
    --teal: #0D9488;
    --coral: #F97316;
    --white: #FFFFFF;
    --light-bg: #F4F0FA;
  }

  section {
    font-family: 'Inter', 'Calibri', sans-serif;
    font-size: 24px;
    padding: 40px 60px;
  }

  section::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 4px;
    background: var(--purple);
  }

  h1 {
    color: var(--white);
    font-size: 2.2em;
    font-weight: 700;
    margin-bottom: 0.2em;
  }

  h2 {
    color: var(--accent);
    font-size: 1.2em;
    font-weight: 400;
  }

  h3 {
    color: var(--purple);
    font-size: 1.1em;
    font-weight: 600;
  }

  strong {
    color: var(--white);
  }

  em {
    color: var(--accent);
    font-style: italic;
  }

  code {
    background: rgba(107, 33, 168, 0.2);
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.85em;
  }

  table {
    font-size: 0.75em;
    margin-top: 10px;
  }

  th {
    background: var(--purple);
    color: white;
    padding: 8px 12px;
  }

  td {
    padding: 6px 12px;
    border-bottom: 1px solid rgba(255,255,255,0.1);
  }

  section.title {
    display: flex;
    flex-direction: column;
    justify-content: center;
  }

  section.light {
    background: var(--white);
    color: #4A4458;
  }

  section.light h1 {
    color: var(--dark);
  }

  section.light strong {
    color: var(--dark);
  }

  section.light td {
    border-bottom: 1px solid rgba(0,0,0,0.1);
  }

  .columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 24px;
  }

  .columns-3 {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    gap: 20px;
  }

  .card {
    background: rgba(255,255,255,0.05);
    border-radius: 8px;
    padding: 16px;
    border-left: 3px solid var(--purple);
  }

  section.light .card {
    background: var(--light-bg);
    border-left: 3px solid var(--purple);
  }

  .card-teal { border-left-color: var(--teal); }
  .card-coral { border-left-color: var(--coral); }
  .card-accent { border-left-color: var(--accent); }
---

<!-- _class: title -->

# ARIA:
# Governing the AI Asset Estate

## Asset Registry for Intelligent Agents

OASF Classification · GitHub Marketplace · Microsoft Purview Governance

**Josh Garverick** | Chief Technical Architect, Xebia
*April 2026*

---

<!-- _class: light -->

# The problem: AI asset sprawl

Enterprises are accumulating AI primitives without a framework for managing them

<div class="columns">
<div class="card card-coral">

### No inventory
Agents, skills, MCP servers, and prompt libraries proliferate without catalog or ownership

</div>
<div class="card card-coral">

### No metamodel
No standard way to describe relationships between AI building blocks

</div>
<div class="card card-coral">

### No governance
Sensitivity labels and DLP policies don't extend to AI-specific artifacts

</div>
<div class="card card-coral">

### No discovery
Teams can't find, reuse, or safely compose AI assets across organizational boundaries

</div>
</div>

---

<!-- _class: light -->

# Four-layer architecture

<div class="columns-3">
<div class="card">

### Metamodel
*What are these things?*

Entity types, relationships, lifecycle states for AI primitives — classified via OASF

</div>
<div class="card card-accent">

### Marketplace
*How do you manage them?*

Publish, discover, version, and compose AI assets via GitHub + OCI registry

</div>
<div class="card card-teal">

### Governance
*How do you control them?*

Classify, label, enforce, and audit via Microsoft Purview + OASF overlay

</div>
</div>

**Distribution** — Catalog API (distribution gateway) translates OCI artifacts into platform-native installs (Claude Desktop `.mcpb`, Cowork suggestions, web portal) with transparent governance enforcement

---

<!-- _class: light -->

# OASF: Open Agentic Schema Framework

*The classification taxonomy for AI assets — inspired by OCSF, built by AGNTCY (Cisco Outshift)*

<div class="columns">
<div>

- **Records** — Primary data structure: identity, metadata, capabilities, version
- **Skills** — Hierarchical taxonomy of capabilities (NLP, vision, retrieval…)
- **Domains** — Business domain annotations for scoped discovery
- **Modules** — Extensible metadata: MCP descriptors, evals, prompt bundles
- **Locators** — Pointers to source code, images, or endpoints

</div>
<div class="card">

### Key properties
✓ Content-addressed (CID/SHA-256)
✓ OCI-based artifact storage
✓ Forward-compatible schema evolution
✓ Private extension support
✓ Sigstore-signed provenance
✓ Hierarchical skill taxonomy
✓ MCP & A2A format support

</div>
</div>

---

<!-- _class: light -->

# The ARIA metamodel

*Analogous to TOGAF's Architecture Content Framework — but for AI building blocks*

| Entity            | OASF Type            | Description                                    |
| ----------------- | -------------------- | ---------------------------------------------- |
| **Agent**         | Record (primary)     | Autonomous AI unit with identity and lifecycle |
| **Skill**         | Skill annotation     | Reusable capability (MCP, tools, APIs)         |
| **Instruction**   | Module (prompt)      | System prompts, guardrails, behavioral configs |
| **Knowledge**     | Module (knowledge)   | RAG corpora, embeddings, document collections  |
| **Orchestration** | Module (orch_config) | Routing, composition, agent mesh DAGs          |

**Key relationships:** Agent → invokes → Skill (1:N) · Agent → governed_by → Instruction (1:N) · Agent → grounded_in → Knowledge (1:N) · Orchestration → composed_by → Agent (1:N)

---

<!-- _class: light -->

# ARIA asset lifecycle

Every asset progresses through governed states — enforced by CI/CD and tracked in Purview

**Draft** → **Review** → **Published** → **Deprecated** → **Archived**

| Transition             | Governance gate                                                     |
| ---------------------- | ------------------------------------------------------------------- |
| Draft → Review         | OASF schema validation passes; governance overlay present           |
| Review → Published     | CODEOWNERS approval; sensitivity ceiling check; eval thresholds met |
| Published → Deprecated | Deprecation notice issued; consumer dependency scan completed       |
| Deprecated → Archived  | All consumers migrated; Purview retention policy activated          |

---

<!-- _class: light -->

# GitHub as ARIA marketplace

<div class="columns">
<div class="card">

### Repository structure
```
aria-assets/
├── oasf-record.json
├── oasf-governance.json
├── src/
├── tests/
└── .github/
    ├── CODEOWNERS
    └── workflows/
        ├── oasf-validate.yml
        ├── publish.yml
        └── purview-sync.yml
```

</div>
<div>

### CI/CD pipeline

<div class="card card-coral">

**PR opened** — OASF schema + governance + ceiling check

</div>
<div class="card card-teal">

**Merge to main** — Build OCI artifact → push to ACR → tag CID

</div>
<div class="card">

**Post-publish** — Purview label → Data Map lineage → DLP eval

</div>
</div>
</div>

---

<!-- _class: light -->

# Microsoft Purview integration

*Extending data governance into the AI asset domain*

<div class="columns-3">
<div class="card card-teal">

### Sensitivity labels
Labels propagate through agent dependency graph

</div>
<div class="card card-accent">

### DSPM for AI
AI Hub visibility, interaction telemetry, insider risk

</div>
<div class="card">

### Data map & lineage
AI assets as first-class entities with lineage edges

</div>
<div class="card card-coral">

### DLP enforcement
Block under-classified agents from sensitive data

</div>
<div class="card card-teal">

### Audit & compliance
All lifecycle events as first-class compliance events

</div>
<div class="card">

### Purview SDK
Embedded in Agent Framework — governance shifts left

</div>
</div>

---

<!-- _class: light -->

# Sensitivity label inheritance

*An agent inherits the highest sensitivity classification of any asset it depends on*

```
                    ┌─────────────────────────────┐
                    │   Onboarding Agent           │
                    │   ↑ Inherits: Confidential   │
                    └──────┬──────┬──────┬─────────┘
                           │      │      │
              ┌────────────┘      │      └────────────┐
              ▼                   ▼                    ▼
    ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐
    │ Policy Skill     │  │ HR Knowledge Base │  │ System Prompt   │
    │ Public           │  │ Confidential–PHI  │  │ Internal        │
    └─────────────────┘  └──────────────────┘  └─────────────────┘
```

**Enforced at CI time:** `oasf-validate` checks declared tier ≥ max(dependency tiers)
**Enforced at runtime:** Purview DLP prevents under-classified agents from accessing sensitive knowledge

---

<!-- _class: light -->

# Enterprise capability model

*Six capability domains for successful ARIA at scale*

<div class="columns-3">
<div class="card">

### Asset inventory
Discover, catalog, classify

</div>
<div class="card card-accent">

### Lifecycle management
Version, publish, deprecate

</div>
<div class="card card-teal">

### Composition
Agents from skills + knowledge

</div>
<div class="card card-coral">

### Governance
Labels, DLP, lineage, audit

</div>
<div class="card">

### Discovery & reuse
Search, filter, inner-source

</div>
<div class="card card-accent">

### Observability
OTEL telemetry + evaluations

</div>
</div>

---

<!-- _class: light -->

# ARIA Package Manager (`aria`)

*The "last mile" — from registry to runtime*

| Command        | Description                                             |
| -------------- | ------------------------------------------------------- |
| `aria search`  | Discover assets by OASF skill, domain, or keyword       |
| `aria inspect` | Display OASF Record and governance overlay              |
| `aria audit`   | Validate sensitivity ceiling + consumer + dependencies  |
| `aria install` | Pull OCI artifact, enforce governance, wire into target |
| `aria list`    | List installed assets with version and sensitivity      |

**Install targets:** Claude Desktop · VS Code · Agent Framework (local/A2A)
**Governance:** Every install validates ceiling → consumer → dependencies → Purview audit

---

<!-- _class: light -->

# AI FinOps: Cost governance for AI assets

*"Are we allowed to use this?" and "Can we afford to use this?" — same framework, same overlay*

<div class="columns">
<div>

### Cost governance overlay

```json
{
  "cost_governance": {
    "budget_monthly_usd": 500,
    "budget_alert_threshold_pct": 80,
    "cost_center": "CC-4200",
    "chargeback_model": "per_invocation",
    "rate_limits": {
      "daily_invocations": 10000,
      "monthly_tokens": 5000000
    }
  }
}
```

</div>
<div>

### What it enables

<div class="card card-coral">

**Budget enforcement** — middleware blocks invocations when budget is exceeded, just like sensitivity ceiling blocks

</div>
<div class="card card-teal">

**Cost inheritance** — orchestration cost = sum of composed agent costs, following the OASF dependency graph

</div>
<div class="card card-accent">

**Cross-provider rollup** — Azure OpenAI + GitHub Copilot + Container Apps → single per-asset cost view

</div>
</div>
</div>

---

<!-- _class: light -->

# Distribution gateway: Registry to end user

*OCI is the governance layer. The gateway is the user-facing layer.*

<div class="columns">
<div>

### The problem
Non-technical users don't have Docker or ORAS. They need a **one-click experience** through the platforms they already use.

### The solution
A thin Catalog API that sits between OCI and end-user platforms:

- Authenticates via **Entra ID**
- Filters catalog by **governance policy**
- Packages OCI artifacts as **`.mcpb` bundles**
- Logs installs to **Purview audit trail**

</div>
<div>

### Consumption channels

<div class="card">

**Claude Desktop** — organization's ARIA catalog appears in the Extensions panel. Click "Add to Claude." Governance runs transparently.

</div>
<div class="card card-teal">

**Cowork** — contextual suggestions based on OASF domain matching. Blocked assets are never surfaced.

</div>
<div class="card card-accent">

**Web Portal** — browse, filter, request access. For business users, governance teams, and auditors.

</div>
</div>
</div>

---

# Key takeaways

**01** AI assets need the same architectural rigor that TOGAF brought to enterprise IT. The metamodel is the foundation.

**02** OASF provides a community-driven, extensible taxonomy purpose-built for classifying AI primitives.

**03** GitHub delivers the marketplace infrastructure — versioning, PR workflows, Actions, and OCI registry — with zero greenfield build.

**04** Microsoft Purview extends existing data governance into the AI domain: sensitivity labels, DLP, lineage, and audit.

**05** The governed path must be the easiest path. Non-technical users get one-click installs in Claude Desktop; governance runs invisibly behind it.

---

<!-- _class: title -->

# Thank you

**Josh Garverick** | Chief Technical Architect, Xebia

*ARIA · OASF · GitHub · Microsoft Purview*

docs.agntcy.org/oasf | schema.oasf.outshift.com
