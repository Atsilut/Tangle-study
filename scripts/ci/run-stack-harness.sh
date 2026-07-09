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
HARNESS_MODULES="${HARNESS_MODULES:-all}"

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
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-tangle-study}"
STACK_ARTIFACT="${STACK_ARTIFACT:-harness-stack.tar}"

BASE_SERVICES=(db redis gateway users nginx)

module_needs() {
  local module="${1,,}"
  case "$module" in
    all)
      echo "db redis azurite gateway users social group community media chat location nginx rust-worker-media rust-worker-chat rust-worker-location"
      ;;
    users)
      echo "${BASE_SERVICES[*]}"
      ;;
    social)
      echo "${BASE_SERVICES[*]} social"
      ;;
    group)
      echo "${BASE_SERVICES[*]} social group"
      ;;
    community)
      echo "${BASE_SERVICES[*]} social community"
      ;;
    media)
      echo "${BASE_SERVICES[*]} azurite media rust-worker-media"
      ;;
    chat)
      echo "${BASE_SERVICES[*]} social group chat rust-worker-chat"
      ;;
    location)
      echo "${BASE_SERVICES[*]} group location rust-worker-location"
      ;;
    *)
      fail "unknown harness module: $module (expected users|social|group|community|media|chat|location|all)"
      ;;
  esac
}

collect_services_for_modules() {
  local modules_raw="${1,,}"
  local -A seen=()
  local services=()
  local module part

  if [[ "$modules_raw" == "all" ]]; then
    read -r -a services <<< "$(module_needs all)"
    printf '%s\n' "${services[@]}"
    return 0
  fi

  IFS=',' read -r -a module_parts <<< "$modules_raw"
  for part in "${module_parts[@]}"; do
    module="$(echo "$part" | xargs)"
    [[ -n "$module" ]] || continue
    local needed
    read -r -a needed <<< "$(module_needs "$module")"
    local svc
    for svc in "${needed[@]}"; do
      if [[ -z "${seen[$svc]:-}" ]]; then
        seen[$svc]=1
        services+=("$svc")
      fi
    done
  done

  if [[ ${#services[@]} -eq 0 ]]; then
    fail "no harness modules resolved from HARNESS_MODULES=${HARNESS_MODULES}"
  fi

  printf '%s\n' "${services[@]}"
}

build_harness_filter() {
  local modules_raw="${1,,}"
  if [[ "$modules_raw" == "all" ]]; then
    echo "Category=Harness"
    return 0
  fi

  local filter="Category=Harness&("
  local first=1
  local module part trait
  IFS=',' read -r -a module_parts <<< "$modules_raw"
  for part in "${module_parts[@]}"; do
    module="$(echo "$part" | xargs)"
    [[ -n "$module" ]] || continue
    case "$module" in
      users) trait="Users" ;;
      social) trait="Social" ;;
      group) trait="Group" ;;
      community) trait="Community" ;;
      media) trait="Media" ;;
      chat) trait="Chat" ;;
      location) trait="Location" ;;
      *) fail "unknown harness module for filter: $module" ;;
    esac
    if [[ "$first" -eq 1 ]]; then
      filter+="HarnessModule=${trait}"
      first=0
    else
      filter+="|HarnessModule=${trait}"
    fi
  done
  filter+=")"
  echo "$filter"
}

ensure_stack_images_loaded() {
  local gateway_image="${COMPOSE_PROJECT_NAME}-gateway"
  if docker image inspect "$gateway_image" >/dev/null 2>&1; then
    return 0
  fi

  local tar_path="$STACK_ARTIFACT"
  [[ "$tar_path" == /* ]] || tar_path="${ROOT}/${tar_path}"
  [[ -f "$tar_path" ]] || fail "missing stack images ($gateway_image) and no ${STACK_ARTIFACT} — run ./scripts/ci/compose-build-stack.sh first"

  bash "$ROOT/scripts/ci/load-compose-stack.sh" "$tar_path"
}

stack_tests_dll() {
  echo "${ROOT}/services/Stack.Tests/bin/Release/net10.0/Stack.Tests.dll"
}

stack_tests_need_rebuild() {
  local dll src_dir out_dir fixture
  dll="$(stack_tests_dll)"
  src_dir="${ROOT}/services/Stack.Tests/Fixtures/Harness"
  out_dir="${ROOT}/services/Stack.Tests/bin/Release/net10.0/Fixtures/Harness"

  [[ -f "$dll" ]] || return 0
  [[ -f "$out_dir/sample.jpg" && -f "$out_dir/sample.mp4" ]] || return 0

  for fixture in sample.jpg sample.mp4; do
    [[ "${src_dir}/${fixture}" -nt "${out_dir}/${fixture}" ]] && return 0
  done
  [[ "${ROOT}/services/Stack.Tests/Stack.Tests.csproj" -nt "$dll" ]] && return 0

  while IFS= read -r -d '' cs_file; do
    [[ "$cs_file" -nt "$dll" ]] && return 0
  done < <(find "${ROOT}/services/Stack.Tests" "${ROOT}/services/TestSupport" "${ROOT}/services/TestSupport.Scenarios" -name '*.cs' -print0)

  return 1
}

build_stack_tests() {
  log_step "BUILD STACK.TESTS"
  nuget_mount="$(ci_nuget_mount)"
  docker compose "${COMPOSE_ARGS[@]}" run --rm --no-deps \
    -v "$nuget_mount" \
    --entrypoint bash \
    harness -c "dotnet build services/Stack.Tests/Stack.Tests.csproj -c Release"
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

start_harness_stack() {
  local -a services=()
  local svc
  while IFS= read -r svc; do
    [[ -n "$svc" ]] && services+=("$svc")
  done < <(collect_services_for_modules "$HARNESS_MODULES")

  local -a core_services=()
  local -a worker_services=()
  for svc in "${services[@]}"; do
  case "$svc" in
      rust-worker-*)
        worker_services+=("$svc")
        ;;
      nginx)
        ;;
      *)
        core_services+=("$svc")
        ;;
    esac
  done

  log_step "START HARNESS STACK (modules=${HARNESS_MODULES})"
  if [[ ${#core_services[@]} -gt 0 ]]; then
    docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --wait "${core_services[@]}"
  fi
  if [[ ${#worker_services[@]} -gt 0 ]]; then
    docker compose "${COMPOSE_ARGS[@]}" up -d --no-build "${worker_services[@]}"
    wait_for_worker_metrics "${worker_services[@]}"
  fi
  if printf '%s\n' "${services[@]}" | grep -qx nginx; then
    docker compose "${COMPOSE_ARGS[@]}" up -d --no-build --wait nginx
  fi
}

# Workers expose Prometheus on :9090 but have no in-image healthcheck tooling.
# Probe via gateway (wget) so harness tests do not race worker startup.
wait_for_worker_metrics() {
  local workers=("$@")
  local worker attempt
  local max_attempts=36

  [[ ${#workers[@]} -gt 0 ]] || return 0
  log_step "WAIT FOR WORKER METRICS"

  for worker in "${workers[@]}"; do
    for ((attempt = 1; attempt <= max_attempts; attempt++)); do
      if docker compose "${COMPOSE_ARGS[@]}" exec -T gateway \
        wget -q -O /dev/null "http://${worker}:9090/metrics" 2>/dev/null; then
        log_info "${worker} metrics ready"
        break
      fi
      if [[ "$attempt" -eq "$max_attempts" ]]; then
        fail "${worker} metrics not ready after ${max_attempts} attempts"
      fi
      sleep 2
    done
  done
}

cleanup() {
  docker compose "${COMPOSE_ARGS[@]}" down -v
}
trap cleanup EXIT

HARNESS_FILTER="$(build_harness_filter "$HARNESS_MODULES")"
log_info "harness modules=${HARNESS_MODULES} filter=${HARNESS_FILTER}"

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
else
  ensure_stack_images_loaded
  if docker image inspect "$SDK_IMAGE" >/dev/null 2>&1; then
    log_info "harness SDK image already available ($SDK_IMAGE)"
  else
    log_step "BUILD HARNESS SDK IMAGE"
    docker compose "${COMPOSE_ARGS[@]}" build harness
  fi
fi

start_harness_stack

nginx_ip=""
if docker compose "${COMPOSE_ARGS[@]}" ps -q nginx >/dev/null 2>&1; then
  nginx_ip="$(resolve_container_ip nginx)"
  export TANGLE_HARNESS_API_BASE_URL="http://${nginx_ip}"
  if printf '%s\n' "$(collect_services_for_modules "$HARNESS_MODULES")" | grep -qx media; then
    configure_media_blob_endpoint "$nginx_ip"
  fi
else
  fail "nginx is required for harness tests but is not part of the selected module stack"
fi

log_step "RUN HARNESS TESTS"
if stack_tests_need_rebuild; then
  build_stack_tests
fi
[[ -f "$(stack_tests_dll)" ]] \
  || fail "missing services/Stack.Tests/bin/Release/net10.0/Stack.Tests.dll — build Stack.Tests first"

nuget_mount="$(ci_nuget_mount)"
docker compose "${COMPOSE_ARGS[@]}" run --rm --no-deps \
  -e "TANGLE_HARNESS_API_BASE_URL=${TANGLE_HARNESS_API_BASE_URL}" \
  -e "TANGLE_HARNESS_REDIS_CONNECTION=redis:6379" \
  -e "HARNESS_FILTER=${HARNESS_FILTER}" \
  -v "$nuget_mount" \
  --entrypoint bash \
  harness -c "dotnet test services/Stack.Tests/Stack.Tests.csproj -c Release --no-build --filter \"\${HARNESS_FILTER}\" --verbosity minimal"

ci_fix_cache_ownership

log_info "stack harness completed (modules=${HARNESS_MODULES})"
