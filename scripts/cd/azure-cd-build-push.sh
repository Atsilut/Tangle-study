#!/usr/bin/env bash
set -euo pipefail

export DOCKER_BUILDKIT=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[DEPLOY][BUILD]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

require_env CONTAINER_REGISTRY
require_env IMAGE_TAG

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
NGINX_PROD_CONF="infra/nginx/nginx.production.conf"
COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-docker/versions.prod.env}"

[[ -f "$PARAM_FILE" ]] || fail "missing parameter file: $PARAM_FILE"

log_step "BUILD & PUSH"

# shellcheck source=scripts/ci/libs/read-parameters.sh
source "$ROOT/scripts/ci/libs/read-parameters.sh"

############################################
log_step "PARSE CONFIG"

log_info "reading build image tags from $PARAM_FILE"
DOTNET_SDK="$(param_build_image dotnetSdk)"
DOTNET_ASPNET="$(param_build_image dotnetAspnet)"
NODE_IMG="$(param_build_image node)"
NGINX_IMG="$(param_build_image nginx)"
RUST_IMG="$(param_build_image rust)"
DEBIAN_IMG="$(param_build_image debian)"
PROMETHEUS_IMG="$(param_infra_image prometheus)"
GRAFANA_IMG="$(param_infra_image grafana)"

############################################
log_step "RENDER PINNED VERSIONS"
bash "$ROOT/scripts/ci/render-versions-env.sh" "$COMPOSE_ENV_FILE"

############################################
log_step "COMPILE ARTIFACTS"
export COMPOSE_ENV_FILE
SKIP_TESTS=1 bash "$ROOT/scripts/ci/build-workers-release.sh"
bash "$ROOT/scripts/ci/dotnet-publish.sh"

############################################
log_step "BUILD IMAGES"

IMAGES=(
  tangle-study-web
  tangle-study-worker-media
  tangle-study-worker-chat
  tangle-study-worker-location
  tangle-study-prometheus
  tangle-study-grafana
)

declare -A DOCKERFILE_MAP=(
  [tangle-study-web]="clients/web/Dockerfile"
  [tangle-study-worker-media]="workers/docker/Dockerfile.runtime.media"
  [tangle-study-worker-chat]="workers/docker/Dockerfile.runtime.chat"
  [tangle-study-worker-location]="workers/docker/Dockerfile.runtime.location"
  [tangle-study-prometheus]="infra/azure/monitoring/prometheus/Dockerfile"
  [tangle-study-grafana]="infra/azure/monitoring/grafana/Dockerfile"
)

set_build_args() {
  local image="$1"
  BUILD_ARGS=()

  case "$image" in
    tangle-study-web)
      BUILD_ARGS+=(
        "--build-arg" "NODE_IMAGE=$NODE_IMG"
        "--build-arg" "NGINX_IMAGE=$NGINX_IMG"
        "--build-arg" "NGINX_CONF=$NGINX_PROD_CONF"
      )
      ;;
    tangle-study-worker-media|tangle-study-worker-chat|tangle-study-worker-location)
      BUILD_ARGS+=("--build-arg" "DEBIAN_IMAGE=$DEBIAN_IMG")
      ;;
    tangle-study-prometheus)
      BUILD_ARGS+=("--build-arg" "PROMETHEUS_IMAGE=$PROMETHEUS_IMG")
      ;;
    tangle-study-grafana)
      BUILD_ARGS+=("--build-arg" "GRAFANA_IMAGE=$GRAFANA_IMG")
      ;;
  esac
}

build_push() {
  local image="$1"
  local dockerfile="${DOCKERFILE_MAP[$image]}"
  local tag="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  log_step "START $image"

  if [[ -z "$dockerfile" || ! -f "$dockerfile" ]]; then
    log_error "dockerfile missing or not mapped for: $image"
    ls -al "$(dirname "${dockerfile:-.}")" || true >&2
    fail "dockerfile missing or not mapped for: $image"
  fi

  set_build_args "$image"

  log_info "running docker build for $image"
  if ! docker build \
      -f "$dockerfile" \
      "${BUILD_ARGS[@]}" \
      -t "$tag" \
      .; then
    fail "docker build failed for $image"
  fi

  log_info "pushing image to registry"
  docker push "$tag"
  log_info "success $image"
}

for img in "${IMAGES[@]}"; do
  build_push "$img"
done

log_step "DONE"
