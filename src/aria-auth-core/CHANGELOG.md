# Changelog

All notable changes to ARIA Auth Core are documented in this file.

The format follows Keep a Changelog categories and SemVer intent.

## Unreleased

### Added
- Initial extraction of shared auth and governance policy core from aria-cli.

## 0.1.0 - 2026-05-02

### Added
- Shared models for auth configuration and [OASF](https://schema.oasf.outshift.com/) governance records.
- Shared services for identity provider contracts, provider factory, access policy resolution, and governance validation.
- Decoupled governance overlay resolver contract for host-specific registry implementations.
