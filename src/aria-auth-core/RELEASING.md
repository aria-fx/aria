# Releasing ARIA Auth Core

This guide describes how to publish `Aria.Auth.Core` safely and consistently.

## Release Checklist

1. Update package code under `src/aria-auth-core/`.
2. Add a version heading to `src/aria-auth-core/CHANGELOG.md`.
3. Draft release notes using `.github/release-notes/aria-auth-core-release-template.md`.
4. Confirm CI is green (`ARIA Auth Core CI`).
5. Create a tag in the format `aria-auth-core-vX.Y.Z`.
6. Verify the `Publish ARIA Auth Core` workflow succeeds and that GitVersion resolves the same SemVer as the tag.

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
- Manual release: run workflow dispatch (version is computed by GitVersion)

## Safety Checks in Publish Workflow

- Computes SemVer with GitVersion and validates SemVer format.
- For tag releases, validates that GitVersion SemVer matches the pushed tag version.
- Verifies there are actual changes under `src/aria-auth-core/`.
- Fails on duplicate package version in GitHub Packages.
- Fails if changelog does not contain the release version.
- For manual releases, prepares changelog/version metadata in the runner for packaging only; persisted metadata updates must still be made through a pull request.

## Dry Run

Use workflow dispatch with `dry_run=true` to validate build and packaging without publishing.

## Required Permissions

- `contents: read`
- `packages: write`

Publishing uses the repository `GITHUB_TOKEN`; no additional secrets are required for GitHub Packages in this repository.
