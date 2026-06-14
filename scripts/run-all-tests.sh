#!/usr/bin/env bash
# Run API (.NET), Rust worker, media harness, and web (Vitest) test suites.
# Requires Docker Desktop (API + harness use compose; Rust + web run in one-off containers).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# Git Bash on Windows: use a Windows host path for bind mounts and stop MSYS from
# rewriting -w /src/... into C:/Program Files/Git/src/...
if command -v pwd.exe >/dev/null 2>&1; then
  DOCKER_ROOT="$(pwd -W)"
else
  DOCKER_ROOT="$ROOT"
fi
export MSYS_NO_PATHCONV=1

RUST_IMAGE="${RUST_IMAGE:-rust:1.96.0-bookworm}"
NODE_IMAGE="${NODE_IMAGE:-node:26.3-bookworm-slim}"

run_rust() {
  echo "==> Rust worker unit tests"
  docker run --rm \
    -v "${DOCKER_ROOT}:/src" \
    -w /src/workers/rust-worker \
    "$RUST_IMAGE" \
    cargo test
}

run_api() {
  echo "==> API integration tests (Testcontainers)"
  "$ROOT/scripts/docker-test.sh" "$@"
}

run_harness() {
  echo "==> Media harness (E2E: API + rust-worker-media)"
  "$ROOT/scripts/run-media-harness.sh"
}

run_web() {
  echo "==> Web client tests (Vitest)"
  docker run --rm \
    -v "${DOCKER_ROOT}:/src" \
    -w /src/clients/web \
    "$NODE_IMAGE" \
    sh -c "npm ci --no-audit --no-fund && npm run test"
}

usage() {
  cat <<'EOF'
Usage: ./scripts/run-all-tests.sh [options] [-- api-test-args...]

Runs, in order:
  1. Rust worker unit tests (cargo test in Docker)
  2. API integration tests (docker-test.sh / Testcontainers)
  3. Media harness E2E (run-media-harness.sh)
  4. Web client tests (Vitest in Docker)

Options:
  --parallel-frontend   Run Rust and web tests concurrently, then API and harness
  --skip-rust           Skip Rust unit tests
  --skip-api            Skip API integration tests
  --skip-harness        Skip media harness E2E
  --skip-web            Skip web (Vitest) tests
  -h, --help            Show this help

Arguments after "--" are forwarded to docker-test.sh (e.g. test filters).

Examples:
  ./scripts/run-all-tests.sh
  ./scripts/run-all-tests.sh --skip-harness
  ./scripts/run-all-tests.sh -- --filter "FullyQualifiedName~MetricsIntegrationTests"
EOF
}

PARALLEL_FRONTEND=false
SKIP_RUST=false
SKIP_API=false
SKIP_HARNESS=false
SKIP_WEB=false
API_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --parallel-frontend) PARALLEL_FRONTEND=true; shift ;;
    --skip-rust) SKIP_RUST=true; shift ;;
    --skip-api) SKIP_API=true; shift ;;
    --skip-harness) SKIP_HARNESS=true; shift ;;
    --skip-web) SKIP_WEB=true; shift ;;
    -h|--help) usage; exit 0 ;;
    --) shift; API_ARGS=("$@"); break ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if $PARALLEL_FRONTEND && { ! $SKIP_RUST || ! $SKIP_WEB; }; then
  pids=()
  if ! $SKIP_RUST; then run_rust & pids+=($!); fi
  if ! $SKIP_WEB; then run_web & pids+=($!); fi
  for pid in "${pids[@]}"; do
    wait "$pid"
  done
else
  if ! $SKIP_RUST; then run_rust; fi
fi

if ! $SKIP_API; then
  if ((${#API_ARGS[@]})); then
    run_api "${API_ARGS[@]}"
  else
    run_api
  fi
fi

if ! $SKIP_HARNESS; then run_harness; fi

if ! $PARALLEL_FRONTEND && ! $SKIP_WEB; then run_web; fi

echo "==> All requested test suites passed."
