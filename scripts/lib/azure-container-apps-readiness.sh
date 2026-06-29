#!/usr/bin/env bash
# Shared Container Apps revision readiness helpers for CD smoke and post-deploy waits.

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
