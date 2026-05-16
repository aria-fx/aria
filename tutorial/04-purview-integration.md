# 04 — Purview Integration

Configure Microsoft Purview to govern your ARIA assets with sensitivity
labels, DLP enforcement, and lineage tracking.

## What you'll configure

- Purview sensitivity labels mapped to ARIA governance tiers
- A custom entity type for AI assets in the Purview Data Map
- DLP policies that prevent under-classified agents from accessing sensitive data
- Lineage edges tracking which agents consume which skills and knowledge

## Prerequisites

- Completed [02 — Marketplace Setup](./02-marketplace-setup.md)
- Microsoft 365 E5 license (for Purview compliance features)
- Purview Account provisioned by Terraform
- Global Admin or Compliance Admin role for label configuration

## Step 1: Map sensitivity tiers to Purview labels

ARIA uses five sensitivity tiers. Each maps to a Purview sensitivity label:

| ARIA Tier | Purview Label | Color | Scope |
|-----------|---------------|-------|-------|
| `public` | Public | Green | Open data, no restrictions |
| `internal` | Internal | Blue | Internal use, not for external sharing |
| `confidential` | Confidential | Orange | Business-sensitive, need-to-know |
| `highly_confidential` | Highly Confidential | Red | Restricted access, regulated data |
| `restricted` | Restricted | Dark Red | Maximum protection, minimal access |

In the Purview compliance portal (`compliance.microsoft.com`):

1. Navigate to **Information protection** → **Labels**
2. Create or verify that labels matching each tier exist
3. Note the label GUIDs — you'll use these in governance overlays

## Step 2: Register AI asset entity type in Data Map

Purview's Data Map needs a custom entity type to represent ARIA assets.
This extends the Apache Atlas type system that Purview uses internally.

```bash
# Create the custom type definition
az rest --method POST \
  --uri "https://purview-aria-dev.purview.azure.com/catalog/api/atlas/v2/types/typedefs" \
  --headers "Content-Type=application/json" \
  --body '{
    "entityDefs": [{
      "name": "oasf_ai_asset",
      "description": "[OASF](https://schema.oasf.outshift.com/)-classified AI asset registered through ARIA",
      "superTypes": ["DataSet"],
      "serviceType": "ARIA",
      "attributeDefs": [
        {
          "name": "sensitivity_tier",
          "typeName": "string",
          "description": "ARIA sensitivity tier (public, internal, confidential, highly_confidential, restricted)"
        },
        {
          "name": "oasf_version",
          "typeName": "string",
          "description": "[OASF](https://schema.oasf.outshift.com/) Record version"
        },
        {
          "name": "asset_type",
          "typeName": "string",
          "description": "ARIA entity type (agent, skill, instruction, knowledge, orchestration)"
        },
        {
          "name": "compliance_frameworks",
          "typeName": "string",
          "description": "Comma-separated list of compliance frameworks"
        },
        {
          "name": "dependency_sensitivity_ceiling",
          "typeName": "string",
          "description": "Maximum sensitivity tier this asset is allowed to depend on"
        }
      ]
    }]
  }'
```

## Step 3: Register relationship types

Define the ARIA relationship types so lineage edges appear in the Data Map:

```bash
az rest --method POST \
  --uri "https://purview-aria-dev.purview.azure.com/catalog/api/atlas/v2/types/typedefs" \
  --headers "Content-Type=application/json" \
  --body '{
    "relationshipDefs": [
      {
        "name": "aria_invokes",
        "description": "Agent invokes a Skill",
        "relationshipCategory": "ASSOCIATION",
        "endDef1": { "type": "oasf_ai_asset", "name": "invoker", "isContainer": false },
        "endDef2": { "type": "oasf_ai_asset", "name": "invoked_skill", "isContainer": false }
      },
      {
        "name": "aria_grounded_in",
        "description": "Agent is grounded in a Knowledge base",
        "relationshipCategory": "ASSOCIATION",
        "endDef1": { "type": "oasf_ai_asset", "name": "consumer", "isContainer": false },
        "endDef2": { "type": "oasf_ai_asset", "name": "knowledge_source", "isContainer": false }
      },
      {
        "name": "aria_governed_by",
        "description": "Agent is governed by an Instruction set",
        "relationshipCategory": "ASSOCIATION",
        "endDef1": { "type": "oasf_ai_asset", "name": "governed_agent", "isContainer": false },
        "endDef2": { "type": "oasf_ai_asset", "name": "instruction_set", "isContainer": false }
      }
    ]
  }'
```

## Step 4: Configure DLP policies

Create a DLP policy that prevents under-classified agents from accessing
data above their sensitivity ceiling. In the Purview compliance portal:

1. Navigate to **Data loss prevention** → **Policies**
2. Create a new policy scoped to the AI asset containers
3. Add a condition: "Content contains sensitivity label **Confidential** or above"
4. Add an action: "Block access when the requesting identity's
   `sensitivity_tier` attribute is below `confidential`"

In practice, this is enforced through two mechanisms:

- **CI/CD time**: The `oasf-validate` workflow checks that the governance
  overlay's `sensitivity_tier` doesn't exceed its `dependency_sensitivity_ceiling`
- **Runtime**: The `PurviewPolicyMiddleware` in the Agent Framework evaluates
  prompts and responses against Purview DLP policies

## Step 5: Update governance overlays with label IDs

Now that you have Purview labels, update your asset's governance overlay
with the actual label GUID:

```json
{
  "governance": {
    "sensitivity_tier": "internal",
    "purview_label_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    ...
  }
}
```

The `purview-sync.yml` workflow reads this label ID and applies it to
the OCI artifact metadata in the Purview Data Map after each publish.

## Step 6: Verify lineage in Purview

After publishing an asset, open the Purview governance portal:

1. Navigate to **Data Map** → **Browse assets**
2. Filter by type: `oasf_ai_asset`
3. Click on your asset to see its properties
4. Click the **Lineage** tab to see relationship edges

You should see your agent connected to its skills and knowledge bases
via the `aria_invokes` and `aria_grounded_in` relationships.

## Ongoing governance

Once configured, the Purview integration operates automatically:

- Every `purview-sync.yml` run registers/updates assets in the Data Map
- Sensitivity labels propagate through the dependency graph
- DLP policies evaluate AI interactions against declared classifications
- The Purview audit log captures all lifecycle events
- DSPM for AI surfaces anomalous usage patterns

The compliance team can now answer questions like:

- "Which agents access PHI-classified knowledge?"
- "If this knowledge base is reclassified, which agents are affected?"
- "Show me all assets governed under HIPAA with their approval chains"

## Next Steps

→ [05 — Sample Agent](./05-sample-agent.md)
