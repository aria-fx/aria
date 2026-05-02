---
description: Run a pre-release packaging checklist for Aria CLI across NuGet, npm, and Python, including publish readiness for GitHub Packages.
name: Aria Packaging Release Checklist
argument-hint: Release scope, target version, and ecosystems to validate (nuget/npm/python).
agent: Aria CLI Packaging Agent
tools: [read, search, execute, todo]
---
Run a pre-release validation checklist for Aria CLI packaging and publishing.

## Inputs
- Release scope: $ARGUMENTS
- Targets: NuGet dotnet tool, npm package, Python package

## Required Checks
1. Verify package metadata and version consistency across manifests and docs.
2. Validate build, test, and pack commands for each selected ecosystem.
3. Validate install smoke tests where feasible:
   - dotnet tool install path assumptions
   - npm package installability and tarball sanity
   - Python wheel/sdist build validity
4. Audit publish workflows for:
   - trigger correctness
   - required permissions
   - token/secret wiring
   - registry endpoints and package naming
   - duplicate-version guardrails
5. Assess external registry readiness for npmjs.org and PyPI when requested.
6. Identify blockers and propose minimal fixes.

## Output
Return:
- Pass/fail checklist by ecosystem
- Blocking issues with file-level references
- Recommended fixes in priority order
- Release go/no-go recommendation with residual risk notes
