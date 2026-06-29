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

# Set by try_api_http_hosts_from_web when a probe target is verified (short app name or internal FQDN).
API_HTTP_UPSTREAM="${API_HTTP_UPSTREAM:-}"

api_http_upstream_log_label() {
  local host="$1"
  local port="${2:-8080}"
  local app_name="${3:-tangle-study-api}"

  if [[ -z "$host" || "$host" == "$app_name" ]]; then
    echo "${app_name}:${port}"
    return 0
  fi
  if [[ "$host" == *".internal."* ]]; then
    echo "${app_name} internal FQDN:${port}"
    return 0
  fi
  echo "${host}:${port}"
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

probe_http_health_via_exec() {
  local probe_app="$1"
  local rg="$2"
  local host="$3"
  local port="${4:-8080}"
  local upstream="${host}:${port}"
  local command output_file

  command="wget -qO- --timeout=15 http://${upstream}/health"
  output_file="$(mktemp)"
  run_container_app_exec "$probe_app" "$probe_app" "$command" "$rg" >"$output_file" 2>&1 || true

  if container_app_exec_reported_failure "$output_file"; then
    rm -f "$output_file"
    return 1
  fi
  if grep -q Healthy "$output_file"; then
    rm -f "$output_file"
    return 0
  fi
  rm -f "$output_file"
  return 1
}

wait_for_api_http_from_web() {
  local web_app="$1"
  local rg="$2"
  local host="$3"
  local api_app="$4"
  local port="${5:-8080}"
  local max_attempts="${6:-6}"
  local interval="${7:-15}"
  local attempt label

  label="$(api_http_upstream_log_label "$host" "$port" "$api_app")"

  for (( attempt=1; attempt<=max_attempts; attempt++ )); do
    if probe_http_health_via_exec "$web_app" "$rg" "$host" "$port"; then
      echo "==> API HTTP probe succeeded from ${web_app} to ${label}"
      return 0
    fi
    if (( attempt < max_attempts )); then
      echo "==> API HTTP probe failed from ${web_app} to ${label} (attempt ${attempt}/${max_attempts}); retry in ${interval}s..."
      sleep "$interval"
    fi
  done

  echo "==> API HTTP probe failed from ${web_app} to ${label} after ${max_attempts} attempts" >&2
  return 1
}

try_api_http_hosts_from_web() {
  local web_app="$1"
  local api_app="$2"
  local rg="$3"
  local port="${4:-8080}"
  local fqdn_host short_host

  API_HTTP_UPSTREAM=""
  fqdn_host="$(aca_internal_app_host "$api_app" "$rg")"
  short_host="$api_app"

  echo "==> Probing ACA short app name $(api_http_upstream_log_label "$short_host" "$port" "$api_app") from ${web_app}"
  if wait_for_api_http_from_web "$web_app" "$rg" "$short_host" "$api_app" "$port" 3 10; then
    API_HTTP_UPSTREAM="${short_host}:${port}"
    return 0
  fi

  echo "==> Short app name probe failed; trying $(api_http_upstream_log_label "$fqdn_host" "$port" "$api_app") from ${web_app}" >&2
  if wait_for_api_http_from_web "$web_app" "$rg" "$fqdn_host" "$api_app" "$port"; then
    API_HTTP_UPSTREAM="${fqdn_host}:${port}"
    return 0
  fi

  return 1
}

container_app_env_var() {
  local app_name="$1"
  local rg="$2"
  local env_name="$3"
  az containerapp show \
    --name "$app_name" \
    --resource-group "$rg" \
    --query "properties.template.containers[0].env[?name=='${env_name}'].value | [0]" \
    --output tsv 2>/dev/null || true
}

probe_web_api_health_via_exec() {
  local web_app="$1"
  local rg="$2"
  local api_app="${3:-tangle-study-api}"
  local upstream port host output_file command
  local short_label fqdn_label

  if ! az containerapp show --name "$web_app" --resource-group "$rg" &>/dev/null; then
    echo "==> ${web_app}: not found; skip web→API exec health probe" >&2
    return 1
  fi

  port=8080
  host="${api_app}"
  short_label="$(api_http_upstream_log_label "$host" "$port" "$api_app")"
  fqdn_label="$(api_http_upstream_log_label "$(aca_internal_app_host "$api_app" "$rg")" "$port" "$api_app")"

  upstream="$(container_app_env_var "$web_app" "$rg" "TANGLE_API_UPSTREAM")"
  if [[ -z "$upstream" ]]; then
    if ! try_api_http_hosts_from_web "$web_app" "$api_app" "$rg" "$port"; then
      echo "==> ${web_app} cannot reach ${api_app} /health via short name (${short_label}) or internal FQDN (${fqdn_label})" >&2
      return 1
    fi
    upstream="$API_HTTP_UPSTREAM"
  fi

  command="wget -qO- --timeout=15 http://${upstream}/health"
  echo "==> Probing ${web_app} → ${upstream}/health via container exec"
  output_file="$(mktemp)"
  run_container_app_exec "$web_app" "$web_app" "$command" "$rg" >"$output_file" 2>&1 || true

  if container_app_exec_reported_failure "$output_file"; then
    echo "==> Failed host: ${upstream} (also try ${short_label} vs ${fqdn_label})" >&2
    if declare -f redact_log_stream >/dev/null 2>&1; then
      redact_log_stream <"$output_file" >&2
    else
      cat "$output_file" >&2
    fi
    echo "==> Hint: ACA cross-app HTTP routing is per caller — run azure-cd-ensure-api-web-runtime.sh to probe and set TANGLE_API_UPSTREAM." >&2
    echo "==> Hint: nginx proxy_set_header Host must match the upstream hostname (see infra/nginx/docker-entrypoint.sh)." >&2
    rm -f "$output_file"
    return 1
  fi
  if grep -q Healthy "$output_file"; then
    rm -f "$output_file"
    echo "==> ${web_app} reached ${upstream}/health (Healthy) via exec"
    return 0
  fi

  echo "==> ${web_app} did not get Healthy from ${upstream}/health via exec" >&2
  echo "==> Hint: verify runtime nginx upstream and Host header (grep server / proxy_set_header Host in default.conf)." >&2
  rm -f "$output_file"
  return 1
}
