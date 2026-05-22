# aria — ARIA Package Manager

A command-line interface for discovering, validating, and installing
[OASF](https://schema.oasf.outshift.com/)-governed AI assets from OCI registries into target runtimes.

## Overview

`aria` is the "last mile" tool that connects the Enterprise AI Asset
Management marketplace (GitHub + OCI registry + [OASF](https://schema.oasf.outshift.com/) classification)
to developer workstations and production runtimes. It bridges the gap
between "assets exist in a registry" and "assets are wired into my
agent runtime and governed by policy."

## Commands

| Command              | Description                                                                      |
| -------------------- | -------------------------------------------------------------------------------- |
| `aria init`          | Initialize `~/.aria/config.json` with consumer identity, registries, and targets |
| `aria search`        | Discover assets by [OASF](https://schema.oasf.outshift.com/) skill taxonomy, domain, or keyword                       |
| `aria inspect <ref>` | Display [OASF](https://schema.oasf.outshift.com/) Record and governance overlay without installing                    |
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

## Multi-registry search behavior

- `aria search` fans out discovery to each configured entry in `registries`.
- Search output includes source registry and governance state (`governed`, `ungoverned`, `governance_unreachable`).
- Results are merged and deduplicated by canonical asset key (`name` + `version` + module ref/locator), preferring the highest `registry_policies.<source>.priority`.
- Output order is deterministic for stable UX and CI assertions.
- If one registry fails, results from healthy registries still return.
- Use `aria search --verbose` to see per-registry diagnostics (status, counts, and errors).
- If no registries are configured, `aria search` prints guidance to run `aria init` or update `~/.aria/config.json`.

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
  "registry_policies": {
    "ghcr.io/jgarverick/aria-assets": {
      "trust_tier": "internal_governed",
      "require_governance_overlay": true,
      "priority": 100
    },
    "ghcr.io/public/aria-assets": {
      "trust_tier": "public_curated",
      "require_governance_overlay": false,
      "max_sensitivity_if_ungoverned": "internal",
      "priority": 10
    }
  },
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

### Authentication Providers

`aria` supports multiple identity providers for resolving user identity and access policies. Configure the provider using `auth.provider` in `~/.aria/config.json`.

#### Entra (Default)

Microsoft Entra ID (formerly Azure AD). Resolves identity via `DefaultAzureCredential` (supports managed identity, service principal, user login).

- **Configuration**: `entra` section
- **Scopes**: Configurable (default: Azure management API)
- **Groups/Roles**: Reads `groups` and `roles`/`scp` claims from token
- **Tenant ID**: Resolved from token `tid` claim

```bash
auth.provider = "entra"
auth.enable_experimental_providers = false
```

#### Okta (Experimental)

Okta identity platform. Supports multiple token acquisition methods.

- **Enable**: Set `auth.enable_experimental_providers = true`
- **Configuration**: `okta` section
- **Token Acquisition** (tried in order):
  1. Environment variable (default: `OKTA_ACCESS_TOKEN`)
  2. File-based token (`access_token_file`, supports `~` expansion)
  3. Client credentials flow to `token_endpoint` (requires `client_secret_env_var`)
- **Groups/Roles**: Reads `groups`, `okta.groups`, `roles`, `permissions` claims; parses space/comma-separated scopes
- **Tenant ID**: Configured `issuer` or resolved from token `tid`/`iss`

**Configuration Example**:
```json
{
  "auth": {
    "provider": "okta",
    "enable_experimental_providers": true
  },
  "okta": {
    "enabled": true,
    "issuer": "https://your-org.okta.com/oauth2/default",
    "client_id": "your-okta-client-id",
    "scopes": ["openid", "profile", "groups"],
    "access_token_env_var": "OKTA_ACCESS_TOKEN",
    "access_token_file": "~/.aria/okta-token.txt",
    "token_endpoint": "https://your-org.okta.com/oauth2/default/v1/token",
    "client_secret_env_var": "OKTA_CLIENT_SECRET"
  }
}
```

**Usage**:
```bash
# Provide token via environment variable
export OKTA_ACCESS_TOKEN=<your-jwt>
aria whoami

# Or provide token in file for CI/CD
aria whoami

# Or use client credentials flow (will request token automatically)
export OKTA_CLIENT_SECRET=<your-secret>
aria whoami
```

#### Auth0 (Experimental)

Auth0 identity platform. Supports interactive device flow for CLI and static tokens for CI/CD.

- **Enable**: Set `auth.enable_experimental_providers = true`
- **Configuration**: `auth0` section
- **Token Acquisition** (tried in order):
  1. Environment variable (`AUTH0_ACCESS_TOKEN`)
  2. File-based token (`access_token_file`, supports `~` expansion)
  3. Device flow (requires `domain` and `client_id`; `audience` is optional and defaults to `https://{domain}/api/v2/`)
- **Device Flow**: Prompts user to visit browser URL and enter code; polls for token
- **Groups/Roles**: Reads `groups`, `https://aria.dev/groups`, `roles`, `permissions`, `scope` claims; parses space/comma-separated scopes
- **Tenant ID**: Configured `domain` or resolved from token `aud` (string or first array element)/`iss`

**Configuration Example**:
```json
{
  "auth": {
    "provider": "auth0",
    "enable_experimental_providers": true
  },
  "auth0": {
    "enabled": true,
    "domain": "your-tenant.us.auth0.com",
    "client_id": "your-auth0-client-id",
    "audience": "https://api.example.com",
    "scopes": ["openid", "profile", "read:assets", "offline_access"],
    "access_token_file": "~/.aria/auth0-token.txt"
  }
}
```

**Usage**:
```bash
# Interactive device flow (first-time setup)
aria whoami
# Output: Visit https://your-tenant.us.auth0.com/activate?user_code=XXXX

# Provide token via environment variable (CI/CD)
export AUTH0_ACCESS_TOKEN=<your-jwt>
aria whoami

# Or provide token in file
aria whoami
```

#### Claim Normalization

All providers normalize identity claims to a consistent model:

| Provider | Object ID Claim  | Tenant ID Claim | Groups Claims  | Roles Claims |
|----------|------------------|-----------------|----------------|-------------|
| Entra    | `oid`            | `tid`           | `groups`       | `roles`, `scp` |
| Okta     | `oid`/`uid`/`sub` | `tid`/configured issuer/`iss` | `groups`, `okta.groups` | `roles`, `permissions`, `scp`, `scope` |
| Auth0    | `sub`/`oid`/`uid` | `aud`/`iss`/configured domain | `groups`, `https://aria.dev/groups` | `roles`, `permissions`, `scope` |

Each provider:
- Tries multiple claim names to find identity (for compatibility across Auth0/Okta config variants)
- Supports array claims and space/comma-separated string values for groups/roles
- Normalizes to `HashSet<string>` for consistent access rule evaluation

Run `aria whoami` to verify the resolved identity and access profile before running `aria audit` or `aria install`.

### Authorization and Access Rules

1. `aria` resolves `auth.provider` and uses the matching identity adapter (`entra`, `okta`, `auth0`).
2. The adapter extracts normalized identity: ObjectId, TenantId, UserPrincipalName, Groups, Roles.
3. `access_rules` map normalized groups/roles to an effective sensitivity ceiling and granted Purview roles.
4. Governance checks enforce:
  - [OASF](https://schema.oasf.outshift.com/) sensitivity tier <= effective ceiling
  - `allowed_consumers`, `allowed_entra_groups`, and `allowed_entra_roles` from governance overlay
  - `purview.required_roles_by_sensitivity` for data-plane access
  - Per-source registry policy (`trust_tier`, `require_governance_overlay`, `max_sensitivity_if_ungoverned`, `priority`)

### Auth Adapter Implementation Status

- ✅ Entra adapter (first-class, production-ready)
- ✅ Okta adapter (experimental, supports JWT + client credentials)
- ✅ Auth0 adapter (experimental, supports device flow + JWT + file-based tokens)
- ✅ Claim normalization across all providers
- ✅ Unit tests for each provider

Future work:
- Add provider capability matrix docs (token source, group claim behavior, role claim behavior, tenant mapping)
- Support additional providers (Ping Identity, custom OIDC, SAML)

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
  Models/             → Shared config + [OASF](https://schema.oasf.outshift.com/) policy/record models
  Services/           → Shared identity provider contracts, provider factory, governance/access policy
src/aria-cli/
  Program.cs          → System.CommandLine root + command tree
  Services/           → CLI provider adapters + OCI registry implementation
  Targets/            → Pluggable adapters (Claude Desktop, Agent Framework, VS Code)
```
