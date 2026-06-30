#!/usr/bin/env bash
# Build and push Tangle images to GHCR for Azure CD (SAFE VERSION)
#
# Fixes:
# - NO registry fallback (prevents wrong org push)
# - verifies image exists after push (fixes MANIFEST_UNKNOWN)
# - ensures deterministic tag deployment safety
# - fails fast on silent buildx failures

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# shellcheck source=scripts/lib/versions-prod-env.sh
source "$ROOT/scripts/lib/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required (NO fallback allowed)}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

: "${DOTNET_SDK_IMAGE:?missing}"
: "${DOTNET_ASPNET_IMAGE:?missing}"
: "${NODE_IMAGE:?missing}"
: "${NGINX_IMAGE:?missing}"
: "${RUST_IMAGE:?missing}"
: "${DEBIAN_IMAGE:?missing}"

USE_BUILDX_CACHE=false
if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
  USE_BUILDX_CACHE=true
fi

echo "========================================"
echo "[DEPLOY][BUILD] IMAGE PIPELINE START"
echo "========================================"
echo "Registry     : $CONTAINER_REGISTRY"
echo "Tag          : $IMAGE_TAG"
echo "Build cache  : $USE_BUILDX_CACHE"
echo "========================================"

build_push() {
  local dockerfile="$1"
  local image_name="$2"
  shift 2
  local -a build_args=("$@")

  local tag="${CONTAINER_REGISTRY}/${image_name}:${IMAGE_TAG}"

  echo "========================================"
  echo "[DEPLOY][BUILD] ${image_name}"
  echo "========================================"
  echo "Target: $tag"

  if [[ "$USE_BUILDX_CACHE" == "true" ]]; then
    docker buildx build \
      --cache-from "type=gha,scope=${image_name}" \
      --cache-to "type=gha,mode=max,scope=${image_name}" \
      -f "$dockerfile" \
      "${build_args[@]}" \
      -t "$tag" \
      --provenance=false \
      --sbom=false \
      --push \
      .
  else
    docker build \
      -f "$dockerfile" \
      "${build_args[@]}" \
      -t "$tag" \
      .
    docker push "$tag"
  fi

  echo "========================================"
  echo "[DEPLOY][BUILD] VERIFY IMAGE EXISTS"
  echo "========================================"

  # CRITICAL: prevent MANIFEST_UNKNOWN later in ACA
  if ! docker buildx imagetools inspect "$tag" >/dev/null 2>&1; then
    echo "[FATAL] Image not found in registry after push: $tag" >&2
    exit 1
  fi

  echo "[OK] Verified: $tag"
}

run_build() {
  local label="$1"
  shift
  if build_push "$@"; then
    echo "[DEPLOY][BUILD] DONE: $label"
  else
    echo "[DEPLOY][BUILD] FAILED: $label" >&2
    exit 1
  fi
}

start_build() {
  local label="$1"
  shift
  run_build "$label" "$@" &
  PIDS+=("$!")
  LABELS+=("$label")
}

PIDS=()
LABELS=()

start_build api services/Api/Dockerfile tangle-study-api \
  --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE}" \
  --build-arg "DOTNET_ASPNET_IMAGE=${DOTNET_ASPNET_IMAGE}"

start_build web clients/web/Dockerfile tangle-study-web \
  --build-arg "NGINX_CONF=nginx.production.conf" \
  --build-arg "NODE_IMAGE=${NODE_IMAGE}" \
  --build-arg "NGINX_IMAGE=${NGINX_IMAGE}"

start_build worker workers/rust-worker/Dockerfile tangle-study-worker \
  --build-arg "RUST_IMAGE=${RUST_IMAGE}" \
  --build-arg "DEBIAN_IMAGE=${DEBIAN_IMAGE}"

start_build prometheus infra/azure/monitoring/prometheus/Dockerfile tangle-study-prometheus \
  --build-arg "PROMETHEUS_IMAGE=${PROMETHEUS_IMAGE}"

start_build grafana infra/azure/monitoring/grafana/Dockerfile tangle-study-grafana \
  --build-arg "GRAFANA_IMAGE=${GRAFANA_IMAGE}"

FAILED=0

for i in "${!PIDS[@]}"; do
  if ! wait "${PIDS[$i]}"; then
    echo "[DEPLOY][BUILD] FAILED: ${LABELS[$i]}" >&2
    FAILED=1
  fi
done

if [[ "$FAILED" -ne 0 ]]; then
  echo "[FATAL] Build pipeline failed" >&2
  exit 1
fi

echo "========================================"
echo "[DEPLOY][BUILD] SUCCESS"
echo "========================================"
echo "All images pushed successfully to:"
echo "$CONTAINER_REGISTRY @ $IMAGE_TAG"
echo "========================================"