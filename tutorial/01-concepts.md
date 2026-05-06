# 01 — ARIA Concepts

## What is ARIA?

ARIA (Asset Registry for Intelligent Agents) is a reference architecture for
managing the full lifecycle of enterprise AI primitives — agents, skills,
instructions, knowledge bases, and orchestration configurations.

Think of it as **TOGAF for AI assets**: a metamodel that defines what these
things are, how they relate to each other, and how they're governed.

## The Four Layers

ARIA operates across four layers:

1. **Metamodel** — What are these things and how do they relate?
   - Five entity types classified by OASF
   - Relationship types with governance implications
   - Lifecycle states with gated transitions

2. **Marketplace** — How do you publish, discover, and compose them?
   - GitHub repos with OASF manifest sidecars
   - OCI registry for content-addressed artifact storage
   - CI/CD pipelines for validation and publishing

3. **Governance** — How do you classify, control, and audit them?
   - Microsoft Purview for sensitivity labels and DLP
   - OASF governance overlay for policy-as-code
   - Sensitivity label inheritance through dependency graphs

4. **Distribution** — How do non-technical users consume them?
   - ARIA Catalog API translates OCI artifacts into platform-native installs
   - Claude Desktop Extensions panel with enterprise allowlist
   - Cowork contextual discovery based on OASF domain matching
   - Web portal for browsing, filtering, and requesting access
   - Governance enforcement is invisible — blocked assets are never shown

## The Five Entity Types

| Entity            | What it is                                        | OASF classification                |
| ----------------- | ------------------------------------------------- | ---------------------------------- |
| **Agent**         | An autonomous AI unit with identity and lifecycle | OASF Record (primary)              |
| **Skill**         | A reusable capability an agent can invoke         | OASF Skill annotation              |
| **Instruction**   | Behavioral configuration (prompts, guardrails)    | OASF Module (prompt_bundle)        |
| **Knowledge**     | Data corpus for grounding and retrieval           | OASF Module (knowledge_base)       |
| **Orchestration** | Routing and composition logic                     | OASF Module (orchestration_config) |

## Key Relationships

```
Agent ──invokes──────→ Skill        (1:N)
Agent ──governed_by──→ Instruction  (1:N)
Agent ──grounded_in──→ Knowledge    (1:N)
Orchestration ──composed_by──→ Agent (1:N)
```

Each relationship carries a governance implication. For example, when an agent
invokes a skill, it inherits the sensitivity ceiling of that skill.

## The Sensitivity Inheritance Rule

> An AI asset inherits the highest sensitivity classification of any asset
> it depends on.

If your agent accesses a "Public" skill and a "Confidential" knowledge base,
the agent itself is classified as "Confidential."

This is enforced:
- **At CI time** — the `oasf-validate` GitHub Action checks that the agent's
  declared `sensitivity_tier` ≥ max(dependency tiers)
- **At runtime** — Purview DLP policies prevent under-classified agents from
  accessing sensitive knowledge
- **At install time** — the `aria audit` command validates governance before
  pulling artifacts

## Next Steps

→ [02 — Marketplace Setup](./02-marketplace-setup.md)
