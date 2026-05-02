# Releasing ARIA Auth Core

This guide describes how to publish `Aria.Auth.Core` safely and consistently.

## Release Checklist

1. Update package code under `src/aria-auth-core/`.
2. Add a version heading to `src/aria-auth-core/CHANGELOG.md`.
3. Draft release notes using `.github/release-notes/aria-auth-core-release-template.md`.
4. Confirm CI is green (`ARIA Auth Core CI`).
5. Create a tag in the format `aria-auth-core-vX.Y.Z`.
6. Verify the `Publish ARIA Auth Core` workflow succeeds.

## Changelog Format

- Keep entries user-facing and grouped under:
  - Added
  - Changed
  - Fixed
  - Deprecated
  - Removed
  - Security
- Use SemVer headings, for example:

```md
## 0.2.0 - 2026-05-10
```

The publish workflow fails if the target version heading is missing.

## Publish Triggers

- Tag-based release: push `aria-auth-core-v*`
- Manual release: run workflow dispatch and provide `version`

## Safety Checks in Publish Workflow

- Validates SemVer format.
- Verifies there are actual changes under `src/aria-auth-core/`.
- Fails on duplicate package version in GitHub Packages.
- Fails if changelog does not contain the release version.

## Dry Run

Use workflow dispatch with `dry_run=true` to validate build and packaging without publishing.

## Required Permissions

- `contents: read`
- `packages: write`

Publishing uses the repository `GITHUB_TOKEN`; no additional secrets are required for GitHub Packages in this repository.
