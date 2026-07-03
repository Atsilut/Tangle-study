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
SDK_IMAGE="${SDK_IMAGE:-tangle-study-sdk:local}"

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

api_tests_dll() {
  echo "${ROOT}/services/Api.Tests/bin/Release/net10.0/Api.Tests.dll"
}

api_tests_need_rebuild() {
  local dll src_dir out_dir fixture
  dll="$(api_tests_dll)"
  src_dir="${ROOT}/services/Api.Tests/Fixtures/Harness"
  out_dir="${ROOT}/services/Api.Tests/bin/Release/net10.0/Fixtures/Harness"

  [[ -f "$dll" ]] || return 0
  [[ -f "$out_dir/sample.jpg" && -f "$out_dir/sample.mp4" ]] || return 0

  for fixture in sample.jpg sample.mp4; do
    [[ "${src_dir}/${fixture}" -nt "${out_dir}/${fixture}" ]] && return 0
  done
  [[ "${ROOT}/services/Api.Tests/Api.Tests.csproj" -nt "$dll" ]] && return 0

  while IFS= read -r -d '' cs_file; do
    [[ "$cs_file" -nt "$dll" ]] && return 0
  done < <(find "${ROOT}/services/Api.Tests" -name '*.cs' -print0)

  return 1
}

build_api_tests() {
  log_step "BUILD API.TESTS"
  nuget_mount="$(ci_nuget_mount)"
  docker compose "${COMPOSE_ARGS[@]}" run --rm --no-deps \
    -v "$nuget_mount" \
    --entrypoint bash \
    harness -c "dotnet build services/Api.Tests/Api.Tests.csproj -c Release --no-incremental"
  ci_fix_cache_ownership
}

resolve_container_ip() {
  local service="$1"
  local container_id ip
  container_id="$(docker compose "${COMPOSE_ARGS[@]}" ps -q "$service" | head -1)"
  [[ -n "$container_id" ]] || fail "$service is not running — harness stack may not have started correctly"
  ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{break}}{{end}}' "$container_id")"
  [[ -n "$ip" ]] || fail "$service has no network address — is the stack healthy?"
  echo "$ip"
}

configure_media_blob_endpoint() {
  local nginx_ip="$1"
  export HARNESS_PUBLIC_BLOB_ENDPOINT="http://${nginx_ip}"
  log_step "CONFIGURE MEDIA BLOB ENDPOINT (${HARNESS_PUBLIC_BLOB_ENDPOINT})"
  docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --no-deps --force-recreate --wait media
}

cleanup() {
  docker compose "${COMPOSE_ARGS[@]}" down -v
}
trap cleanup EXIT

if [[ "$SKIP_PUBLISH" != "1" ]]; then
  log_step "PUBLISH DOTNET SERVICES"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" bash "$ROOT/scripts/ci/dotnet-publish.sh"
fi

if [[ "$SKIP_WORKERS" != "1" ]]; then
  log_step "BUILD RUST WORKERS"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" bash "$ROOT/scripts/ci/build-workers-release.sh"
fi

if [[ "$SKIP_COMPOSE_BUILD" != "1" ]]; then
  log_step "BUILD HARNESS STACK IMAGES"
  COMPOSE_ENV_FILE="${COMPOSE_ENV_FILE:-}" bash "$ROOT/scripts/ci/compose-build-stack.sh"
elif docker image inspect "$SDK_IMAGE" >/dev/null 2>&1; then
  log_info "harness SDK image already available ($SDK_IMAGE)"
else
  log_step "BUILD HARNESS SDK IMAGE"
  docker compose "${COMPOSE_ARGS[@]}" build harness
fi

log_step "START HARNESS STACK"
docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --wait \
  db redis azurite api media rust-worker-media
docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --wait nginx

nginx_ip="$(resolve_container_ip nginx)"
configure_media_blob_endpoint "$nginx_ip"

log_step "RUN HARNESS TESTS"
if api_tests_need_rebuild; then
  build_api_tests
fi
[[ -f "$(api_tests_dll)" ]] \
  || fail "missing services/Api.Tests/bin/Release/net10.0/Api.Tests.dll — run ./scripts/ci/dotnet-publish.sh first"

nuget_mount="$(ci_nuget_mount)"
docker compose "${COMPOSE_ARGS[@]}" run --rm --no-deps \
  -e "TANGLE_HARNESS_API_BASE_URL=http://${nginx_ip}" \
  -v "$nuget_mount" harness

ci_fix_cache_ownership

log_info "media harness completed"
