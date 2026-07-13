#!/usr/bin/env bash
# EF Core Container Apps Job runner: start each migrate job from parameters.prod.json,
# poll status, dump logs on failure.
#
# Usage: AZURE_RESOURCE_GROUP=tangle-study-prod bash scripts/cd/azure-cd-migrate.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOG_PREFIX="[DEPLOY][MIGRATE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/cd/libs/container-app-job-logs.sh
source "$ROOT/scripts/cd/libs/container-app-job-logs.sh"

require_env AZURE_RESOURCE_GROUP

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
TIMEOUT="${MIGRATE_TIMEOUT_SEC:-600}"
RG="$AZURE_RESOURCE_GROUP"

[[ -f "$PARAM_FILE" ]] || fail "missing parameter file: $PARAM_FILE"

mapfile -t JOB_NAMES < <(jq -r '.parameters.migrateJobs.value[].name' "$PARAM_FILE")
[[ ${#JOB_NAMES[@]} -gt 0 ]] || fail "no migrateJobs found in $PARAM_FILE"

migrate_service_label() {
  local job_name="$1"
  local label="${job_name#tangle-study-migrate-}"
  echo "$label"
}

dump_failure() {
  local job_name="$1"
  local execution_name="$2"

  log_error "dumping-job-logs job=$job_name execution=$execution_name"

  dump_container_app_job_logs \
    "$job_name" \
    "$RG" \
    "$execution_name" \
    "$job_name" \
    80 || true
}

run_one_job() {
  local job_name="$1"
  local service
  service="$(migrate_service_label "$job_name")"

  log_group_start "migrate ${service} (${job_name})"
  log_info "starting rg=$RG timeout=${TIMEOUT}s"

  local execution_name
  execution_name="$(az containerapp job start \
    --name "$job_name" \
    --resource-group "$RG" \
    --query name \
    --output tsv)"

  log_info "execution_started name=$execution_name"

  local deadline=$((SECONDS + TIMEOUT))
  local status="Running"
  local last_status=""

  while (( SECONDS < deadline )); do
    status="$(az containerapp job execution show \
      --name "$job_name" \
      --resource-group "$RG" \
      --job-execution-name "$execution_name" \
      --query properties.status \
      --output tsv 2>/dev/null || echo "Running")"

    if [[ "$status" != "$last_status" ]]; then
      log_info "status=$status"
      last_status="$status"
    fi

    case "$status" in
      Succeeded)
        log_info "succeeded execution=$execution_name"
        log_group_end
        return 0
        ;;

      Failed|Stopped)
        log_error "failed execution=$execution_name status=$status"

        az containerapp job execution show \
          --name "$job_name" \
          --resource-group "$RG" \
          --job-execution-name "$execution_name" \
          --query "{name:name,status:properties.status,startTime:properties.startTime,endTime:properties.endTime}" \
          --output json 2>/dev/null | redact_log_stream >&2 || true

        dump_failure "$job_name" "$execution_name"
        log_group_end
        return 1
        ;;
    esac

    sleep 10
  done

  log_error "timeout execution=$execution_name timeout=${TIMEOUT}s"
  dump_failure "$job_name" "$execution_name"
  log_group_end
  return 1
}

############################################
log_step "MIGRATE JOBS"
log_info "count=${#JOB_NAMES[@]} jobs=${JOB_NAMES[*]}"

failed=0
for job_name in "${JOB_NAMES[@]}"; do
  if ! run_one_job "$job_name"; then
    failed=1
  fi
done

[[ $failed -eq 0 ]] || fail "one or more migrate jobs failed"

############################################
log_step "MIGRATE COMPLETE"
log_info "all-jobs-succeeded=true"
