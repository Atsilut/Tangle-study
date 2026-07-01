#!/usr/bin/env bash
set -euo pipefail

# Single source of truth - parameters.prod.json
#
# Usage:
#   scripts/ci/render-versions-env.sh > docker/versions.prod.env
#   scripts/ci/render-versions-env.sh docker/versions.prod.env

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
OUT_FILE="${1:-/dev/stdout}"

# shellcheck source=scripts/ci/libs/read-parameters.sh
source "$ROOT/scripts/ci/libs/read-parameters.sh"

[[ -f "$PARAM_FILE" ]] || { echo "[render-versions-env] parameter file not found: $PARAM_FILE" >&2; exit 1; }

if [[ "$OUT_FILE" != "/dev/stdout" ]]; then
  mkdir -p "$(dirname "$OUT_FILE")"
fi

{
  echo "# Pinned container versions for CI, deployment, and prod-like local runs."
  echo "# GENERATED FILE - do not edit by hand. Source of truth: $PARAM_FILE"
  echo "# Regenerate: scripts/ci/render-versions-env.sh > docker/versions.prod.env"
  echo "# Use: docker compose --env-file docker/versions.prod.env up --build"
  echo ""
  echo "# Infrastructure (Compose services)"
  echo "POSTGRES_IMAGE=$(param_infra_image postgres)"
  echo "REDIS_IMAGE=$(param_infra_image redis)"
  echo "AZURITE_IMAGE=$(param_infra_image azurite)"
  echo "PROMETHEUS_IMAGE=$(param_infra_image prometheus)"
  echo "POSTGRES_EXPORTER_IMAGE=$(param_infra_image postgresExporter)"
  echo "REDIS_EXPORTER_IMAGE=$(param_infra_image redisExporter)"
  echo "GRAFANA_IMAGE=$(param_infra_image grafana)"
  echo ""
  echo "# Application build bases (Docker build-args)"
  echo "DOTNET_SDK_IMAGE=$(param_build_image dotnetSdk)"
  echo "DOTNET_ASPNET_IMAGE=$(param_build_image dotnetAspnet)"
  echo "NODE_IMAGE=$(param_build_image node)"
  echo "NGINX_IMAGE=$(param_build_image nginx)"
  echo "RUST_IMAGE=$(param_build_image rust)"
  echo "DEBIAN_IMAGE=$(param_build_image debian)"
} > "$OUT_FILE"

if grep -q '=null$' "$OUT_FILE" 2>/dev/null; then
  echo "[render-versions-env] ERROR: one or more keys resolved to null - check $PARAM_FILE" >&2
  grep '=null$' "$OUT_FILE" >&2
  exit 1
fi
