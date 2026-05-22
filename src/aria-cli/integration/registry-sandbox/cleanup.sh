#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "[cleanup] Stopping registry sandbox and removing volumes..."
docker compose -f "$ROOT_DIR/docker-compose.yml" down -v

echo "[cleanup] Done"
