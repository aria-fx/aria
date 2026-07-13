# 06 — ARIA CLI

Discover, audit, and install ARIA-governed AI assets into your development
runtimes using the ARIA Package Manager.

## What you'll do

- Initialize the aria CLI with your consumer identity and registry
- Search for assets by [OASF](https://schema.oasf.outshift.com/) skill taxonomy
- Inspect an asset's [OASF](https://schema.oasf.outshift.com/) Record and governance overlay
- Audit governance compliance before installing
- Install an MCP skill into Claude Desktop
- Scaffold a curated preset bundle with one command
- See governance blocking an over-classified install

## Step 1: Build the CLI

```bash
cd src/aria-cli
dotnet build
```

Run it to see the command tree:

```bash
dotnet run -- --help
```

Output:

```
Description:
  aria — ARIA Package Manager for [OASF](https://schema.oasf.outshift.com/)-governed AI assets

Usage:
  aria [command] [options]

Commands:
  init       Initialize aria configuration in ~/.aria/config.json
  search     Discover AI assets by [OASF](https://schema.oasf.outshift.com/) skill, domain, or keyword
  inspect    Display [OASF](https://schema.oasf.outshift.com/) Record and governance overlay for an asset
  audit      Validate governance compliance before install
  install    Pull and install an AI asset into a target runtime
  scaffold   Install a curated preset bundle of AI assets
  list       List installed AI assets
```

## Step 2: Initialize configuration

```bash
dotnet run -- init
```

This creates `~/.aria/config.json` with default values. Edit it to match
your environment:

```bash
cat ~/.aria/config.json | jq .
```

The key fields:

```json
{
  "consumer_id": "my-team",
  "sensitivity_ceiling": "confidential",
  "registries": ["ghcr.io/my-org/aria-assets"],
  "targets": {
    "claude-desktop": {
      "config_path": "~/Library/Application Support/Claude/claude_desktop_config.json"
    },
    "agent-framework": {
      "project_path": "./src",
      "a2a_endpoint": "https://agents.myorg.com"
    },
    "vscode": {
      "workspace_path": "./.vscode/mcp.json"
    }
  }
}
```

The `consumer_id` identifies your team for allow-list checks. The
`sensitivity_ceiling` sets the maximum classification you're authorized
to install.

## Step 3: Search for assets

```bash
# Search by skill taxonomy
dotnet run -- search --skill "knowledge_retrieval/rag"

# Search by domain
dotnet run -- search --domain "human_resources"

# Combine filters
dotnet run -- search --skill "rag" --domain "human_resources"
```

You'll see a table of matching assets:

```
┌──────────────────────────────────────────┬─────────┬───────────────┬──────┬──────────────────────────┐
│ Name                                     │ Version │ Type          │ Skills│ Description             │
├──────────────────────────────────────────┼─────────┼───────────────┼──────┼──────────────────────────┤
│ aria.dev/agents/onboarding-assistant    │ 2.1.0   │ agent         │ rag  │ HR onboarding agent...  │
│ aria.dev/skills/policy-lookup           │ 1.0.0   │ mcp_server    │ rag  │ MCP server for HR...    │
│ aria.dev/knowledge/hr-policies          │ 3.0.0   │ knowledge_base│ rag  │ HR policy knowledge...  │
└──────────────────────────────────────────┴─────────┴───────────────┴──────┴──────────────────────────┘
```

## Step 4: Inspect an asset

Before installing, inspect the full [OASF](https://schema.oasf.outshift.com/) metadata:

```bash
dotnet run -- inspect ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0
```

This displays two panels:

- **[OASF](https://schema.oasf.outshift.com/) Record** — name, version, skills, domains, modules, authors
- **Governance Overlay** — sensitivity tier, classifications, ceiling,
  approval chain, allowed consumers, compliance frameworks

This is the governance-aware equivalent of `npm info` or `docker inspect`.

## Step 5: Audit governance

Check whether you're authorized to install an asset:

```bash
# This should PASS (confidential ≤ confidential)
dotnet run -- audit ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0 \
  --ceiling confidential
```

Output:

```
✓ All governance checks passed
  Asset tier: confidential
  Your ceiling: confidential
  Consumer: my-team
  Frameworks: SOC2, GDPR
```

Now try with a ceiling that's too low:

```bash
# This should FAIL (confidential > public)
dotnet run -- audit ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0 \
  --ceiling public
```

Output:

```
✗ Asset sensitivity 'confidential' exceeds your ceiling 'public'.
  Request elevated access from the AI Governance team.
  Required approvals: ai-governance → hr-data-steward
```

The CLI shows exactly what approvals you'd need to get access.

## Step 6: Install to a target

Install an MCP skill into Claude Desktop:

```bash
dotnet run -- install ghcr.io/jgarverick/aria-assets/policy-lookup-skill:1.0.0 \
  --target claude-desktop
```

The install flow:

```
aria install ghcr.io/jgarverick/aria-assets/policy-lookup-skill:1.0.0 → claude-desktop

1. Fetching [OASF](https://schema.oasf.outshift.com/) metadata...
   Asset: aria.dev/skills/policy-lookup v1.0.0
2. Validating governance...
   ✓ Sensitivity: internal ≤ confidential
   ✓ Consumer 'my-team' is authorized
3. Pulling OCI artifact...
   Cached to: ~/.aria/cache/aria.dev-skills-policy-lookup
4. Installing to claude-desktop...
   Registering MCP server 'policy-lookup' in Claude Desktop
     Transport: stdio
     Tools: lookup_policy, list_policies
   Config: ~/Library/Application Support/Claude/claude_desktop_config.json

✓ Successfully installed aria.dev/skills/policy-lookup v1.0.0 → claude-desktop
```

For Agent Framework:

```bash
dotnet run -- install ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0 \
  --target agent-framework
```

This registers the agent as a remote A2A endpoint instead of modifying
a Claude Desktop config file.

## Step 7: Scaffold a preset bundle

Some workflows need several related assets. `aria scaffold` installs a
curated bundle in one command, running the same governance-enforced
install flow for each asset:

```bash
dotnet run -- scaffold --preset usage-eval --target claude-desktop
```

The `usage-eval` preset (alias `provider-usage-evaluator`) installs the
provider/cloud usage evaluation bundle — four MCP skills
(`usage-ingest-normalize`, `usage-eval-metrics`, `usage-conformance`,
`usage-reporting`) plus the `provider-usage-evaluator` agent. Each asset
is fetched, governance-validated, pulled, and installed individually,
then a summary table shows per-asset status:

```
✓ Scaffold complete: 5/5 assets installed
```

Useful variations:

```bash
# Skills only — skip the agent asset
dotnet run -- scaffold --preset usage-eval --target claude-desktop --skills-only

# Re-running is idempotent: cached artifacts are overwritten in place
dotnet run -- scaffold --preset usage-eval --target claude-desktop
```

If any asset is blocked by governance (e.g. your ceiling is below
`confidential`), the scaffold continues through the remaining assets,
reports every failure in the summary table, and exits non-zero.

## Step 8: List installed assets

```bash
dotnet run -- list
```

Shows all installed assets with their governance metadata:

```
┌─────────────────────────────────┬─────────┬────────────┬─────────────────┬──────────────┬────────────┐
│ Name                            │ Version │ Type       │ Target          │ Sensitivity  │ Installed  │
├─────────────────────────────────┼─────────┼────────────┼─────────────────┼──────────────┼────────────┤
│ aria.dev/skills/policy-lookup  │ 1.0.0   │ mcp_server │ claude-desktop  │ internal     │ 2026-05-01 │
└─────────────────────────────────┴─────────┴────────────┴─────────────────┴──────────────┴────────────┘
```

## How the install targets work

The CLI uses pluggable adapters for different runtimes:

| Target              | What it does                                                                                                                                                         |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **claude-desktop**  | Reads the [OASF](https://schema.oasf.outshift.com/) module descriptor, generates a `claude_desktop_config.json` entry with command, args, and transport, then writes it to the Claude Desktop config path |
| **vscode**          | Similar to Claude Desktop but targets `.vscode/mcp.json` in the workspace                                                                                            |
| **agent-framework** | For MCP modules, registers as a tool provider. For agent Records, registers as a remote A2A endpoint using the `a2a_endpoint` from config                            |

Adding a new target is straightforward — implement `IInstallTarget` with
a `Name` and `InstallAsync` method, then register it in `TargetRegistry`.

## GitHub CLI extension

For teams standardized on `gh`, the same commands are available as:

```bash
gh extension install jgarverick/gh-aria
gh aria search --skill "knowledge_retrieval/rag"
gh aria install jgarverick/aria-policy-lookup-skill --target claude-desktop
gh aria audit --pr 42 --ceiling confidential
```

The `--pr` flag on audit is particularly useful during code review —
it validates the governance overlay in a pull request before approval.

## Next Steps

→ [07 — Conference Demo Script](./07-demo-script.md)
