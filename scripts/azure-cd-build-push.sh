#!/usr/bin/env bash
# =========================================================
# [DEPLOY][BUILD] GHCR IMAGE BUILD PIPELINE
# =========================================================

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

source "$ROOT/scripts/lib/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"


########################################
log_step() { echo ""; echo "========================================"; echo "[DEPLOY][BUILD][STEP] $*"; echo "========================================"; }
log_info() { echo "[DEPLOY][BUILD][INFO] $*"; }
log_error() { echo "[DEPLOY][BUILD][ERROR] $*" >&2; }


########################################
log_step "ENVIRONMENT SUMMARY"

log_info "registry=$CONTAINER_REGISTRY"
log_info "tag=$IMAGE_TAG"
log_info "dotnet_sdk=$DOTNET_SDK_IMAGE"
log_info "node=$NODE_IMAGE"
log_info "nginx=$NGINX_IMAGE"


########################################
build_one() {
  local name="$1"
  local dockerfile="$2"
  shift 2

  local tag="${CONTAINER_REGISTRY}/${name}:${IMAGE_TAG}"

  echo ""
  echo "========================================"
  echo "[DEPLOY][BUILD][IMAGE] $name"
  echo "[DEPLOY][BUILD][TARGET] $tag"
  echo "========================================"

  if docker buildx build \
      --provenance=false \
      --sbom=false \
      -f "$dockerfile" \
      -t "$tag" \
      "$@" \
      --push \
      .; then
    echo "[DEPLOY][BUILD][OK] $name"
  else
    echo "[DEPLOY][BUILD][FAIL] $name" >&2
    return 1
  fi
}


########################################
log_step "PARALLEL BUILD START"

pids=()
labels=()

build_async() {
  build_one "$@" &
  pids+=("$!")
  labels+=("$1")
}


build_async api services/Api/Dockerfile \
  --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE}" \
  --build-arg "DOTNET_ASPNET_IMAGE=${DOTNET_ASPNET_IMAGE}"

build_async web clients/web/Dockerfile \
  --build-arg "NODE_IMAGE=${NODE_IMAGE}" \
  --build-arg "NGINX_IMAGE=${NGINX_IMAGE}" \
  --build-arg "NGINX_CONF=nginx.production.conf"

build_async worker workers/rust-worker/Dockerfile \
  --build-arg "RUST_IMAGE=${RUST_IMAGE}" \
  --build-arg "DEBIAN_IMAGE=${DEBIAN_IMAGE}"


########################################
log_step "WAIT BUILD RESULTS"

failed=0

for i in "${!pids[@]}"; do
  if ! wait "${pids[$i]}"; then
    log_error "image-failed=${labels[$i]}"
    failed=1
  fi
done


########################################
log_step "FINAL RESULT"

if [[ "$failed" -ne 0 ]]; then
  log_error "status=FAILED"
  exit 1
fi

log_info "status=SUCCESS registry=$CONTAINER_REGISTRY tag=$IMAGE_TAG"