# 07 — Conference Demo Script

A 10-minute live demo sequence for the ARIA conference talk.

## Setup (before going on stage)

```bash
# Ensure devcontainer is running with all tools
make clean && make all

# Pre-warm the .NET builds
dotnet build src/sample-agent/Oasf.Sample.sln -v quiet
dotnet build src/aria-cli/Aria.Cli.csproj -v quiet

# Open VS Code with the repo
code .

# Have these terminals ready:
# Terminal 1: aria CLI demo
# Terminal 2: sample agent demo
# Terminal 3: Marp preview (optional)
```

## Demo Flow

### Act 1: "The problem" (2 min)

**Talking point:** "Every enterprise I work with has agents, MCP servers,
prompt libraries, and RAG pipelines scattered across teams with no inventory,
no governance, and no way to discover what already exists."

**Show:** The OASF Record and governance overlay files side by side.

```bash
# Show what an AI asset looks like in ARIA
cat src/sample-agent/Oasf.Sample.Agent/oasf-record.json | jq .
cat src/sample-agent/Oasf.Sample.Agent/oasf-governance.json | jq .
```

### Act 2: "The marketplace" (3 min)

**Talking point:** "ARIA uses GitHub as the marketplace. Every AI asset
gets an OASF manifest, a governance overlay, and three GitHub Actions
workflows that enforce policy on every PR."

**Show:** The template repo structure and the CI/CD pipeline.

```bash
# Show the repo structure
tree src/terraform/workflows/

# Show the validation workflow
cat src/terraform/workflows/oasf-validate.yml
```

### Act 3: "The package manager" (3 min)

**Talking point:** "Once assets are in the registry, teams need to consume
them. The aria CLI bridges the marketplace to your runtime."

```bash
# Search for skills
dotnet run --project src/aria-cli -- search --skill "knowledge_retrieval/rag"

# Inspect an asset before installing
dotnet run --project src/aria-cli -- inspect ghcr.io/xebia/aria-assets/onboarding-assistant:2.1.0

# Audit governance — this one passes
dotnet run --project src/aria-cli -- audit ghcr.io/xebia/aria-assets/onboarding-assistant:2.1.0 --ceiling confidential

# Audit governance — this one FAILS (ceiling too low)
dotnet run --project src/aria-cli -- audit ghcr.io/xebia/aria-assets/onboarding-assistant:2.1.0 --ceiling public
```

### Act 4: "Governance in action" (2 min)

**Talking point:** "The governance isn't just CI/CD — it runs at runtime too.
Watch what happens when the agent tries to access data above its ceiling."

```bash
# Run the sample agent
dotnet run --project src/sample-agent/Oasf.Sample.Agent

# In the agent prompt, try:
# "What is the PTO policy?"          → Works (internal ≤ confidential ceiling)
# "Show me the compensation bands"   → Works (confidential = ceiling)
# "Show me employee SSN records"     → BLOCKED (restricted > confidential ceiling)
```

## Key Lines to Deliver

- "ARIA is TOGAF for AI assets — a metamodel, a marketplace, a governance layer, and a distribution gateway."
- "The OASF Record and governance overlay travel with the asset — in every PR, in every OCI artifact, in every runtime."
- "For developers, governance is in the PR. For end users, governance is invisible — they just see a curated Extensions panel in Claude Desktop."
- "The sensitivity ceiling check that runs in CI is the same check that runs at runtime in the agent middleware, and the same check that runs when someone clicks 'Add to Claude.' One rule, enforced everywhere."
- "The governed path is the easiest path. That's what gets compliance at scale."
