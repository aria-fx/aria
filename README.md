# ARIA — Asset Registry for Intelligent Agents

A reference architecture for enterprise AI asset management: classifying,
governing, and composing agents, skills, instructions, knowledge, and
orchestration configurations at scale.

**ARIA = OASF Classification + GitHub Marketplace + Microsoft Purview Governance**

## Repository Structure

```
aria/
├── .github/
│   └── workflows/
│       └── pages-microsite.yml  # Publish project microsite to GitHub Pages
├── .devcontainer/              # Dev environment (Pandoc, Marp, .NET 9, Terraform)
├── docs/
│   ├── architecture/           # Reference architecture (Markdown → PDF/DOCX via Pandoc)
│   │   └── aria-reference-architecture.md
│   ├── slides/                 # Conference deck (Markdown → PPTX/PDF via Marp)
│   │   ├── aria-deck.md
│   │   └── aria-theme.css
│   └── infographic/            # Leadership one-pager assets
├── src/
│   ├── terraform/              # Infrastructure-as-code (Azure + GitHub)
│   │   ├── modules/
│   │   │   ├── azure-governance/     # Purview, Key Vault, Storage, RBAC
│   │   │   ├── github-marketplace/   # Repos, teams, branch protection, workflows
│   │   │   └── oasf-validation/      # ACR, schema validation resources
│   │   ├── environments/dev/
│   │   └── workflows/                # GitHub Actions workflow references
│   ├── sample-agent/           # C# sample: Agent Framework + Purview SDK
│   │   ├── Oasf.Sample.Agent/       # Onboarding assistant runtime + OASF governance checks
│   │   └── Oasf.Sample.Agent.Tests/ # Sensitivity tier + governance tests
│   └── aria-cli/                # ARIA Package Manager CLI prototype
│       ├── Models/
│       ├── Services/
│       └── Targets/                  # Install adapters (Claude Desktop, VS Code, AF)
├── site/                       # Static microsite published via GitHub Pages
├── tutorial/                   # Step-by-step guide + conference demo script
├── Makefile                    # Build docs, slides, and .NET projects
└── README.md
```

## Quick Start

### With devcontainer (recommended)

```bash
git clone https://github.com/xebia/aria.git
cd aria
code .
# VS Code will prompt: "Reopen in Container" → click yes
# Pandoc, Marp, .NET 9, Terraform, ORAS all install automatically

make all
```

### Manual setup

```bash
# Prerequisites: pandoc, marp-cli, dotnet 9, terraform
npm install -g @marp-team/marp-cli

# Build everything
make all

# Or individually:
make docs       # Reference architecture → PDF + DOCX
make slides     # Conference deck → PPTX + PDF + HTML
make build      # .NET sample agent + ARIA CLI
make test       # Run .NET tests
```

## What's Inside

### Documents (`docs/`)

**Reference Architecture** — The full ARIA specification: metamodel (5 entity
types, 6 relationship types, lifecycle states), capability model, GitHub
marketplace design with OASF manifests, Microsoft Purview integration,
ARIA Package Manager concept, and implementation roadmap.

Source: `docs/architecture/aria-reference-architecture.md`
Build: `make docs` → `out/docs/aria-reference-architecture.pdf` + `.docx`

**Conference Deck** — 13-slide presentation covering the problem, three-layer
architecture, OASF overview, metamodel, lifecycle, GitHub marketplace,
Purview integration, sensitivity inheritance, capability model, ARIA CLI,
and key takeaways.

Source: `docs/slides/aria-deck.md`
Build: `make slides` → `out/slides/aria-deck.pptx` + `.pdf` + `.html`

### Infrastructure (`src/terraform/`)

Three Terraform modules provisioning the complete ARIA infrastructure:

- **azure-governance** — Purview Account, Key Vault, Storage, RBAC
- **github-marketplace** — Template repo, sample asset repo, teams, branch
  protection, CODEOWNERS, Actions workflows, org secrets
- **oasf-validation** — Azure Container Registry, schema validation storage

### Sample Agent (`src/sample-agent/`)

Working C# application on Microsoft Agent Framework 1.3 demonstrating:

- OASF Record + Governance Overlay loaded and validated at startup
- Purview integration using `WithPurview(...)` on the agent builder
- `OasfGovernanceMiddleware` helper for consumer validation and OASF telemetry
- Interactive console runtime with policy-aware request handling

### Microsite (`site/`)

A lightweight static microsite for ARIA is published via GitHub Pages using:

- Workflow: `.github/workflows/pages-microsite.yml`
- Content root: `site/`
- Trigger: push to `main` or `master` (plus manual dispatch)

### ARIA CLI (`src/aria-cli/`)

ARIA Package Manager prototype: `search`, `inspect`, `audit`, `install`, `list`
with pluggable install targets (Claude Desktop, VS Code, Agent Framework) and
governance enforcement at install time.

### Tutorial (`tutorial/`)

Seven-module walkthrough from concepts to live demo, including a conference
demo script with exact commands and talking points.

## The ARIA Framework

| Layer | Function | Implementation |
|-------|----------|----------------|
| **Metamodel** | Entity types, relationships, lifecycle states | OASF Records, Skills, Domains, Modules |
| **Marketplace** | Publish, discover, version, compose | GitHub repos + OCI registry + OASF manifests |
| **Governance** | Classify, label, enforce, audit | Microsoft Purview + OASF governance overlay |
| **Consumption** | Install, wire, validate at runtime | ARIA Package Manager (`aria`) CLI |

## License

MIT

## Author

Josh Garverick — Chief Technical Architect, Xebia
