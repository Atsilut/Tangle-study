#!/usr/bin/env bash
set -euo pipefail

export DOCKER_BUILDKIT=1

: "${CONTAINER_REGISTRY:?}"
: "${IMAGE_TAG:?}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEPLOY_LOG_PREFIX="[DEPLOY][BUILD]"
# shellcheck source=scripts/cd/libs/common.sh
source "$ROOT/scripts/cd/libs/common.sh"

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
NGINX_PROD_CONF="infra/nginx/nginx.production.conf"

[[ -f "$PARAM_FILE" ]] || fail "missing parameter file: $PARAM_FILE"

log_step "BUILD & PUSH"

# shellcheck source=scripts/ci/libs/read-parameters.sh
source "$ROOT/scripts/ci/libs/read-parameters.sh"

# ----------------------------
# 1. PARSE CONFIG
# ----------------------------
log_info "Reading build image tags from $PARAM_FILE..."
DOTNET_SDK="$(param_build_image dotnetSdk)"
DOTNET_ASPNET="$(param_build_image dotnetAspnet)"
NODE_IMG="$(param_build_image node)"
NGINX_IMG="$(param_build_image nginx)"
RUST_IMG="$(param_build_image rust)"
DEBIAN_IMG="$(param_build_image debian)"

# ----------------------------
# 2. OPTIMIZED IMAGE LIST
# ----------------------------
IMAGES=(
  tangle-study-api
  tangle-study-web
  tangle-study-worker
)

declare -A DOCKERFILE_MAP=(
  [tangle-study-api]="services/Api/Dockerfile"
  [tangle-study-web]="clients/web/Dockerfile"
  [tangle-study-worker]="workers/rust-worker/Dockerfile"
)

# ----------------------------
# 3. BUILD FUNCTIONS
# ----------------------------
set_build_args() {
  local image="$1"
  BUILD_ARGS=()

  case "$image" in
    tangle-study-api)
      BUILD_ARGS+=("--build-arg" "DOTNET_SDK_IMAGE=$DOTNET_SDK" "--build-arg" "DOTNET_ASPNET_IMAGE=$DOTNET_ASPNET")
      ;;
    tangle-study-web)
      BUILD_ARGS+=(
        "--build-arg" "NODE_IMAGE=$NODE_IMG"
        "--build-arg" "NGINX_IMAGE=$NGINX_IMG"
        "--build-arg" "NGINX_CONF=$NGINX_PROD_CONF"
      )
      ;;
    tangle-study-worker)
      BUILD_ARGS+=("--build-arg" "RUST_IMAGE=$RUST_IMG" "--build-arg" "DEBIAN_IMAGE=$DEBIAN_IMG")
      ;;
  esac
}

build_push() {
  local image="$1"
  local dockerfile="${DOCKERFILE_MAP[$image]}"
  local tag="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  log_step "START $image"

  if [[ -z "$dockerfile" || ! -f "$dockerfile" ]]; then
    log_error "Dockerfile missing or not mapped for: $image"
    ls -al "$(dirname "${dockerfile:-.}")" || true >&2
    exit 1
  fi

  set_build_args "$image"

  log_info "Running docker build for $image..."
  if ! docker build \
      -f "$dockerfile" \
      "${BUILD_ARGS[@]}" \
      -t "$tag" \
      .; then
    fail "docker build failed for $image"
  fi

  log_info "Pushing image to registry..."
  docker push "$tag"
  log_info "SUCCESS $image"
}

# ----------------------------
# 4. EXECUTION LOOP
# ----------------------------
for img in "${IMAGES[@]}"; do
  build_push "$img"
done

log_step "DONE"
