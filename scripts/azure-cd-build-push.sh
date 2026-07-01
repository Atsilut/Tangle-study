#!/usr/bin/env bash
set -euo pipefail

export DOCKER_BUILDKIT=1

: "${CONTAINER_REGISTRY:?}"
: "${IMAGE_TAG:?}"

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
NGINX_PROD_CONF="infra/nginx/nginx.production.conf"

if [[ ! -f "$PARAM_FILE" ]]; then
  echo "[FATAL] missing parameter file: $PARAM_FILE" >&2
  exit 1
fi

echo "========================================"
echo "[DEPLOY] BUILD & PUSH"
echo "========================================"

# ----------------------------
# 1. PARSE CONFIG
# ----------------------------
echo "[INFO] Reading build image tags from $PARAM_FILE..."
DOTNET_SDK=$(jq -r '.parameters.buildImages.value.dotnetSdk' "$PARAM_FILE")
DOTNET_ASPNET=$(jq -r '.parameters.buildImages.value.dotnetAspnet' "$PARAM_FILE")
NODE_IMG=$(jq -r '.parameters.buildImages.value.node' "$PARAM_FILE")
NGINX_IMG=$(jq -r '.parameters.buildImages.value.nginx' "$PARAM_FILE")
RUST_IMG=$(jq -r '.parameters.buildImages.value.rust' "$PARAM_FILE")
DEBIAN_IMG=$(jq -r '.parameters.buildImages.value.debian' "$PARAM_FILE")

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

  echo ""
  echo "========================================"
  echo "[BUILD START] $image"
  echo "========================================"

  if [[ -z "$dockerfile" || ! -f "$dockerfile" ]]; then
    echo "[FATAL] Dockerfile missing or not mapped for: $image" >&2
    ls -al "$(dirname "${dockerfile:-.}")" || true >&2
    exit 1
  fi

  set_build_args "$image"

  echo "[INFO] Running docker build for $image..."
  if ! docker build \
      -f "$dockerfile" \
      "${BUILD_ARGS[@]}" \
      -t "$tag" \
      .; then
    echo "[FATAL] docker build failed for $image" >&2
    exit 1
  fi

  echo "[INFO] Pushing image to registry..."
  docker push "$tag"
  echo "[SUCCESS] $image"
}

# ----------------------------
# 4. EXECUTION LOOP
# ----------------------------
for img in "${IMAGES[@]}"; do
  build_push "$img"
done

echo "========================================"
echo "[DEPLOY] DONE"
echo "========================================"