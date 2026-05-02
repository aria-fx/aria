---
title: "ARIA: Asset Registry for Intelligent Agents"
subtitle: "Reference Architecture & Capability Model"
author: "Josh Garverick — Chief Technical Architect, Xebia"
date: "April 2026"
version: "1.0"
abstract: |
  Enterprises are rapidly accumulating AI primitives — agents, skills, instructions,
  knowledge bases, and orchestration configurations — without a standardized metamodel
  for classifying, governing, and composing them. This document presents ARIA
  (Asset Registry for Intelligent Agents), a reference architecture that applies the
  Open Agentic Schema Framework (OASF) as the classification taxonomy, uses GitHub
  as the canonical marketplace and registry, and integrates Microsoft Purview as the
  governance and compliance layer.
keywords:
  - ARIA
  - OASF
  - AI governance
  - enterprise architecture
  - Microsoft Purview
  - MCP
  - agent management
toc: true
toc-depth: 3
numbersections: true
colorlinks: true
linkcolor: "purple"
geometry: margin=1in
fontsize: 11pt
mainfont: "Latin Modern Roman"
monofont: "DejaVu Sans Mono"
header-includes:
  - \usepackage{booktabs}
  - \usepackage{xcolor}
  - \definecolor{purple}{HTML}{6B21A8}
  - \definecolor{accent}{HTML}{0891B2}
  - \definecolor{teal}{HTML}{0D9488}
---

# Executive Summary

Enterprises are rapidly accumulating AI primitives — agents, skills, instructions, knowledge bases, and orchestration configurations — without a standardized metamodel for classifying, governing, and composing them. ARIA (Asset Registry for Intelligent Agents) presents a reference architecture that applies the Open Agentic Schema Framework (OASF) as the classification taxonomy, uses GitHub as the canonical marketplace and registry for these primitives, and integrates Microsoft Purview as the governance and compliance layer.

The architecture draws on the metamodel tradition of TOGAF's Architecture Content Framework, adapting it to the unique requirements of AI asset management: versioned, composable primitives with sensitivity-aware lineage and automated compliance enforcement.

## Problem Statement

- AI assets (agents, MCP servers, prompt libraries, RAG corpora) are proliferating without inventory, classification, or governance
- No standard metamodel exists for describing relationships between AI primitives the way TOGAF describes IT architecture building blocks
- Sensitivity labeling, data lineage, and compliance enforcement do not yet extend to AI-specific artifacts
- Teams cannot discover, reuse, or compose AI primitives across organizational boundaries safely

## Solution Overview

| Layer       | Function                                                        | Implementation                               |
| ----------- | --------------------------------------------------------------- | -------------------------------------------- |
| Metamodel   | Entity types, relationships, lifecycle states for AI primitives | OASF Records, Skills, Domains, Modules       |
| Marketplace | Publish, discover, version, and compose AI primitives           | GitHub repos + OCI registry + OASF manifests |
| Governance  | Classify, label, enforce, and audit AI asset usage              | Microsoft Purview + OASF overlay policies    |

# The ARIA Metamodel

Inspired by TOGAF's Architecture Content Framework, this metamodel defines the entity types that constitute an enterprise's AI capability inventory. Each entity is classified using OASF's attribute-based taxonomy and stored as an OASF Record with skills, domains, and module annotations.

## Core Entity Types

| Entity Type   | OASF Classification           | Description                                                                              | Examples                                             |
| ------------- | ----------------------------- | ---------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Agent         | Record (primary)              | Autonomous or semi-autonomous AI unit with defined capabilities, identity, and lifecycle | Copilot agent, CrewAI crew, AutoGen group, A2A agent |
| Skill         | Skill annotation              | Discrete, reusable capability that an agent can invoke                                   | MCP server, function tool, API connector             |
| Instruction   | Module (prompt_bundle)        | System prompt, guardrail set, or behavioral configuration                                | System prompt, safety rules, persona config          |
| Knowledge     | Module (knowledge_base)       | Structured or unstructured data corpus for grounding and retrieval                       | RAG index, embeddings store, document collection     |
| Orchestration | Module (orchestration_config) | Composition and routing logic connecting agents, skills, and knowledge                   | Agent mesh config, routing DAG, workflow definition  |

## Entity Relationships

| Relationship | Source        | Target      | Cardinality | Governance Implication                                       |
| ------------ | ------------- | ----------- | ----------- | ------------------------------------------------------------ |
| invokes      | Agent         | Skill       | 1:N         | Agent inherits sensitivity ceiling of invoked skills         |
| governed_by  | Agent         | Instruction | 1:N         | Instruction version pinning required for compliance          |
| grounded_in  | Agent         | Knowledge   | 1:N         | Data classification of knowledge propagates to agent outputs |
| composed_by  | Orchestration | Agent       | 1:N         | Orchestration inherits highest sensitivity of member agents  |
| depends_on   | Skill         | Skill       | N:M         | Transitive dependency chain affects blast radius analysis    |
| extends      | Module        | Record      | 1:N         | Extension modules must pass schema validation                |

## OASF Record Structure

Every entity in the metamodel is represented as an OASF Record containing identity, classification metadata, capability annotations, and governance envelope:

- **name**: Unique identifier using domain-based naming (e.g., `aria.dev/agents/onboarding-assistant`)
- **version**: Semantic version enabling dependency resolution and rollback
- **schema_version**: OASF schema version for forward compatibility
- **skills**: Array of OASF skill taxonomy references describing capabilities
- **domains**: Business domain annotations for discovery scoping
- **modules**: Extensible metadata blocks (MCP descriptors, evaluation metrics, prompt bundles)
- **locators**: References to source code, container images, or service endpoints
- **authors**: Ownership chain for accountability and approval routing
- **created_at / updated_at**: Temporal anchors for audit and lifecycle tracking

## Lifecycle States

| State      | Description                                    | Allowed Transitions             | Governance Gate                           |
| ---------- | ---------------------------------------------- | ------------------------------- | ----------------------------------------- |
| Draft      | Asset under development, not discoverable      | Draft → Review                  | None (author workspace)                   |
| Review     | PR submitted, OASF validation passing          | Review → Published / Draft      | CODEOWNERS approval + schema validation   |
| Published  | Active in registry, discoverable and invocable | Published → Deprecated / Review | Purview sensitivity label assigned        |
| Deprecated | Marked for sunset, consumers warned            | Deprecated → Archived           | Migration plan required                   |
| Archived   | Immutable record preserved for audit           | Terminal                        | Retention policy enforced via Purview DLM |

# Enterprise Capability Model

For an enterprise to successfully manage AI assets at scale, it needs capabilities across six domains.

| Domain                           | Capabilities                                                                    | Maturity Indicator                                                   |
| -------------------------------- | ------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Asset Inventory & Classification | Discover, catalog, and classify all AI primitives using a standardized taxonomy | All AI assets registered with OASF Records                           |
| Lifecycle Management             | Version, publish, deprecate, and archive with automated gating                  | Automated CI/CD validates OASF schema on every PR                    |
| Composition & Orchestration      | Compose agents from skills and knowledge; resolve dependency chains             | Orchestration configs reference only published, validated primitives |
| Governance & Compliance          | Apply sensitivity labels, enforce DLP, track lineage, maintain audit trails     | Purview labels auto-propagate through dependency graph               |
| Discovery & Reuse                | Search, filter, and consume AI assets by skill taxonomy or domain               | Searchable registry with skill-based filtering active                |
| Observability & Evaluation       | Monitor performance, track metrics, detect drift, surface violations            | OTEL telemetry + evaluation modules attached to published Records    |

# Reference Implementation: GitHub as ARIA Marketplace

GitHub provides the foundation for the canonical marketplace due to its native support for versioning, pull request workflows, code review, branch protection, and GitHub Actions automation.

## Repository Structure

```
aria-assets/
├── oasf-record.json          # OASF Record manifest (required)
├── oasf-governance.json      # Governance overlay
├── src/                      # Asset implementation
├── tests/                    # Validation and evaluation tests
├── docs/                     # Usage documentation
└── .github/
    ├── CODEOWNERS            # Governance-aware ownership
    └── workflows/
        ├── oasf-validate.yml # Schema validation on PR
        ├── publish.yml       # Push to OCI registry on merge
        └── purview-sync.yml  # Sync labels to Purview
```

## OASF Record Manifest Example

```json
{
  "name": "aria.dev/agents/onboarding-assistant",
  "version": "2.1.0",
  "schema_version": "1.0.0",
  "description": "HR onboarding agent with document generation and policy Q&A",
  "skills": [
    { "id": 10101, "name": "nlp/nlu/intent_classification" },
    { "id": 30101, "name": "knowledge_retrieval/rag" }
  ],
  "domains": [{ "name": "human_resources/onboarding" }],
  "modules": [
    { "type": "mcp_server", "transport": "stdio", "tools": ["lookup_policy"] },
    { "type": "knowledge_base", "ref": "aria.dev/knowledge/hr-policies" }
  ],
  "authors": ["Josh Garverick <josh.garverick@aria.dev>"]
}
```

## Governance Overlay Manifest Example

```json
{
  "governance": {
    "sensitivity_tier": "confidential",
    "data_classifications": ["PII", "PHI"],
    "approval_chain": ["ai-governance-lead", "data-privacy-officer"],
    "allowed_consumers": ["hr-team", "onboarding-automation"],
    "max_data_retention_days": 90,
    "audit_level": "full",
    "dependency_sensitivity_ceiling": "highly_confidential",
    "compliance_frameworks": ["HIPAA", "SOC2"]
  }
}
```

# Microsoft Purview Integration

Microsoft Purview serves as the governance and compliance backbone, extending its existing data classification, sensitivity labeling, and DLP capabilities into the AI asset domain.

## Sensitivity Label Inheritance

Purview's sensitivity labels propagate through the AI asset dependency graph. The inheritance rule is strict: an AI asset inherits the highest sensitivity classification of any asset it depends on. This is enforced at both CI time (the `oasf-validate` action checks ceiling constraints) and runtime (Purview DLP policies evaluate AI interactions).

## Integration Points

- **AI Hub**: Centralized visibility into active agents, skills, and knowledge bases
- **Interaction Telemetry**: Prompts and responses evaluated against Purview classifiers
- **Insider Risk Management**: Detection of anomalous AI usage patterns
- **Data Map & Lineage**: AI assets as first-class entities with relationship edges
- **DLP Policy Enforcement**: Block under-classified agents from accessing sensitive data
- **Purview SDK**: Embedded in Agent Framework SDK for shift-left governance

# ARIA Package Manager

The `aria` CLI bridges the OCI marketplace with developer runtimes.

## Core Commands

| Command             | Description                                                 |
| ------------------- | ----------------------------------------------------------- |
| `aria search`        | Discover assets by OASF skill taxonomy, domain, or keyword  |
| `aria inspect <ref>` | Display OASF Record and governance overlay                  |
| `aria audit <ref>`   | Validate sensitivity ceiling, consumer, and dependencies    |
| `aria install <ref>` | Pull OCI artifact, enforce governance, install to target    |
| `aria list`          | List installed assets with version, sensitivity, and target |

## Install Targets

| OASF Module Type | Target                | Install Behavior                              |
| ---------------- | --------------------- | --------------------------------------------- |
| mcp_server       | Claude Desktop        | Add MCP entry to `claude_desktop_config.json` |
| mcp_server       | VS Code               | Add to `.vscode/mcp.json` workspace config    |
| mcp_server       | Agent Framework       | Register as MCP tool provider                 |
| Record (agent)   | Agent Framework (A2A) | Register as remote A2A endpoint               |
| knowledge_base   | Agent Framework       | Register with RAG pipeline configuration      |

# Implementation Roadmap

| Phase       | Duration  | Deliverables                                                                  | Success Criteria                                   |
| ----------- | --------- | ----------------------------------------------------------------------------- | -------------------------------------------------- |
| Foundation  | 4–6 weeks | OASF server, governance overlay schema, GitHub template, validation Action    | First asset registered with valid OASF Record      |
| Marketplace | 6–8 weeks | OCI publish pipeline, Agent Directory, discovery channels, CODEOWNERS         | Teams discover and consume assets through registry |
| Purview     | 6–8 weeks | Label mapping, purview-sync Action, Data Map lineage, sensitivity propagation | Labels auto-propagate through dependency graphs    |
| Runtime     | 4–6 weeks | DLP enforcement, DSPM dashboards, insider risk, audit logging                 | Compliance team generates AI audit reports         |
| Scale       | Ongoing   | Cross-team adoption, eval framework, EU AI Act templates, federated directory | All production AI assets governed through ARIA     |

# Conclusion

Enterprise AI is at an inflection point. ARIA — the combination of OASF for classification, GitHub for marketplace operations, and Microsoft Purview for governance — creates a practical, implementable framework that transforms AI asset management from ad hoc artifact accumulation into a governed, discoverable, composable ecosystem.

The key insight is that governance must be embedded in the development workflow — not bolted on after deployment. By making the OASF Record and governance overlay part of every pull request, and by automating Purview integration through CI/CD pipelines, ARIA makes compliance a natural consequence of building AI assets rather than an afterthought.
