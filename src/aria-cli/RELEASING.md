# Releasing aria CLI

This guide describes how to publish the `Aria.Cli` NuGet tool package and the `@aria-fx/aria-cli` npm package consistently.

## Release Checklist

1. Update code under `src/aria-cli/`.
2. Add a version heading to `src/aria-cli/CHANGELOG.md`.
3. Draft release notes using `.github/release-notes/aria-cli-release-template.md`.
4. Confirm tests and build are green.
5. Create a tag in the format `aria-cli-vX.Y.Z`.
6. Verify `Publish aria CLI Packages` succeeds and GitVersion resolves the same SemVer as the tag.

## Versioning

- GitVersion computes the release SemVer from git history and tags using `GitVersion.yml`.
- For tag releases, the workflow validates GitVersion SemVer matches `aria-cli-vX.Y.Z`.

## Publish Triggers

- Tag-based release: push `aria-cli-v*`
- Manual release: run workflow dispatch (version is computed by GitVersion)

## Safety Checks in Publish Workflow

- Computes SemVer with GitVersion and validates SemVer format.
- Validates no duplicate version exists in GitHub Packages for NuGet and npm.
- Fails if `src/aria-cli/CHANGELOG.md` does not contain the release version heading.
- Supports `dry_run=true` to validate packaging without publishing.

## Required Permissions

- `contents: read`
- `packages: write`

Publishing uses the repository `GITHUB_TOKEN`; no additional secrets are required for GitHub Packages in this repository.
