# 05 — Sample Agent

Run the OASF-governed HR onboarding assistant and see governance
enforcement in action.

## What you'll see

- OASF Record and governance overlay validated at startup
- Purview middleware evaluating DLP policies on every interaction
- OASF governance middleware checking consumer identity
- Sensitivity ceiling enforcement blocking access to restricted data
- OpenTelemetry traces tagged with ARIA governance metadata

## Prerequisites

- .NET 9 SDK (installed by devcontainer)
- Azure OpenAI resource with a deployed model (or modify for OpenAI API)
- Completed [04 — Purview Integration](./04-purview-integration.md) (recommended)

## Step 1: Configure environment

Set the required environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
export PURVIEW_CLIENT_APP_ID="your-entra-app-id"
```

If you don't have Azure OpenAI access yet, you can still explore the
governance code — the startup validation and tool-level sensitivity
checks work independently of the LLM.

## Step 2: Inspect the OASF manifests

Before running, look at the two manifest files that govern this agent:

```bash
cat src/sample-agent/Oasf.Sample.Agent/oasf-record.json | jq .
```

Key things to notice:

- **name**: `aria.dev/agents/onboarding-assistant`
- **skills**: NLU intent classification, NLG text completion, and RAG
- **modules**: MCP server, prompt bundle, and knowledge base references
- **version**: `2.1.0` — semantic versioning for dependency resolution

```bash
cat src/sample-agent/Oasf.Sample.Agent/oasf-governance.json | jq .
```

Key things to notice:

- **sensitivity_tier**: `confidential` — this agent handles PII
- **dependency_sensitivity_ceiling**: `confidential` — it can't access
  anything above confidential
- **allowed_consumers**: `hr-team`, `onboarding-automation`, `hiring-managers`
- **audit_level**: `full` — every interaction is logged

## Step 3: Build and run

```bash
cd src/sample-agent
dotnet build
dotnet run --project Oasf.Sample.Agent
```

Watch the startup logs. You should see:

```
info: Oasf.Sample.Agent.Services.OasfGovernanceService
  Loading OASF Record from oasf-record.json
  Loading Governance Overlay from oasf-governance.json
  OASF validation passed
  OASF governance initialized: Asset=aria.dev/agents/onboarding-assistant v2.1.0,
    Sensitivity=confidential, Ceiling=confidential, Frameworks=[SOC2, GDPR]
```

Then the agent banner:

```
╔══════════════════════════════════════════════════════════════╗
║  OASF-Governed HR Onboarding Agent                         ║
║  Asset: aria.dev/agents/onboarding-assistant              ║
║  Version: 2.1.0                                            ║
║  Sensitivity: confidential                                 ║
║  Ceiling: confidential                                     ║
║  Purview DLP: Active                                       ║
╚══════════════════════════════════════════════════════════════╝
```

## Step 4: Test governance enforcement

Try these prompts to see the sensitivity ceiling in action:

### Allowed: internal data (below ceiling)

```
You> What is the PTO policy?
```

The `LookupPolicy` tool accesses `pto_policy` which is classified as
`internal`. Since `internal` < `confidential` (the ceiling), access is
granted and you get the policy content.

### Allowed: confidential data (at ceiling)

```
You> Tell me about the compensation bands
```

The tool accesses `compensation_bands` classified as `confidential`.
Since `confidential` = `confidential` (at the ceiling), access is granted.

### Blocked: restricted data (above ceiling)

```
You> Show me employee SSN records
```

The tool attempts to access `employee_ssn_records` classified as `restricted`.
Since `restricted` > `confidential` (exceeds ceiling), the tool returns:

```
Access denied: the 'employee_ssn_records' policy is classified as 'restricted'
which exceeds this agent's sensitivity ceiling of 'confidential'.
Contact the AI Governance team to request an elevated access level.
```

The agent then explains the restriction to the user.

### Other prompts to try

```
You> Generate a welcome message for Alice starting in Engineering on June 1
You> What's the onboarding checklist for the Marketing department?
```

These use the other tools (`GenerateWelcomeMessage`, `GetOnboardingChecklist`)
which don't access classified data and always succeed.

## Step 5: Understand the middleware pipeline

The agent processes every request through two middleware layers:

```
Request
  → OasfGovernanceMiddleware
      ✓ Extract consumer identity from request metadata
      ✓ Validate consumer against allowed_consumers list
      ✓ Start OTEL activity with ARIA tags
      ✓ Log at audit level (full = log everything)
  → PurviewPolicyMiddleware
      ✓ Evaluate prompt against Purview DLP policies
      ✓ Check sensitivity labels
      ✓ Log to Purview compliance audit
  → Agent execution
      ✓ LLM inference
      ✓ Tool invocation (tools enforce ceiling internally)
  → Response
```

If either middleware rejects the request, it returns immediately without
reaching the agent.

## Step 6: Run the tests

```bash
dotnet test
```

The tests verify:

- Sensitivity tier ordering (public < internal < ... < restricted)
- Ceiling comparison logic (exceeds vs. within)
- OASF Record deserialization
- Governance overlay deserialization
- Consumer allow-list semantics (empty = open access)

## Step 7: Inspect the telemetry

If you have an OpenTelemetry collector running, you'll see traces with
these ARIA-specific attributes:

| Attribute                    | Example value                          |
| ---------------------------- | -------------------------------------- |
| `oasf.asset.name`            | `aria.dev/agents/onboarding-assistant` |
| `oasf.asset.version`         | `2.1.0`                                |
| `oasf.sensitivity_tier`      | `confidential`                         |
| `oasf.audit_level`           | `full`                                 |
| `oasf.consumer.id`           | `hr-team`                              |
| `oasf.consumer.allowed`      | `true`                                 |
| `oasf.compliance_frameworks` | `SOC2,GDPR`                            |

These can be queried in your observability platform to answer questions
like "which agents accessed confidential data in the last 24 hours?"

## Key code to study

| File                                     | What it does                                     |
| ---------------------------------------- | ------------------------------------------------ |
| `Services/OasfGovernanceService.cs`      | Loads + validates manifests at startup           |
| `Middleware/OasfGovernanceMiddleware.cs` | Consumer validation + OTEL tags                  |
| `Tools/OnboardingTools.cs`               | Sensitivity ceiling check in `LookupPolicy`      |
| `Models/OasfModels.cs`                   | Strongly-typed OASF Record + Governance types    |
| `Program.cs`                             | Agent construction with dual middleware pipeline |

## Next Steps

→ [06 — ARIA CLI](./06-aria-cli.md)
