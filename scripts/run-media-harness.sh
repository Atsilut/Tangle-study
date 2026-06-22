#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/lib/compose-env.sh
source "$ROOT/scripts/lib/compose-env.sh"

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

docker compose "${COMPOSE_ARGS[@]}" build api rust-worker-media harness
docker compose "${COMPOSE_ARGS[@]}" up -d --wait
docker compose "${COMPOSE_ARGS[@]}" run --rm harness
