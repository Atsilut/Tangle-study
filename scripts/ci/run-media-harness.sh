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

COMPOSE_ARGS=(-f docker-compose.yml -f docker-compose.harness.yml --profile harness)
if [[ -n "${COMPOSE_ENV_FILE:-}" ]]; then
  env_path="$COMPOSE_ENV_FILE"
  [[ "$env_path" != /* ]] && env_path="$ROOT/$env_path"
  COMPOSE_ARGS=(--env-file "$env_path" "${COMPOSE_ARGS[@]}")
fi

cleanup() {
  docker compose "${COMPOSE_ARGS[@]}" down -v
}
trap cleanup EXIT

log_step "BUILD HARNESS SERVICES"
docker compose "${COMPOSE_ARGS[@]}" build --parallel api media nginx rust-worker-media harness

log_step "START HARNESS STACK"
docker compose "${COMPOSE_ARGS[@]}" up -d --wait

log_step "RUN HARNESS TESTS"
nuget_mount="$(ci_nuget_mount)"
docker compose "${COMPOSE_ARGS[@]}" run --rm -v "$nuget_mount" harness

ci_fix_cache_ownership

log_info "media harness completed"
