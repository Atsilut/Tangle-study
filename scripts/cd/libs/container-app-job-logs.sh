#!/usr/bin/env bash
# Log Analytics + Container Apps Job log helpers for CD scripts.

_libs_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/cd/libs/log-redact.sh
source "$_libs_dir/log-redact.sh"

resolve_log_analytics_workspace_id() {
  local rg="$1"
  # Prefer gateway (MSA); fall back to web if gateway lookup fails.
  local app_name="${2:-tangle-study-gateway}"
  local env_id env_name

  env_id="$(az containerapp show \
    --name "$app_name" \
    --resource-group "$rg" \
    --query properties.managedEnvironmentId \
    --output tsv 2>/dev/null || true)"
  if [[ -z "$env_id" && "$app_name" != "tangle-study-web" ]]; then
    env_id="$(az containerapp show \
      --name tangle-study-web \
      --resource-group "$rg" \
      --query properties.managedEnvironmentId \
      --output tsv)"
  fi
  env_name="${env_id##*/}"
  az containerapp env show \
    --name "$env_name" \
    --resource-group "$rg" \
    --query "properties.appLogsConfiguration.logAnalyticsConfiguration.customerId" \
    --output tsv
}

dump_container_app_job_logs() {
  local job_name="$1"
  local rg="$2"
  local execution_name="$3"
  local container_name="${4:-$job_name}"
  local tail="${5:-40}"

  log_info "recent job logs ($job_name / $execution_name)"

  if az containerapp job logs show \
    --name "$job_name" \
    --resource-group "$rg" \
    --execution "$execution_name" \
    --container "$container_name" \
    --format text \
    --tail "$tail" \
    --only-show-errors 2>/dev/null | redact_log_stream; then
    return 0
  fi

  local workspace_id
  workspace_id="$(resolve_log_analytics_workspace_id "$rg" 2>/dev/null || true)"
  if [[ -z "$workspace_id" ]]; then
    log_error "job logs unavailable (containerapp logs and Log Analytics lookup failed)"
    return 1
  fi

  az monitor log-analytics query \
    --workspace "$workspace_id" \
    --only-show-errors \
    --analytics-query "ContainerAppConsoleLogs_CL | where ContainerGroupName_s startswith '${execution_name}' | project TimeGenerated, Log_s | order by TimeGenerated asc | take ${tail}" \
    --output table 2>/dev/null | redact_log_stream >&2 || true
}
