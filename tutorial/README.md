# ARIA Tutorial

Step-by-step guide to understanding, deploying, and using the ARIA framework.

## Contents

| Module | Description | Time |
|--------|-------------|------|
| [01 — Concepts](./01-concepts.md) | ARIA metamodel, entity types, OASF classification | 15 min |
| [02 — Marketplace setup](./02-marketplace-setup.md) | Deploy Terraform infrastructure (GitHub + Azure) | 30 min |
| [03 — Your first asset](./03-first-asset.md) | Create, govern, and publish an OASF-governed skill | 20 min |
| [04 — Purview integration](./04-purview-integration.md) | Configure sensitivity labels and DLP enforcement | 20 min |
| [05 — Sample agent](./05-sample-agent.md) | Run the onboarding assistant with governance middleware | 15 min |
| [06 — ARIA CLI](./06-aria-cli.md) | Discover, audit, and install assets into runtimes | 15 min |
| [07 — Conference demo](./07-demo-script.md) | Live demo script for conference presentation | 10 min |

## Prerequisites

- Docker Desktop (for devcontainer) or manual install of Pandoc, Marp, .NET 9, Terraform
- Azure subscription with Contributor access
- GitHub organization with admin access
- GitHub PAT with `admin:org`, `repo`, `workflow` scopes

## Quick Start

```bash
# Clone and open in VS Code with devcontainer
git clone https://github.com/xebia/aria.git
cd aria
code .
# VS Code will prompt to reopen in devcontainer

# Build everything
make all

# Or build individually
make docs      # PDF + DOCX reference architecture
make slides    # PPTX + PDF + HTML slide deck
make build     # .NET sample agent + ARIA CLI
```
