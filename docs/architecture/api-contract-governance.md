# ARIA API Contract Governance Model

**Status:** Authoritative (Effective May 2026)  
**Ownership:** Architecture Council + Runtime Engineering  
**Last Updated:** 2026-05-10

---

## Overview

This document establishes a single source of truth and update workflow for ARIA API contracts to prevent silent spec/runtime divergence and ensure consistent integration across aria, aria-gateway, and aria-skills repositories.

## Contract Hierarchy and Ownership

### 1. **Architecture Specifications (Authoritative)**

The canonical API contracts are defined as OpenAPI 3.1.0 specifications in [`aria/docs/architecture/`](./):

- **`catalog-api.openapi.yaml`** — Governed catalog and install API for direct asset discovery and installation.
- **`distribution-gateway.openapi.yaml`** — Runtime-oriented delivery gateway for channel-native bundling and access evaluation.

**Ownership:** ARIA Architecture Council  
**Maintainers:** `@aria-fx/architecture`  
**SLA:** Contract changes reviewed and merged within 3 business days.

### 2. **Runtime Implementations**

Each repository implements and must strictly conform to the authoritative contract:

- **aria-gateway** (`api/src/routes/*`) — Implements distribution-gateway and catalog API endpoints.
- **aria-cli** (`Services/CatalogClient.cs`) — Consumes catalog-api endpoints.
- **aria-skills** (orchestrator) — Consumes distribution-gateway endpoints.

**Ownership:** Per-repo engineering teams  
**Validation:** CI-enforced OpenAPI drift detection (see Enforcement below).

### 3. **Generated OpenAPI (Non-authoritative)**

Runtime-generated OpenAPI specs (e.g., `aria-gateway/api/docs/openapi.json`) are derived documentation, not authoritative sources. These are used for validation and tooling only.

---

## Workflow: Proposing Contract Changes

### Step 1: Open Architecture RFC

Create an issue in `aria` repo with title `[RFC] Contract Change: <endpoint or operation summary>`.

**Template:**
```
### Motivation
Explain why the contract change is needed.

### Proposed Changes
Describe the OpenAPI changes (new paths, fields, status codes, etc.).

### Impact Analysis
- [ ] Consumer repos affected (aria-cli, aria-gateway, aria-skills)
- [ ] Breaking vs. backward-compatible
- [ ] Migration path for existing consumers

### Acceptance Criteria
- [ ] All consumer teams review and approve
- [ ] Backward-compat impact documented or accepted
- [ ] Implementation PR ready in each affected repo
```

### Step 2: Architecture Review

ARIA Architecture Council reviews the RFC and OpenAPI changes:
- Validates against OpenAPI best practices (consistent naming, proper status codes, security schemes).
- Checks impact on dependent systems (governance, pricing, access models).
- Confirms alignment with enterprise integration patterns.

**Approval:** At least 2 approvals from `@aria-fx/architecture`.

### Step 3: Update Authoritative Specification

Once approved, the RFC issue author or Architecture team updates the OpenAPI file in `aria/docs/architecture/`:

```bash
# Merge to aria/main only after RFC approval
git checkout -b chore/contract-update/<issue-number>
# Edit catalog-api.openapi.yaml or distribution-gateway.openapi.yaml
git commit -m "chore: update <api> contract per aria#<issue-number>"
git push -u origin chore/contract-update/<issue-number>
# Create PR with link to RFC issue
```

**PR Checklist:**
- [ ] Links to approved RFC issue.
- [ ] Changes validated against OpenAPI 3.1.0 schema.
- [ ] Changelog entry added to `docs/architecture/CHANGELOG.md`.
- [ ] At least 1 Architecture approval.

### Step 4: Implement in Runtime

Each affected repo implements the new contract:

```bash
# Example: aria-gateway
git checkout -b feat/contract-compliance/<issue-number>
# Update route handlers, models, tests to match new contract
git commit -m "feat: implement contract changes per aria#<issue-number>"
git push -u origin feat/contract-compliance/<issue-number>
# Create PR with cross-repo dependency link
gh pr create --body "Implements aria#<issue-number>. Depends on aria-gateway#<sync-issue>"
```

**PR Checklist:**
- [ ] Links to RFC issue and architecture PR.
- [ ] OpenAPI drift test passes (`tests/openapi-contract-drift.test.ts`).
- [ ] All routes/responses match contract spec.
- [ ] Integration tests pass.
- [ ] At least 1 approval from per-repo engineering team.

### Step 5: Validate Alignment

Once all PRs are merged, run cross-repo contract sync validation:

```bash
# From CI or local development
make validate-contract-alignment
# or
npm run test:contract-drift --workspace=aria-gateway
```

**Success Criteria:**
- No unresolved contract diffs reported.
- All consumer repos report compliance.

---

## Approved Temporary Drift

In rare cases, a consumer repo may temporarily diverge from the contract to support emergencies or phased rollouts.

### Exception Process

1. **File Exception Request** — Create an issue in the consumer repo titled `[Drift Exception] <component> reasons`.
2. **Document Impact** — Clearly state the drift, why it's necessary, and target resolution date.
3. **Architecture Approval** — Requires explicit approval from `@aria-fx/architecture`.
4. **Add to Exception List** — Entry in `.github/contract-exceptions.md` with expiration date.
5. **Monitor** — CI reports exception (not a failure) during contract sync checks.
6. **Resolve** — Remove exception once consumer catches up.

**Example Exception:**
```markdown
### aria-gateway install endpoint timeout (approved May 2026–May 31, 2026)
- **Drift:** Install returns 200 OK immediately instead of 202 Accepted-and-in-progress.
- **Reason:** Legacy client library requires synchronous response; migration in progress.
- **Expires:** May 31, 2026.
- **Resolved By:** aria-gateway#40 (async client rollout).
```

---

## Enforcement and Validation

### CI Gates

Every PR touching API routes or contracts triggers:

1. **OpenAPI Contract Drift Test** (`tests/openapi-contract-drift.test.ts`)
   - Compares runtime-generated OpenAPI against pinned contract assertions.
   - Fails PR if paths, methods, security schemes, or response codes are removed.
   - Warnings (not failures) for new endpoints or fields.

2. **Cross-Repo Contract Sync** (`.github/workflows/contract-sync.yml`)
   - Runs after contract or implementation PR merges.
   - Compares architecture spec against runtime OpenAPI for all deployed services.
   - Reports drift summary and blocks deployment if unresolved exceptions exist.

### Manual Validation

Teams can validate locally:

```bash
# aria-gateway
cd aria-gateway/api
npm run test:contract-drift

# aria-gateway distribution-gateway contract vs. architecture spec
npm run test:contract-sync

# All repos
cd /workspaces/aria-fx
make validate-all-contracts
```

---

## Escalation and Disputes

If a runtime team believes a contract requirement is infeasible:

1. **File a Counter-RFC** in the `aria` repo.
2. **Propose alternative** with detailed rationale.
3. **Architecture Council review** — May approve alternative or require implementation as specified.
4. **Binding decision** — Architecture Council decision is final; escalate to steering if needed.

---

## Version and Changelog

### Semver Policy

ARIA API contracts follow semver:

- **Major (`X.0.0`):** Breaking changes (removed paths, renamed fields, incompatible response schemas).
- **Minor (`1.X.0`):** Additive changes (new paths, new optional fields, new status codes).
- **Patch (`1.0.X`):** Documentation updates, no behavior changes.

**Update:** Increment version in OpenAPI `info.version` field when publishing changes.

### Changelog

Maintain `docs/architecture/CHANGELOG.md` with entries for each contract change:

```markdown
## [1.2.0] - 2026-05-10

### Added
- `/catalog/assets/{name}/{version}/governance` endpoint (aria#12)
- `governance_approval_chain` field in asset manifest response

### Changed
- `/catalog/assets` query parameter `sensitivity` now optional (was required)

### Fixed
- Corrected `/v1/channels/{channel}/catalog` response schema (aria#10)
```

---

## Related Policies

- **OpenAPI Best Practices:** [oasf.schema.org/openapi](https://oasf.schema.org/openapi) (external, when available)
- **Security Schemes:** All endpoints must declare `bearerAuth` or exempt security via `.security: []`
- **Error Responses:** All endpoints must include `401 Unauthorized` and `403 Forbidden` when protected.
- **Cross-Repo Linking:** All contract PRs must link affected issues in consumer repos.

---

## Contact

- **Architecture Questions:** @aria-fx/architecture
- **Consumer Integration Issues:** Per-repo engineering team (aria-cli, aria-gateway, aria-skills)
- **Process Improvements:** ARIA Steering Committee

