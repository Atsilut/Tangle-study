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

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

JOB_NAME="${MIGRATE_JOB_NAME:-tangle-study-migrate}"
TIMEOUT="${MIGRATE_TIMEOUT_SEC:-600}"
RG="$AZURE_RESOURCE_GROUP"

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
        --output yaml >&2 || true
      exit 1
      ;;
  esac
  sleep 10
done

echo "==> Migrate job timed out (last status: $status)" >&2
exit 1
