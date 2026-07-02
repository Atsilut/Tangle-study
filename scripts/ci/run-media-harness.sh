#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][HARNESS]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"
# shellcheck source=scripts/ci/libs/ci-cache.sh
source "$ROOT/scripts/ci/libs/ci-cache.sh"

COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-docker/versions.prod.env}"

COMPOSE_ARGS=(
  -f docker-compose.yml
  -f docker-compose.harness.yml
  -f docker-compose.runtime.yml
  --profile harness
)
if [[ -n "${COMPOSE_ENV_FILE:-}" ]]; then
  env_path="$COMPOSE_ENV_FILE"
  [[ "$env_path" != /* ]] && env_path="$ROOT/$env_path"
  COMPOSE_ARGS=(--env-file "$env_path" "${COMPOSE_ARGS[@]}")
fi

SKIP_PUBLISH="${SKIP_PUBLISH:-0}"
SKIP_WORKERS="${SKIP_WORKERS:-0}"
SKIP_COMPOSE_BUILD="${SKIP_COMPOSE_BUILD:-0}"

cleanup() {
  docker compose "${COMPOSE_ARGS[@]}" down -v
}
trap cleanup EXIT

if [[ "$SKIP_PUBLISH" != "1" ]]; then
  log_step "PUBLISH DOTNET SERVICES"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" "$ROOT/scripts/ci/dotnet-publish.sh"
fi

if [[ "$SKIP_WORKERS" != "1" ]]; then
  log_step "BUILD RUST WORKERS"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" "$ROOT/scripts/ci/build-workers-release.sh"
fi

if [[ "$SKIP_COMPOSE_BUILD" != "1" ]]; then
  log_step "BUILD HARNESS STACK IMAGES"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" "$ROOT/scripts/ci/compose-build-stack.sh"
else
  log_step "BUILD HARNESS SDK IMAGE"
  docker compose "${COMPOSE_ARGS[@]}" build harness
fi

log_step "START HARNESS STACK"
docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --wait

log_step "RUN HARNESS TESTS"
nuget_mount="$(ci_nuget_mount)"
docker compose "${COMPOSE_ARGS[@]}" run --rm -v "$nuget_mount" harness

ci_fix_cache_ownership

log_info "media harness completed"
