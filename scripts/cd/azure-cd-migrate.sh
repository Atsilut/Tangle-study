#!/usr/bin/env bash
# EF Core Container Apps Job runner: start migration job, poll status, dump logs on failure.
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

JOB_NAME="${MIGRATE_JOB_NAME:-tangle-study-migrate}"
TIMEOUT="${MIGRATE_TIMEOUT_SEC:-600}"
RG="$AZURE_RESOURCE_GROUP"

dump_failure() {
  local execution_name="$1"

  log_error "dumping-job-logs execution=$execution_name"

  dump_container_app_job_logs \
    "$JOB_NAME" \
    "$RG" \
    "$execution_name" \
    "$JOB_NAME" \
    80 || true
}

############################################
log_step "START MIGRATION JOB"

log_info "job=$JOB_NAME rg=$RG timeout=${TIMEOUT}s"

EXECUTION_NAME="$(az containerapp job start \
  --name "$JOB_NAME" \
  --resource-group "$RG" \
  --query name \
  --output tsv)"

log_info "execution_started name=$EXECUTION_NAME"

############################################
log_step "POLL JOB STATUS"

deadline=$((SECONDS + TIMEOUT))
status="Running"

while (( SECONDS < deadline )); do
  status="$(az containerapp job execution show \
    --name "$JOB_NAME" \
    --resource-group "$RG" \
    --job-execution-name "$EXECUTION_NAME" \
    --query properties.status \
    --output tsv 2>/dev/null || echo "Running")"

  log_info "execution_status=$status"

  case "$status" in
    Succeeded)
      log_step "MIGRATION SUCCESS"
      log_info "execution=$EXECUTION_NAME status=Succeeded"
      exit 0
      ;;

    Failed|Stopped)
      log_step "MIGRATION FAILED"
      log_error "execution=$EXECUTION_NAME status=$status"

      az containerapp job execution show \
        --name "$JOB_NAME" \
        --resource-group "$RG" \
        --job-execution-name "$EXECUTION_NAME" \
        --query "{name:name,status:properties.status,startTime:properties.startTime,endTime:properties.endTime}" \
        --output json 2>/dev/null | redact_log_stream >&2 || true

      dump_failure "$EXECUTION_NAME"
      exit 1
      ;;
  esac

  sleep 10
done

############################################
log_step "MIGRATION TIMEOUT"

log_error "execution=$EXECUTION_NAME status=TIMEOUT timeout=${TIMEOUT}s"

dump_failure "$EXECUTION_NAME"

exit 1
