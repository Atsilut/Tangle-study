#!/usr/bin/env bash
set -euo pipefail

: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "========================================"
echo "[DEPLOY][BUILD] PHASE 1 - BUILD & PUSH"
echo "========================================"

IMAGES=(
  tangle-study-api
  tangle-study-web

  tangle-study-worker-chat
  tangle-study-worker-location
  tangle-study-worker-media

  tangle-study-redis
  tangle-study-redis-exporter

  tangle-study-prometheus
  tangle-study-grafana
  tangle-study-postgres-exporter
)

build_push() {
  local image="$1"
  local tag="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  case "$image" in
    tangle-study-api)
      docker build -f services/Api/Dockerfile \
        --build-arg DOTNET_SDK_IMAGE="${DOTNET_SDK_IMAGE}" \
        --build-arg DOTNET_ASPNET_IMAGE="${DOTNET_ASPNET_IMAGE}" \
        -t "$tag" .
      ;;
    tangle-study-web)
      docker build -f clients/web/Dockerfile \
        --build-arg NODE_IMAGE="${NODE_IMAGE}" \
        --build-arg NGINX_IMAGE="${NGINX_IMAGE}" \
        -t "$tag" .
      ;;
    tangle-study-worker-*)
      docker build -f workers/rust-worker/Dockerfile \
        --build-arg RUST_IMAGE="${RUST_IMAGE}" \
        --build-arg DEBIAN_IMAGE="${DEBIAN_IMAGE}" \
        -t "$tag" .
      ;;
    *)
      docker build -f infra/${image}/Dockerfile \
        -t "$tag" .
      ;;
  esac

  docker push "$tag"
}

failed=0

for img in "${IMAGES[@]}"; do
  echo "==> BUILD ${img}"
  if ! build_push "$img"; then
    echo "[FATAL] build failed: ${img}" >&2
    failed=1
  fi
done

[[ $failed -eq 0 ]] || exit 1

echo "========================================"
echo "[DEPLOY][BUILD] DONE"
echo "========================================"