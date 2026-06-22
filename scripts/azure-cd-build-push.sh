#!/usr/bin/env bash
# Build and push Tangle images to GHCR for Azure CD.
#
# Required env:
#   CONTAINER_REGISTRY   e.g. ghcr.io/my-org/tangle-study
#   IMAGE_TAG
#
# Optional: load build-args from docker/versions.prod.env (source before calling).
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

DOTNET_SDK_IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
DOTNET_ASPNET_IMAGE="${DOTNET_ASPNET_IMAGE:-mcr.microsoft.com/dotnet/aspnet:10.0}"
NODE_IMAGE="${NODE_IMAGE:-node:26.3-bookworm-slim}"
NGINX_IMAGE="${NGINX_IMAGE:-nginx:1.31.1-alpine}"
RUST_IMAGE="${RUST_IMAGE:-rust:1.96.0-bookworm}"
DEBIAN_IMAGE="${DEBIAN_IMAGE:-debian:bookworm-slim}"

build_push() {
  local dockerfile="$1"
  local image_name="$2"
  shift 2
  local -a build_args=("$@")
  local tag="${CONTAINER_REGISTRY}/${image_name}:${IMAGE_TAG}"

  echo "==> Building ${image_name} -> ${tag}"
  docker build -f "$dockerfile" "${build_args[@]}" -t "$tag" .
  docker push "$tag"
}

build_push services/Api/Dockerfile tangle-api \
  --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE}" \
  --build-arg "DOTNET_ASPNET_IMAGE=${DOTNET_ASPNET_IMAGE}"

build_push clients/web/Dockerfile tangle-web \
  --build-arg "NGINX_CONF=nginx.production.conf" \
  --build-arg "NODE_IMAGE=${NODE_IMAGE}" \
  --build-arg "NGINX_IMAGE=${NGINX_IMAGE}"

build_push workers/rust-worker/Dockerfile tangle-worker \
  --build-arg "RUST_IMAGE=${RUST_IMAGE}" \
  --build-arg "DEBIAN_IMAGE=${DEBIAN_IMAGE}"

echo "==> Pushed images to ${CONTAINER_REGISTRY} with tag ${IMAGE_TAG}"
