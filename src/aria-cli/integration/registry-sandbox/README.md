# Registry Sandbox for Integration Testing

This folder provides a Docker Compose based sample registry setup for integration testing multi-registry behavior in `aria`.

It intentionally includes both:
- a governed internal registry artifact (has `oasf-record.json` + `oasf-governance.json`)
- a public ungoverned registry artifact (has `oasf-record.json` only)

## What it starts

- `localhost:5500` -> internal registry
- `localhost:5501` -> public registry

## Prerequisites

- Docker + Docker Compose
- Network access to pull `registry:2` and `ghcr.io/oras-project/oras:v1.2.3`
- `curl`

## Quick start

```bash
cd src/aria-cli/integration/registry-sandbox
chmod +x seed.sh cleanup.sh
./seed.sh
```

After seeding, use these registry roots in `~/.aria/config.json`:

```json
"registries": [
  "localhost:5500/aria-assets",
  "localhost:5501/aria-assets"
]
```

Stop and clean up:

```bash
./cleanup.sh
```

## Seeded sample references

- Governed internal: `localhost:5500/aria-assets/policy-lookup-skill:1.0.0`
- Ungoverned public: `localhost:5501/aria-assets/public-web-search:1.0.0`

## Intended test scenarios

- Multi-registry fan-out and aggregation
- Dedupe/ordering behavior when multiple sources are present
- Governed vs ungoverned policy decisions
- Partial registry outage handling

## Notes

Current `aria` implementation may still use demo search paths depending on feature branch state. This sandbox is meant to support issue #14 and #17 implementation and CI/integration-test authoring.
