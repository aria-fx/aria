# aria — ARIA Package Manager

A command-line interface for discovering, validating, and installing
OASF-governed AI assets from OCI registries into target runtimes.

## Overview

`aria` is the "last mile" tool that connects the Enterprise AI Asset
Management marketplace (GitHub + OCI registry + OASF classification)
to developer workstations and production runtimes. It bridges the gap
between "assets exist in a registry" and "assets are wired into my
agent runtime and governed by policy."

## Commands

| Command              | Description                                                                      |
| -------------------- | -------------------------------------------------------------------------------- |
| `aria init`          | Initialize `~/.aria/config.json` with consumer identity, registries, and targets |
| `aria search`        | Discover assets by OASF skill taxonomy, domain, or keyword                       |
| `aria inspect <ref>` | Display OASF Record and governance overlay without installing                    |
| `aria audit <ref>`   | Validate governance (ceiling, consumer, dependencies) before install             |
| `aria install <ref>` | Pull OCI artifact, enforce governance, install to target runtime                 |
| `aria list`          | List installed assets with version, sensitivity, and target                      |

## Install Targets

| Target            | Module Type      | Behavior                                                 |
| ----------------- | ---------------- | -------------------------------------------------------- |
| `claude-desktop`  | `mcp_server`     | Adds MCP server entry to `claude_desktop_config.json`    |
| `vscode`          | `mcp_server`     | Adds MCP server to `.vscode/mcp.json` workspace config   |
| `agent-framework` | `mcp_server`     | Registers as MCP tool provider in Agent Framework        |
| `agent-framework` | Record (agent)   | Registers as remote A2A endpoint or local agent instance |
| `agent-framework` | `knowledge_base` | Registers with agent's RAG pipeline configuration        |

## Quick Start

```bash
# Build the CLI
dotnet build src/aria-cli/Aria.Cli.csproj

# Initialize config
dotnet run --project src/aria-cli/Aria.Cli.csproj -- init

# Search for HR-related RAG skills
dotnet run --project src/aria-cli/Aria.Cli.csproj -- search --skill "knowledge_retrieval/rag" --domain "human_resources"

# Inspect an asset before installing
dotnet run --project src/aria-cli/Aria.Cli.csproj -- inspect ghcr.io/jgarverick/aria-assets/policy-lookup-skill:1.0.0

# Audit governance compliance
dotnet run --project src/aria-cli/Aria.Cli.csproj -- audit ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0 --ceiling confidential

# Install an MCP skill into Claude Desktop
dotnet run --project src/aria-cli/Aria.Cli.csproj -- install ghcr.io/jgarverick/aria-assets/policy-lookup-skill:1.0.0 --target claude-desktop

# Install an agent into Agent Framework via A2A
dotnet run --project src/aria-cli/Aria.Cli.csproj -- install ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0 --target agent-framework
```

## Install as Global Tool

```bash
dotnet pack src/aria-cli/Aria.Cli.csproj -o ./nupkg
dotnet tool install --global --add-source ./nupkg aria
aria search --skill "knowledge_retrieval/rag"
```

## Governance Enforcement

Every `aria install` runs these checks before pulling content:

1. **Sensitivity ceiling** — asset tier ≤ consumer ceiling
2. **Consumer allow-list** — consumer identity in `allowed_consumers`
3. **Dependency scan** — recursive governance check on all module refs
4. **Purview audit** — install event logged to Purview audit trail

If any check fails, the install is blocked and the required approval
chain is displayed.

## Configuration

`~/.aria/config.json`:

```json
{
  "consumer_id": "hr-team",
  "sensitivity_ceiling": "confidential",
  "registries": ["ghcr.io/jgarverick/aria-assets"],
  "targets": {
    "claude-desktop": {
      "config_path": "~/Library/Application Support/Claude/claude_desktop_config.json"
    },
    "agent-framework": {
      "project_path": "./src/Oasf.Sample.Agent",
      "a2a_endpoint": "https://agents.aria.dev"
    }
  },
  "purview": {
    "account": "purview-aria-dev",
    "tenant_id": "your-tenant-id"
  }
}
```

## GitHub CLI Extension

For `gh` users, the same functionality is available as:

```bash
gh extension install jgarverick/gh-aria
gh aria search --skill "knowledge_retrieval/rag"
gh aria install jgarverick/aria-policy-lookup-skill --target claude-desktop
```

## Architecture

```
Program.cs           → System.CommandLine root + command tree
Services/
  OciRegistryService → OCI artifact fetch (Azure.Containers.ContainerRegistry / oras)
  GovernanceService  → Sensitivity ceiling + consumer + dependency validation
Targets/
  InstallTargets     → Pluggable adapters (Claude Desktop, Agent Framework, VS Code)
Models/
  AriaConfig          → CLI config, OASF Record/Governance types, SensitivityTiers
```
