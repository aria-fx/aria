# OASF-Governed Agent Sample — Microsoft Agent Framework + Purview

This sample demonstrates the ARIA reference architecture as a working
C# runtime. It bootstraps an HR onboarding agent with OASF governance
validation, consumer allow-list checks, and Purview policy integration
using Microsoft Agent Framework 1.3 APIs.

## What This Demonstrates

### Reference Architecture Concepts in Code

| Concept                 | Implementation                                                                           |
| ----------------------- | ---------------------------------------------------------------------------------------- |
| **OASF Record**         | `oasf-record.json` — skills, domains, modules, locators declared per OASF schema         |
| **Governance Overlay**  | `oasf-governance.json` — sensitivity tier, DLP classification, consumer allow-list       |
| **Purview DLP**         | `WithPurview(...)` on `AIAgentBuilder` — policy-aware middleware in the agent pipeline   |
| **OASF Enforcement**    | `OasfGovernanceMiddleware` helper — consumer allow-list + OASF telemetry tags            |
| **Sensitivity Ceiling** | `OnboardingTools.LookupPolicy()` — blocks access to data above the agent's declared tier |
| **OTEL Observability**  | OASF activity tags emitted during governance checks                                      |
| **Startup Validation**  | `OasfGovernanceService` — mirrors the `oasf-validate` GitHub Action at runtime           |

### Runtime Flow

```
Input → OasfGovernanceMiddleware helper → AIAgent (WithPurview) → LLM response
     ↓                               ↓
   Consumer allow-list               Purview content policy evaluation
   OASF telemetry tags               Compliance-aware response pipeline
```

### Sensitivity Ceiling in Action

The sample knowledge base contains policies at different classification levels:

| Policy                 | Sensitivity    | Agent Ceiling: `confidential` |
| ---------------------- | -------------- | ----------------------------- |
| `pto_policy`           | `internal`     | ✅ Allowed                     |
| `benefits_overview`    | `internal`     | ✅ Allowed                     |
| `compensation_bands`   | `confidential` | ✅ Allowed (at ceiling)        |
| `employee_ssn_records` | `restricted`   | ❌ Blocked                     |

When the agent attempts to access `employee_ssn_records`, the `LookupPolicy` tool
checks the governance overlay's `dependency_sensitivity_ceiling` and returns an
access denied message instead of the data.

## Prerequisites

- .NET 9.0 SDK
- OpenAI API key
- Optional: Microsoft Purview tenant/config for policy checks

## Quick Start

```bash
# From repo root
cd src/sample-agent

# Required
export OPENAI_API_KEY="<your-openai-api-key>"

# Optional (defaults shown)
export OPENAI_MODEL="gpt-4o-mini"
export OPENAI_ENDPOINT="https://your-endpoint.example.com"
export PURVIEW_TENANT_ID="<tenant-guid>"
export CONSUMER_ID="hr-team"

# Build and run
dotnet restore
dotnet build
dotnet run --project Oasf.Sample.Agent/Oasf.Sample.Agent.csproj

# Run tests
dotnet test Oasf.Sample.sln
```

When running, type prompts directly into the console and enter `quit` to exit.

## Project Structure

```
src/sample-agent/
├── Oasf.Sample.Agent/
│   ├── Program.cs                       # Interactive runtime: OpenAI + AIAgent + Purview
│   ├── Middleware/
│   │   └── OasfGovernanceMiddleware.cs # Consumer allow-list checks + OASF telemetry
│   ├── Models/
│   │   └── OasfModels.cs                # Strongly-typed OASF Record + Governance Overlay
│   ├── Services/
│   │   └── OasfGovernanceService.cs     # Startup validation + runtime policy provider
│   ├── Tools/
│   │   └── OnboardingTools.cs           # Reference tool implementations with ceiling checks
│   ├── oasf-record.json                 # OASF Record manifest
│   ├── oasf-governance.json             # Governance overlay
│   └── appsettings.json                 # Logging configuration
└── Oasf.Sample.Agent.Tests/
  └── GovernanceTests.cs               # Sensitivity tier + governance model tests
```

## Key Integration Points

### Microsoft Agent Framework 1.3

- `AsAIAgent(...)` — creates an `AIAgent` from an OpenAI chat client
- `AIAgentBuilder` — composes middleware/decorators around the base agent
- `RunAsync(...)` — executes interactive requests in the console loop

### Microsoft Purview SDK

- `WithPurview(...)` — injects Purview policy enforcement into the agent pipeline
- `PurviewSettings` — configures app identity, tenant, and policy behavior

### OASF Governance

- `OasfGovernanceService` — loads and validates OASF Record + Governance Overlay
- `OasfGovernanceMiddleware` — enforces consumer allow-lists and emits OASF telemetry
- `SensitivityTiers` — ordered tier model for inheritance ceiling checks
- Tool-level sensitivity gating reference in `OnboardingTools.LookupPolicy()`
