#!/usr/bin/env bash
# Build and push Tangle images to GHCR for Azure CD.
#
# Required env:
#   CONTAINER_REGISTRY   e.g. ghcr.io/my-org/tangle-study
#   IMAGE_TAG
#
# Build-args are loaded from docker/versions.prod.env (see COMPOSE_ENV_FILE).
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/lib/versions-prod-env.sh
source "$ROOT/scripts/lib/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"
: "${DOTNET_SDK_IMAGE:?DOTNET_SDK_IMAGE missing — set in docker/versions.prod.env}"
: "${DOTNET_ASPNET_IMAGE:?DOTNET_ASPNET_IMAGE missing — set in docker/versions.prod.env}"
: "${NODE_IMAGE:?NODE_IMAGE missing — set in docker/versions.prod.env}"
: "${NGINX_IMAGE:?NGINX_IMAGE missing — set in docker/versions.prod.env}"
: "${RUST_IMAGE:?RUST_IMAGE missing — set in docker/versions.prod.env}"
: "${DEBIAN_IMAGE:?DEBIAN_IMAGE missing — set in docker/versions.prod.env}"

echo "==> Build bases from $(versions_prod_env_path "$ROOT")"
echo "    DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE}"
echo "    DOTNET_ASPNET_IMAGE=${DOTNET_ASPNET_IMAGE}"
echo "    NODE_IMAGE=${NODE_IMAGE}"
echo "    NGINX_IMAGE=${NGINX_IMAGE}"
echo "    RUST_IMAGE=${RUST_IMAGE}"
echo "    DEBIAN_IMAGE=${DEBIAN_IMAGE}"

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

build_push services/Api/Dockerfile tangle-study-api \
  --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE}" \
  --build-arg "DOTNET_ASPNET_IMAGE=${DOTNET_ASPNET_IMAGE}"

build_push clients/web/Dockerfile tangle-study-web \
  --build-arg "NGINX_CONF=nginx.production.conf" \
  --build-arg "NODE_IMAGE=${NODE_IMAGE}" \
  --build-arg "NGINX_IMAGE=${NGINX_IMAGE}"

build_push workers/rust-worker/Dockerfile tangle-study-worker \
  --build-arg "RUST_IMAGE=${RUST_IMAGE}" \
  --build-arg "DEBIAN_IMAGE=${DEBIAN_IMAGE}"

echo "==> Pushed images to ${CONTAINER_REGISTRY} with tag ${IMAGE_TAG}"
