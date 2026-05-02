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
| `aria whoami`        | Resolve Entra identity and show effective sensitivity/Purview access             |
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
    "tenant_id": "your-tenant-id",
    "required_roles_by_sensitivity": {
      "confidential": ["Data Reader"],
      "highly_confidential": ["Data Curator"],
      "restricted": ["Data Source Administrator"]
    }
  },
  "auth": {
    "provider": "entra",
    "enable_experimental_providers": false
  },
  "entra": {
    "enabled": true,
    "tenant_id": "your-tenant-id",
    "scopes": ["https://management.azure.com/.default"]
  },
  "okta": {
    "enabled": false,
    "issuer": "https://your-org.okta.com/oauth2/default",
    "client_id": "your-okta-client-id",
    "scopes": ["openid", "profile", "groups"],
    "access_token_env_var": "OKTA_ACCESS_TOKEN",
    "access_token_file": "~/.aria/okta-token.txt",
    "token_endpoint": "https://your-org.okta.com/oauth2/default/v1/token",
    "client_secret_env_var": "OKTA_CLIENT_SECRET"
  },
  "auth0": {
    "enabled": false,
    "domain": "your-tenant.us.auth0.com",
    "client_id": "your-auth0-client-id",
    "audience": "https://api.example.com",
    "scopes": ["openid", "profile", "read:assets"]
  },
  "access_rules": [
    {
      "name": "hr-readers",
      "any_entra_groups": ["entra-group-hr-readers"],
      "sensitivity_ceiling": "confidential",
      "purview_roles": ["Data Reader"]
    },
    {
      "name": "governance-admins",
      "any_entra_roles": ["Governance.Admin"],
      "sensitivity_ceiling": "restricted",
      "purview_roles": ["Data Source Administrator", "Data Curator"]
    }
  ]
}
```

### Entra-Based Authorization Model

1. `aria` resolves `auth.provider` and uses the matching identity adapter (`entra`, `okta`, `auth0`).
2. Entra (default) acquires a token via `DefaultAzureCredential` and reads claims (`oid`, `tid`, `groups`, `roles`/`scp`).
3. Experimental providers (`okta`, `auth0`) require `auth.enable_experimental_providers=true`.
4. Okta currently supports JWT access token resolution from env/file and optional client-credentials token endpoint flow.
5. `access_rules` map normalized groups/roles to an effective sensitivity ceiling and granted Purview roles.
6. Governance checks enforce:
  - OASF sensitivity tier <= effective ceiling
  - `allowed_consumers`, `allowed_entra_groups`, and `allowed_entra_roles` from governance overlay
  - `purview.required_roles_by_sensitivity` for data-plane access

Run `aria whoami` to verify the resolved identity and access profile before running `aria audit` or `aria install`.

### Auth Adapter TODOs

Entra is first-class today, but the auth layer now has an adapter seam to support additional identity providers.

- TODO: Add `auth.provider` config selector (for example: `entra`, `okta`, `auth0`, `ping`).
- TODO: Implement `IIdentityProvider` adapters for Okta/Auth0/Ping with claim normalization into the shared identity model.
- TODO: Normalize group and role claim names per provider to a common shape before access rule evaluation.
- TODO: Add integration tests for each provider adapter using representative JWT payloads.
- TODO: Add provider capability matrix in docs (token source, group claim behavior, role claim behavior, tenant mapping).

## GitHub CLI Extension

For `gh` users, the same functionality is available as:

```bash
gh extension install jgarverick/gh-aria
gh aria search --skill "knowledge_retrieval/rag"
gh aria install jgarverick/aria-policy-lookup-skill --target claude-desktop
```

## Architecture

```
src/aria-auth-core/
  Models/             → Shared config + OASF policy/record models
  Services/           → Shared identity provider contracts, provider factory, governance/access policy
src/aria-cli/
  Program.cs          → System.CommandLine root + command tree
  Services/           → CLI provider adapters + OCI registry implementation
  Targets/            → Pluggable adapters (Claude Desktop, Agent Framework, VS Code)
```
