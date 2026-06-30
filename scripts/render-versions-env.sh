#!/usr/bin/env bash
set -euo pipefail

# Single source of truth - parameters.prod.json
#
# Usage:
#   scripts/render-versions-env.sh > docker/versions.prod.env
#   scripts/render-versions-env.sh docker/versions.prod.env

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
OUT_FILE="${1:-/dev/stdout}"

[[ -f "$PARAM_FILE" ]] || { echo "[render-versions-env] parameter file not found: $PARAM_FILE" >&2; exit 1; }

if [[ "$OUT_FILE" != "/dev/stdout" ]]; then
  mkdir -p "$(dirname "$OUT_FILE")"
fi

infra() { jq -r --arg k "$1" '.parameters.infra.value[$k].image' "$PARAM_FILE"; }
build()  { jq -r --arg k "$1" '.parameters.buildImages.value[$k]' "$PARAM_FILE"; }

{
  echo "# Pinned container versions for CI, deployment, and prod-like local runs."
  echo "# GENERATED FILE - do not edit by hand. Source of truth: $PARAM_FILE"
  echo "# Regenerate: scripts/render-versions-env.sh > docker/versions.prod.env"
  echo "# Use: docker compose --env-file docker/versions.prod.env up --build"
  echo ""
  echo "# Infrastructure (Compose services)"
  echo "POSTGRES_IMAGE=$(infra postgres)"
  echo "REDIS_IMAGE=$(infra redis)"
  echo "AZURITE_IMAGE=$(infra azurite)"
  echo "PROMETHEUS_IMAGE=$(infra prometheus)"
  echo "POSTGRES_EXPORTER_IMAGE=$(infra postgresExporter)"
  echo "REDIS_EXPORTER_IMAGE=$(infra redisExporter)"
  echo "GRAFANA_IMAGE=$(infra grafana)"
  echo ""
  echo "# Application build bases (Docker build-args)"
  echo "DOTNET_SDK_IMAGE=$(build dotnetSdk)"
  echo "DOTNET_ASPNET_IMAGE=$(build dotnetAspnet)"
  echo "NODE_IMAGE=$(build node)"
  echo "NGINX_IMAGE=$(build nginx)"
  echo "RUST_IMAGE=$(build rust)"
  echo "DEBIAN_IMAGE=$(build debian)"
} > "$OUT_FILE"

if grep -q '=null$' "$OUT_FILE" 2>/dev/null; then
  echo "[render-versions-env] ERROR: one or more keys resolved to null - check $PARAM_FILE" >&2
  grep '=null$' "$OUT_FILE" >&2
  exit 1
fi