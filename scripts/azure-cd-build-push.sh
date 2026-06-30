#!/usr/bin/env bash
set -euo pipefail

: "${CONTAINER_REGISTRY:?}"
: "${IMAGE_TAG:?}"

echo "========================================"
echo "[DEPLOY] BUILD & PUSH"
echo "========================================"

# ----------------------------
# 1. ORDERED LIST
# ----------------------------
IMAGES=(
  tangle-study-api
  tangle-study-web

  tangle-study-worker-chat
  tangle-study-worker-location
  tangle-study-worker-media
)

# ----------------------------
# 2. DOCKERFILE MAP
# ----------------------------
declare -A DOCKERFILE_MAP=(
  [tangle-study-api]="services/Api/Dockerfile"
  [tangle-study-web]="clients/web/Dockerfile"

  [tangle-study-worker-chat]="workers/rust-worker/Dockerfile"
  [tangle-study-worker-location]="workers/rust-worker/Dockerfile"
  [tangle-study-worker-media]="workers/rust-worker/Dockerfile"
)

build_args() {
  case "$1" in
    tangle-study-api)
      echo "--build-arg DOTNET_SDK_IMAGE=$DOTNET_SDK_IMAGE --build-arg DOTNET_ASPNET_IMAGE=$DOTNET_ASPNET_IMAGE"
      ;;
    tangle-study-web)
      echo "--build-arg NODE_IMAGE=$NODE_IMAGE --build-arg NGINX_IMAGE=$NGINX_IMAGE"
      ;;
    tangle-study-worker-*)
      echo "--build-arg RUST_IMAGE=$RUST_IMAGE --build-arg DEBIAN_IMAGE=$DEBIAN_IMAGE"
      ;;
    *)
      echo ""
      ;;
  esac
}

build_push() {
  local image="$1"
  local dockerfile="${DOCKERFILE_MAP[$image]}"
  local tag="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  echo ""
  echo "========================================"
  echo "[BUILD START]"
  echo "image      = $image"
  echo "dockerfile = $dockerfile"
  echo "tag        = $tag"
  echo "pwd        = $(pwd)"
  echo "========================================"

  if [[ -z "$dockerfile" ]]; then
    echo "[FATAL] Dockerfile mapping not found for image: $image" >&2
    exit 1
  fi

  if [[ ! -f "$dockerfile" ]]; then
    echo "[FATAL] Dockerfile does NOT exist!" >&2
    echo "        image      : $image" >&2
    echo "        expected   : $dockerfile" >&2
    echo "        working dir: $(pwd)" >&2
    echo "" >&2

    echo "[DEBUG] directory listing around expected path:" >&2
    ls -al "$(dirname "$dockerfile")" || true >&2

    exit 1
  fi

  local args
  args="$(build_args "$image")"

  echo "[INFO] build args: $args"

  echo "[INFO] running docker build..."

  if ! docker build \
      -f "$dockerfile" \
      $args \
      -t "$tag" \
      .; then

    echo ""
    echo "[FATAL] docker build failed"
    echo "        image      : $image"
    echo "        dockerfile : $dockerfile"
    echo "        tag        : $tag"
    echo "        context    : $(pwd)"
    exit 1
  fi

  echo "[INFO] pushing image..."
  docker push "$tag"

  echo "[SUCCESS] $image"
}

for img in "${IMAGES[@]}"; do
  build_push "$img"
done

echo "========================================"
echo "[DEPLOY] DONE"
echo "========================================"