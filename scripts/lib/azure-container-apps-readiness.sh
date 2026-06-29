#!/usr/bin/env bash
# Shared Container Apps revision readiness helpers for CD smoke and post-deploy waits.

container_app_env_default_domain() {
  local app_name="$1"
  local rg="$2"
  local env_id env_name
  env_id="$(az containerapp show \
    --name "$app_name" \
    --resource-group "$rg" \
    --query properties.managedEnvironmentId \
    --output tsv)"
  env_name="${env_id##*/}"
  az containerapp env show \
    --name "$env_name" \
    --resource-group "$rg" \
    --query properties.defaultDomain \
    --output tsv
}

aca_internal_app_host() {
  local app_name="$1"
  local rg="$2"
  echo "${app_name}.internal.$(container_app_env_default_domain "$app_name" "$rg")"
}

aca_internal_app_upstream() {
  local app_name="$1"
  local rg="$2"
  local port="${3:-8080}"
  echo "$(aca_internal_app_host "$app_name" "$rg"):${port}"
}

revision_is_running() {
  case "${1:-}" in
    Running|RunningAtMaxScale) return 0 ;;
    *) return 1 ;;
  esac
}

revision_is_operational() {
  local health="${1:-}"
  local running="${2:-}"
  [[ "$health" == "Healthy" ]] && revision_is_running "$running"
}

container_app_active_revision_record() {
  local app_name="$1"
  local rg="$2"
  az containerapp revision list \
    --name "$app_name" \
    --resource-group "$rg" \
    --query "[?properties.active==\`true\`] | [0]" \
    --output json 2>/dev/null || echo "null"
}

container_app_active_revision_fields() {
  local app_name="$1"
  local rg="$2"
  container_app_active_revision_record "$app_name" "$rg" | python3 -c '
import json
import sys

raw = sys.stdin.read().strip()
if not raw or raw == "null":
    print("{}")
    raise SystemExit(0)

row = json.loads(raw)
props = row.get("properties") or {}
print(
    json.dumps(
        {
            "revision": row.get("name", ""),
            "healthState": props.get("healthState", ""),
            "runningState": props.get("runningState", ""),
            "provisioningState": props.get("provisioningState", ""),
            "replicas": props.get("replicas"),
        }
    )
)
'
}

container_app_active_revision_status_line() {
  local app_name="$1"
  local rg="$2"
  container_app_active_revision_record "$app_name" "$rg" | python3 -c '
import json
import sys

raw = sys.stdin.read().strip()
if not raw or raw == "null":
    print("\t".join(["", "Unknown", "Unknown", "Unknown", "0"]))
    raise SystemExit(0)

row = json.loads(raw)
props = row.get("properties") or {}
fields = [
    row.get("name", ""),
    props.get("healthState", ""),
    props.get("runningState", ""),
    props.get("provisioningState", ""),
    str(props.get("replicas", "")),
]
print("\t".join(fields))
'
}

container_app_latest_revision_status() {
  local app_name="$1"
  local rg="$2"
  container_app_active_revision_fields "$app_name" "$rg"
}

dump_container_app_revision_status() {
  local app_name="$1"
  local rg="$2"
  local image target_port fields

  if ! az containerapp show --name "$app_name" --resource-group "$rg" &>/dev/null; then
    echo "==> ${app_name}: not found" >&2
    return 0
  fi

  echo "==> ${app_name} revision status:" >&2
  fields="$(container_app_active_revision_fields "$app_name" "$rg")"
  if [[ "$fields" == "{}" ]]; then
    echo "    (no active revision)" >&2
  else
    echo "$fields" | python3 -c '
import json
import sys

data = json.load(sys.stdin)
for key in ("revision", "healthState", "runningState", "provisioningState", "replicas"):
    print(f"{key}: {data.get(key, "")}")
' >&2
  fi

  image="$(az containerapp show \
    --name "$app_name" \
    --resource-group "$rg" \
    --query "properties.template.containers[0].image" \
    --output tsv 2>/dev/null || true)"
  target_port="$(az containerapp show \
    --name "$app_name" \
    --resource-group "$rg" \
    --query "properties.configuration.ingress.targetPort" \
    --output tsv 2>/dev/null || true)"
  echo "==> ${app_name} image=${image:-unknown} ingress.targetPort=${target_port:-n/a}" >&2
}

wait_for_container_app_revision_healthy() {
  local app_name="$1"
  local rg="$2"
  local timeout_sec="${3:-300}"
  local deadline revision health running provisioning replicas status_line

  if ! az containerapp show --name "$app_name" --resource-group "$rg" &>/dev/null; then
    echo "==> ${app_name}: not found; skip revision wait" >&2
    return 0
  fi

  deadline=$((SECONDS + timeout_sec))
  revision=""
  health="Unknown"
  running="Unknown"

  while (( SECONDS < deadline )); do
    status_line="$(container_app_active_revision_status_line "$app_name" "$rg")"
    IFS=$'\t' read -r revision health running provisioning replicas <<<"$status_line"

    if revision_is_operational "$health" "$running"; then
      echo "==> ${app_name}: revision ${revision} is Healthy (${running}, replicas=${replicas:-?})"
      return 0
    fi

    echo "==> ${app_name}: waiting for Healthy revision (current: ${revision:-none} health=${health:-?} running=${running:-?} provisioning=${provisioning:-?})..."
    sleep 15
  done

  echo "==> ${app_name}: timed out after ${timeout_sec}s waiting for Healthy revision (last: health=${health:-?} running=${running:-?})" >&2
  dump_container_app_revision_status "$app_name" "$rg"

  if revision_is_operational "$health" "$running"; then
    echo "==> ${app_name}: revision ${revision} is Healthy after final check; continuing." >&2
    return 0
  fi

  return 1
}

container_app_exec_reported_failure() {
  local output_file="$1"
  grep -qiE 'ClusterExecFailure|non-zero exit code|command terminated with' "$output_file"
}

run_container_app_exec() {
  local app_name="$1"
  local container_name="$2"
  local command="$3"
  local rg="$4"
  local exec_script rc

  exec_script="$(mktemp)"
  cat >"$exec_script" <<EOF
#!/usr/bin/env bash
set -euo pipefail
az containerapp exec \\
  --name $(printf '%q' "$app_name") \\
  --resource-group $(printf '%q' "$rg") \\
  --container $(printf '%q' "$container_name") \\
  --command $(printf '%q' "$command") \\
  --output none
EOF
  chmod +x "$exec_script"

  if [ -t 0 ] && [ -t 1 ]; then
    "$exec_script"
    rc=$?
    rm -f "$exec_script"
    return "$rc"
  fi

  if command -v script >/dev/null 2>&1; then
    script -q -c "$exec_script" /dev/null
    rc=$?
    rm -f "$exec_script"
    return "$rc"
  fi

  "$exec_script"
  rc=$?
  rm -f "$exec_script"
  return "$rc"
}

probe_api_health_via_exec() {
  local app_name="$1"
  local rg="$2"
  local port="${3:-8080}"
  local output_file command

  if ! az containerapp show --name "$app_name" --resource-group "$rg" &>/dev/null; then
    echo "==> ${app_name}: not found; skip exec health probe" >&2
    return 1
  fi

  command="wget -qO- --timeout=15 http://127.0.0.1:${port}/health"
  echo "==> Probing ${app_name} /health via container exec (127.0.0.1:${port})"
  output_file="$(mktemp)"
  run_container_app_exec "$app_name" "$app_name" "$command" "$rg" >"$output_file" 2>&1 || true

  if container_app_exec_reported_failure "$output_file"; then
    if declare -f redact_log_stream >/dev/null 2>&1; then
      redact_log_stream <"$output_file" >&2
    else
      cat "$output_file" >&2
    fi
    rm -f "$output_file"
    return 1
  fi
  if grep -q Healthy "$output_file"; then
    rm -f "$output_file"
    echo "==> ${app_name} /health returned Healthy via exec"
    return 0
  fi

  echo "==> ${app_name} /health did not return Healthy via exec" >&2
  rm -f "$output_file"
  return 1
}
