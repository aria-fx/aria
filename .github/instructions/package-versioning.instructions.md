---
description: Use when updating package versions, release notes, or publishing workflows for NuGet, npm, and Python packages. Enforces consistent versioning and changelog quality.
name: Package Versioning And Changelog Rules
applyTo:
  - "src/aria-cli/**"
  - ".github/workflows/**"
  - "**/package.json"
  - "**/pyproject.toml"
  - "**/*.csproj"
  - "**/CHANGELOG.md"
---
# Package Versioning Rules

- Keep one release version per change set unless explicitly creating a prerelease.
- Use SemVer consistently: MAJOR.MINOR.PATCH with prerelease suffixes only when intended.
- When multiple package ecosystems are published together, keep versions aligned unless the release plan says otherwise.
- Do not overwrite or republish an existing immutable version.

# Changelog Rules

- Update changelog entries in the same PR as version updates.
- Group entries under Added, Changed, Fixed, Deprecated, Removed, or Security when possible.
- Keep entries user-facing and outcome-oriented; avoid commit-message style fragments.
- Include migration notes when behavior or install steps change.

# Workflow Rules

- Publish jobs must validate version state before upload.
- Publish jobs must fail fast on duplicate-version detection.
- Document required secrets and permissions near the publish workflow or in linked docs.
- Keep publish triggers explicit (for example, tags or protected branches), not broad by default.

# Validation Expectations

- Confirm version updates are reflected in package metadata, workflows, and release docs.
- Ensure changelog and published package version refer to the same release intent.
- Highlight any mismatch as a blocker, not a warning.
