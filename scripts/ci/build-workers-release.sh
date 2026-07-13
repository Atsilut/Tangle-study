#!/usr/bin/env bash
# Cargo test + release build for worker-media, worker-chat, and worker-location.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="${LOG_PREFIX:-[CI][RUST]}"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/ci/libs/ci-cache.sh
source "$ROOT/scripts/ci/libs/ci-cache.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

SKIP_TESTS="${SKIP_TESTS:-0}"

require_release_binary() {
  local path="$1"
  [[ -f "${ROOT}/${path}" ]] || fail "missing release binary: ${path} (run build-workers-release.sh)"
}

log_step "PREPARE CARGO CACHE"
mkdir -p .ci-cache/cargo/registry .ci-cache/cargo/git workers/target

if [[ "$SKIP_TESTS" != "1" ]]; then
  log_step "RUST WORKER TESTS"
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    -v "${ROOT}:/src" \
    -v "${ROOT}/.ci-cache/cargo/registry:/usr/local/cargo/registry" \
    -v "${ROOT}/.ci-cache/cargo/git:/usr/local/cargo/git" \
    -v "${ROOT}/workers/target:/src/workers/target" \
    -w /src/workers \
    "${RUST_IMAGE:?RUST_IMAGE is required}" \
    cargo test --workspace
fi

log_step "RUST WORKER RELEASE BUILD"
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -v "${ROOT}:/src" \
  -v "${ROOT}/.ci-cache/cargo/registry:/usr/local/cargo/registry" \
  -v "${ROOT}/.ci-cache/cargo/git:/usr/local/cargo/git" \
  -v "${ROOT}/workers/target:/src/workers/target" \
  -w /src/workers \
  "${RUST_IMAGE:?RUST_IMAGE is required}" \
  cargo build --release -p worker-media -p worker-chat -p worker-location

require_release_binary "workers/target/release/worker-media"
require_release_binary "workers/target/release/worker-chat"
require_release_binary "workers/target/release/worker-location"

ci_fix_cache_ownership

log_info "rust worker release build completed"
