# ARIA — Asset Registry for Intelligent Agents

A reference architecture for enterprise AI asset management: classifying,
governing, and composing agents, skills, instructions, knowledge, and
orchestration configurations at scale.

**ARIA = [OASF](https://schema.oasf.outshift.com/) Classification + GitHub Marketplace + Microsoft Purview Governance**

## Repository Structure

```
aria/
├── .devcontainer/              # Dev environment (Pandoc, Marp, .NET 9, Terraform)
├── .github/                    # CI/CD, release notes templates, automation assets
│   ├── workflows/
│   └── release-notes/
├── docs/
│   ├── architecture/           # Reference architecture (Markdown → PDF/DOCX via Pandoc)
│   │   └── aria-reference-architecture.md
│   ├── brand/                  # Logo/avatar/favicons for docs and microsite
│   ├── slides/                 # Conference deck (Markdown → PPTX/PDF via Marp)
│   │   ├── aria-deck.md
│   │   └── aria-theme.css
│   └── infographic/            # Leadership one-pager assets
├── scripts/                    # Build helpers (Pandoc filters, transforms)
│   └── pandoc-md-links-to-html.lua
├── site/                       # Generated/static microsite artifacts for GitHub Pages
│   ├── index.html
│   ├── docs/
│   └── tutorial/
├── src/
│   ├── aria-auth-core/         # Shared auth and governance primitives (NuGet package)
│   ├── terraform/              # Infrastructure-as-code (Azure + GitHub)
│   │   ├── modules/
│   │   │   ├── azure-governance/     # Purview, Key Vault, Storage, RBAC
│   │   │   ├── github-marketplace/   # Repos, teams, branch protection, workflows
│   │   │   └── oasf-validation/      # ACR, schema validation resources
│   │   ├── environments/dev/
│   │   └── workflows/                # GitHub Actions workflow references
│   ├── sample-agent/           # C# sample: Agent Framework + Purview SDK
│   │   ├── Oasf.Sample.Agent/       # Onboarding assistant with governance middleware
│   │   └── Oasf.Sample.Agent.Tests/ # Sensitivity tier + governance tests
│   └── aria-cli/                # ARIA Package Manager CLI prototype + npm package assets
│       ├── Models/
│       ├── Services/
│       └── Targets/                  # Install adapters (Claude Desktop, VS Code, AF)
├── tutorial/                   # Step-by-step guide + conference demo script
├── Makefile                    # Build docs, slides, and .NET projects
└── README.md
```

## Quick Start

### With devcontainer (recommended)

```bash
git clone https://github.com/aria-fx/aria.git
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
make site       # Build microsite HTML for docs + tutorial
```

## What's Inside

### Documents (`docs/`)

**Reference Architecture** — The full ARIA specification: metamodel (5 entity
types, 6 relationship types, lifecycle states), capability model, GitHub
marketplace design with [OASF](https://schema.oasf.outshift.com/) manifests, Microsoft Purview integration,
ARIA Package Manager concept, and implementation roadmap.

Source: `docs/architecture/aria-reference-architecture.md`
Build: `make docs` → `out/docs/aria-reference-architecture.pdf` + `.docx`

**Conference Deck** — 13-slide presentation covering the problem, four-layer
architecture, [OASF](https://schema.oasf.outshift.com/) overview, metamodel, lifecycle, GitHub marketplace,
Purview integration, sensitivity inheritance, capability model, ARIA CLI,
and key takeaways.

Source: `docs/slides/aria-deck.md`
Build: `make slides` → `out/slides/aria-deck.pptx` + `.pdf` + `.html`

**Brand Assets** — Official ARIA logos, avatar, and favicon used in docs, slides,
and the microsite.

Source: `docs/brand/README.md`

### Infrastructure (`src/terraform/`)

Three Terraform modules provisioning the complete ARIA infrastructure:

- **azure-governance** — Purview Account, Key Vault, Storage, RBAC
- **github-marketplace** — Template repo, sample asset repo, teams, branch
  protection, CODEOWNERS, Actions workflows, org secrets
- **oasf-validation** — Azure Container Registry, schema validation storage

### Website / Docs Microsite (`site/`)

Pre-rendered HTML pages for architecture and tutorial content used by GitHub Pages.

- Architecture page: `site/docs/architecture.html`
- Tutorial hub: `site/tutorial/index.html`
- Rebuild locally: `make site` (or `make watch-site` for live rebuilds)

### Sample Agent (`src/sample-agent/`)

Working C# application on Microsoft Agent Framework 1.0 demonstrating:

- [OASF](https://schema.oasf.outshift.com/) Record + Governance Overlay loaded and validated at startup
- `PurviewPolicyMiddleware` for DLP enforcement
- Custom `OasfGovernanceMiddleware` for consumer validation and [OASF](https://schema.oasf.outshift.com/) telemetry
- Sensitivity ceiling enforcement in agent tools
- OpenTelemetry traces tagged with [OASF](https://schema.oasf.outshift.com/) metadata

### ARIA CLI (`src/aria-cli/`)

ARIA Package Manager prototype: `search`, `inspect`, `audit`, `install`, `list`
with pluggable install targets (Claude Desktop, VS Code, Agent Framework) and
governance enforcement at install time.

Includes a .NET CLI project plus npm packaging assets under `src/aria-cli/npm/`.

### Shared Auth Core (`src/aria-auth-core/`)

Reusable authentication, identity provider abstraction, and governance primitives
shared by ARIA clients and services.

- Package metadata and usage: `src/aria-auth-core/README.md`
- Change history: `src/aria-auth-core/CHANGELOG.md`
- Release process: `src/aria-auth-core/RELEASING.md`

### Tutorial (`tutorial/`)

Seven-module walkthrough from concepts to live demo, including a conference
demo script with exact commands and talking points.

## Release and CI

Primary workflows and automation assets:

- `.github/workflows/aria-auth-core-ci.yml`
- `.github/workflows/aria-auth-core-publish.yml`
- `.github/workflows/aria-cli-publish-packages.yml`
- `.github/workflows/pages-microsite.yml`

## The ARIA Framework

| Layer            | Function                                           | Implementation                                                        |
| ---------------- | -------------------------------------------------- | --------------------------------------------------------------------- |
| **Metamodel**    | Entity types, relationships, lifecycle states      | [OASF](https://schema.oasf.outshift.com/) Records, Skills, Domains, Modules                                |
| **Marketplace**  | Publish, discover, version, compose                | GitHub repos + OCI registry + [OASF](https://schema.oasf.outshift.com/) manifests                          |
| **Governance**   | Classify, label, enforce, audit                    | Microsoft Purview + [OASF](https://schema.oasf.outshift.com/) governance overlay                           |
| **Distribution** | End-user consumption, one-click installs           | Catalog API + .mcpb packager + Claude Desktop / Cowork integration    |
| **AI FinOps**    | Cost attribution, budgets, rate limits, chargeback | Cost collector skill + budget enforcement middleware + cost dashboard |
| **Consumption**  | Developer CLI, install validation                  | ARIA Package Manager (`aria`) CLI                                     |

## License

MIT

## Author

Josh Garverick — Chief Technical Architect, Xebia
