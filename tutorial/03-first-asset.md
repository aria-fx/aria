# 03 — Your First Asset

Create, govern, and publish an OASF-governed MCP skill from scratch.

## What you'll build

An MCP server skill called `policy-lookup` that provides HR policy Q&A
capabilities. You'll write the OASF Record, governance overlay, submit
a PR, watch the validation workflow pass, and see it published as an OCI
artifact.

## Step 1: Create a new repo from the template

In your GitHub org, click "Use this template" on `aria-asset-template`,
or use the CLI:

```bash
gh repo create your-org/aria-policy-lookup-skill \
  --template your-org/aria-asset-template \
  --private \
  --clone
cd aria-policy-lookup-skill
```

## Step 2: Define the OASF Record

Edit `oasf-record.json` to describe your skill:

```json
{
  "name": "your-org.com/skills/policy-lookup",
  "version": "1.0.0",
  "schema_version": "1.0.0",
  "description": "MCP server providing HR policy knowledge base lookup",
  "skills": [
    { "id": 30101, "name": "knowledge_retrieval/rag" }
  ],
  "domains": [
    { "name": "human_resources" }
  ],
  "modules": [
    {
      "type": "mcp_server",
      "transport": "stdio",
      "tools": ["lookup_policy", "list_policies"]
    }
  ],
  "locators": [
    {
      "type": "source_code",
      "urls": ["https://github.com/your-org/aria-policy-lookup-skill"]
    }
  ],
  "authors": ["Your Name <you@your-org.com>"],
  "created_at": "2026-05-01T00:00:00Z"
}
```

Key decisions:

- **skills**: We declare `knowledge_retrieval/rag` (ID 30101) from the OASF
  skill taxonomy. This makes the skill discoverable via `aria search --skill rag`.
- **modules**: The `mcp_server` type with `transport: "stdio"` tells the
  `aria install` command how to wire this into Claude Desktop or VS Code.
- **tools**: Listing the tool names allows consumers to understand what
  capabilities they're getting before installing.

## Step 3: Define the governance overlay

Edit `oasf-governance.json`:

```json
{
  "governance": {
    "sensitivity_tier": "internal",
    "data_classifications": [],
    "purview_label_id": "",
    "approval_chain": ["ai-governance"],
    "allowed_consumers": [],
    "max_data_retention_days": 90,
    "audit_level": "standard",
    "dependency_sensitivity_ceiling": "confidential",
    "compliance_frameworks": ["SOC2"]
  }
}
```

Key decisions:

- **sensitivity_tier**: `"internal"` — this skill accesses non-public but
  non-sensitive policy documents. No PII, no PHI.
- **allowed_consumers**: Empty array means open access — any team can use it.
- **dependency_sensitivity_ceiling**: `"confidential"` — this skill is allowed
  to be consumed by agents up to confidential classification.
- **compliance_frameworks**: `["SOC2"]` — tags this asset for SOC 2 audit scope.

## Step 4: Implement the skill

Add your MCP server implementation in `src/`. For this tutorial, create
a minimal Node.js MCP server:

```bash
mkdir -p src
cat > src/server.js << 'EOF'
#!/usr/bin/env node
// Minimal MCP server for policy lookup
// In production, this would connect to a real knowledge base

const policies = {
  pto_policy: "Employees accrue 15 days PTO per year.",
  benefits_overview: "Full-time employees are eligible for medical, dental, and vision.",
  remote_work: "Hybrid work requires 3 days in-office per week."
};

process.stdin.setEncoding('utf8');
process.stdin.on('data', (data) => {
  const request = JSON.parse(data);
  if (request.method === 'tools/call' && request.params.name === 'lookup_policy') {
    const topic = request.params.arguments.topic;
    const content = policies[topic] || `No policy found for "${topic}"`;
    process.stdout.write(JSON.stringify({
      jsonrpc: "2.0",
      id: request.id,
      result: { content: [{ type: "text", text: content }] }
    }) + '\n');
  }
});
EOF
chmod +x src/server.js
```

## Step 5: Submit a PR

```bash
git checkout -b feature/initial-asset
git add .
git commit -m "feat: initial OASF-governed policy lookup skill"
git push -u origin feature/initial-asset
gh pr create --title "feat: policy-lookup skill v1.0.0" \
  --body "Initial OASF-governed MCP server for HR policy Q&A"
```

## Step 6: Watch the validation workflow

Open the PR in GitHub. The `OASF Validate` workflow will trigger
automatically. It runs three checks:

1. **Schema validation** — verifies `oasf-record.json` has all required
   fields (`name`, `version`, `schema_version`, `skills`, `locators`, `authors`)

2. **Governance overlay validation** — verifies `oasf-governance.json` has
   required governance fields and the `sensitivity_tier` is a valid value

3. **Sensitivity ceiling check** — verifies the asset's sensitivity tier
   doesn't exceed its own declared ceiling

If all checks pass, you'll see a green checkmark. The PR now requires
CODEOWNERS review from the `ai-governance` team (for the governance overlay)
and `ai-platform-engineering` (for the OASF record).

## Step 7: Merge and publish

Once approved, merge the PR. This triggers two more workflows:

1. **Publish OCI Artifact** — builds a Docker image containing the OASF
   record, governance overlay, and source code, then pushes it to your
   Azure Container Registry with a version tag and `latest`.

2. **Purview Sync** — reads the governance overlay, applies the Purview
   sensitivity label, updates the Data Map with a new AI asset entity,
   and creates lineage edges for any module refs.

## Step 8: Verify publication

```bash
# Check ACR for the artifact
az acr repository list --name acrariadevaoasf -o table

# Check the specific tag
az acr repository show-tags --name acrariadevaoasf \
  --repository aria-assets/your-org.com-skills-policy-lookup -o table
```

You should see tag `1.0.0` and `latest`.

## What just happened

You created a governed AI asset that:

- Has a machine-readable OASF Record declaring its capabilities
- Has a governance overlay declaring its sensitivity and compliance posture
- Was validated against the OASF schema before it could be merged
- Required approval from the governance team
- Was published as a content-addressed OCI artifact
- Was registered in Microsoft Purview's Data Map
- Can be discovered via `aria search --skill "knowledge_retrieval/rag"`
- Can be installed into Claude Desktop via `aria install ... --target claude-desktop`

This is one asset. The power of ARIA is that every AI asset in the enterprise
follows this same pattern — creating a governed, discoverable, composable
ecosystem.

## Next Steps

→ [04 — Purview Integration](./04-purview-integration.md)
