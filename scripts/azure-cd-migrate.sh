#!/usr/bin/env bash
# Start the EF migrate Container Apps Job and wait for success.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   MIGRATE_JOB_NAME (default: tangle-study-migrate)
#   MIGRATE_TIMEOUT_SEC (default: 600)
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/container-app-job-logs.sh
source "$ROOT/scripts/lib/container-app-job-logs.sh"

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

JOB_NAME="${MIGRATE_JOB_NAME:-tangle-study-migrate}"
TIMEOUT="${MIGRATE_TIMEOUT_SEC:-600}"
RG="$AZURE_RESOURCE_GROUP"

dump_migrate_failure_logs() {
  local execution_name="$1"
  dump_container_app_job_logs "$JOB_NAME" "$RG" "$execution_name" "$JOB_NAME" 80 || true
}

echo "==> Starting migrate job: $JOB_NAME"
EXECUTION_NAME="$(az containerapp job start \
  --name "$JOB_NAME" \
  --resource-group "$RG" \
  --query name \
  --output tsv)"

echo "==> Execution: $EXECUTION_NAME (timeout ${TIMEOUT}s)"
deadline=$((SECONDS + TIMEOUT))
status="Unknown"

while (( SECONDS < deadline )); do
  status="$(az containerapp job execution show \
    --name "$JOB_NAME" \
    --resource-group "$RG" \
    --job-execution-name "$EXECUTION_NAME" \
    --query properties.status \
    --output tsv 2>/dev/null || echo "Running")"

  case "$status" in
    Succeeded)
      echo "==> Migrate job succeeded."
      exit 0
      ;;
    Failed|Stopped)
      echo "==> Migrate job failed with status: $status" >&2
      az containerapp job execution show \
        --name "$JOB_NAME" \
        --resource-group "$RG" \
        --job-execution-name "$EXECUTION_NAME" \
        --query "{name:name,status:properties.status,startTime:properties.startTime,endTime:properties.endTime}" \
        --output json 2>/dev/null | redact_log_stream >&2 || true
      dump_migrate_failure_logs "$EXECUTION_NAME"
      exit 1
      ;;
  esac
  sleep 10
done

echo "==> Migrate job timed out (last status: $status)" >&2
dump_migrate_failure_logs "$EXECUTION_NAME"
exit 1
