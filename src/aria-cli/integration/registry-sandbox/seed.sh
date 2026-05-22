#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

INTERNAL_REF="localhost:5500/aria-assets/policy-lookup-skill:1.0.0"
PUBLIC_REF="localhost:5501/aria-assets/public-web-search:1.0.0"
ORAS_IMAGE="ghcr.io/oras-project/oras:v1.2.3"

echo "[seed] Starting local registries..."
docker compose -f "$ROOT_DIR/docker-compose.yml" up -d

echo "[seed] Waiting for registries to be reachable..."
for url in "http://localhost:5500/v2/" "http://localhost:5501/v2/"; do
  for i in {1..30}; do
    if curl -fsS "$url" >/dev/null 2>&1; then
      break
    fi
    sleep 1
  done
  if ! curl -fsS "$url" >/dev/null 2>&1; then
    echo "[seed] Registry not reachable: $url" >&2
    exit 1
  fi
  echo "[seed] Reachable: $url"
done

echo "[seed] Seeding governed internal artifact: $INTERNAL_REF"
docker run --rm --network host \
  -v "$ROOT_DIR/fixtures:/fixtures:ro" \
  "$ORAS_IMAGE" push --disable-path-validation --plain-http "$INTERNAL_REF" \
  "/fixtures/internal-governed/oasf-record.json:application/vnd.oasf.record.v1+json" \
  "/fixtures/internal-governed/oasf-governance.json:application/vnd.oasf.governance.v1+json"

echo "[seed] Seeding ungoverned public artifact: $PUBLIC_REF"
docker run --rm --network host \
  -v "$ROOT_DIR/fixtures:/fixtures:ro" \
  "$ORAS_IMAGE" push --disable-path-validation --plain-http "$PUBLIC_REF" \
  "/fixtures/public-ungoverned/oasf-record.json:application/vnd.oasf.record.v1+json"

echo "[seed] Seed complete. Catalog snapshots:"
curl -fsS "http://localhost:5500/v2/_catalog" && echo
curl -fsS "http://localhost:5501/v2/_catalog" && echo

echo "[seed] Example registries config value:"
echo "  [\"localhost:5500/aria-assets\", \"localhost:5501/aria-assets\"]"
