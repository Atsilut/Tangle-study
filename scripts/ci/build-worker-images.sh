#!/usr/bin/env bash
# Build slim runtime worker images from workers/target/release (see build-workers-release.sh).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][WORKER]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

MEDIA_ONLY=0
CHAT_ONLY=0
LOCATION_ONLY=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --media-only) MEDIA_ONLY=1; shift ;;
    --chat-only) CHAT_ONLY=1; shift ;;
    --location-only) LOCATION_ONLY=1; shift ;;
    -h|--help)
      cat <<'EOF'
Usage: ./scripts/ci/build-worker-images.sh [--media-only] [--chat-only] [--location-only]

Builds runtime worker images from workers/target/release binaries.
Requires: ./scripts/ci/build-workers-release.sh

Env (optional tags for CD):
  WORKER_MEDIA_IMAGE      default: tangle-study-worker-media:local
  WORKER_CHAT_IMAGE       default: tangle-study-worker-chat:local
  WORKER_LOCATION_IMAGE   default: tangle-study-worker-location:local
EOF
      exit 0
      ;;
    *) fail "unknown option: $1 (try --help)" ;;
  esac
done

WORKER_MEDIA_IMAGE="${WORKER_MEDIA_IMAGE:-tangle-study-worker-media:local}"
WORKER_CHAT_IMAGE="${WORKER_CHAT_IMAGE:-tangle-study-worker-chat:local}"
WORKER_LOCATION_IMAGE="${WORKER_LOCATION_IMAGE:-tangle-study-worker-location:local}"

require_release_binary() {
  local path="$1"
  [[ -f "${ROOT}/${path}" ]] || fail "missing ${path} — run ./scripts/ci/build-workers-release.sh first"
}

selected=$((MEDIA_ONLY + CHAT_ONLY + LOCATION_ONLY))
if [[ "$selected" -gt 1 ]]; then
  fail "use at most one of --media-only, --chat-only, or --location-only"
fi

build_image() {
  local dockerfile="$1"
  local tag="$2"
  log_step "BUILD ${tag}"
  docker build \
    -f "${ROOT}/${dockerfile}" \
    --build-arg "DEBIAN_IMAGE=${DEBIAN_IMAGE:?DEBIAN_IMAGE is required}" \
    -t "$tag" \
    "$ROOT"
}

if [[ "$CHAT_ONLY" -eq 1 ]]; then
  require_release_binary "workers/target/release/worker-chat"
  build_image "workers/docker/Dockerfile.runtime.chat" "$WORKER_CHAT_IMAGE"
  log_info "worker runtime images built"
  exit 0
fi

if [[ "$LOCATION_ONLY" -eq 1 ]]; then
  require_release_binary "workers/target/release/worker-location"
  build_image "workers/docker/Dockerfile.runtime.location" "$WORKER_LOCATION_IMAGE"
  log_info "worker runtime images built"
  exit 0
fi

require_release_binary "workers/target/release/worker-media"
if [[ "$MEDIA_ONLY" -eq 0 ]]; then
  require_release_binary "workers/target/release/worker-chat"
  require_release_binary "workers/target/release/worker-location"
fi

build_image "workers/docker/Dockerfile.runtime.media" "$WORKER_MEDIA_IMAGE"

if [[ "$MEDIA_ONLY" -eq 0 ]]; then
  build_image "workers/docker/Dockerfile.runtime.chat" "$WORKER_CHAT_IMAGE"
  build_image "workers/docker/Dockerfile.runtime.location" "$WORKER_LOCATION_IMAGE"
fi

log_info "worker runtime images built"
