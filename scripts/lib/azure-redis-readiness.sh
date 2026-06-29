#!/usr/bin/env bash
# Shared Redis TCP ingress + cross-app readiness helpers for ACA CD.
#
# Expects caller to set: RG, REDIS_APP, API_APP, REDIS_PORT (default 6379).

REDIS_PORT="${REDIS_PORT:-6379}"
REDIS_STACKEXCHANGE_EXTRAS="${REDIS_STACKEXCHANGE_EXTRAS:-,abortConnect=false,connectTimeout=10000,syncTimeout=10000}"
# Set by ensure_redis_cross_app_tcp when a probe target is verified (internal FQDN or short app name).
REDIS_TCP_HOST="${REDIS_TCP_HOST:-}"

_lib_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/log-redact.sh
source "$_lib_dir/log-redact.sh"

container_app_env_default_domain() {
  local app_name="$1"
  local env_id env_name
  env_id="$(az containerapp show \
    --name "$app_name" \
    --resource-group "$RG" \
    --query properties.managedEnvironmentId \
    --output tsv)"
  env_name="${env_id##*/}"
  az containerapp env show \
    --name "$env_name" \
    --resource-group "$RG" \
    --query properties.defaultDomain \
    --output tsv
}

redis_internal_host() {
  local domain="$1"
  echo "${REDIS_APP}.internal.${domain}"
}

redis_effective_host() {
  local domain="$1"
  if [[ -n "${REDIS_TCP_HOST:-}" ]]; then
    echo "$REDIS_TCP_HOST"
    return 0
  fi
  redis_internal_host "$domain"
}

redis_host_port() {
  local domain="$1"
  echo "$(redis_effective_host "$domain"):${REDIS_PORT}"
}

normalize_redis_api_connection_string() {
  local host_port="$1"
  if [[ "$host_port" == *abortConnect=* ]]; then
    echo "$host_port"
    return 0
  fi
  echo "${host_port}${REDIS_STACKEXCHANGE_EXTRAS}"
}

redis_api_connection_string() {
  normalize_redis_api_connection_string "$(redis_host_port "$1")"
}

redis_url() {
  local domain="$1"
  echo "redis://$(redis_host_port "$domain")"
}

redis_ingress_ok() {
  local fqdn transport target_port
  fqdn="$(az containerapp show \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv 2>/dev/null || true)"
  transport="$(az containerapp show \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.transport" \
    --output tsv 2>/dev/null || true)"
  target_port="$(az containerapp show \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.targetPort" \
    --output tsv 2>/dev/null || true)"
  [[ -n "$fqdn" && "$transport" == "tcp" && "$target_port" == "$REDIS_PORT" ]]
}

enable_redis_tcp_ingress() {
  echo "==> ${REDIS_APP}: enabling internal TCP ingress on port ${REDIS_PORT}"
  az containerapp ingress enable \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --type internal \
    --target-port "$REDIS_PORT" \
    --transport tcp \
    --exposed-port "$REDIS_PORT" \
    --output none
}

update_redis_tcp_ingress() {
  echo "==> ${REDIS_APP}: updating ingress to TCP port ${REDIS_PORT}"
  az containerapp ingress update \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --target-port "$REDIS_PORT" \
    --transport tcp \
    --exposed-port "$REDIS_PORT" \
    --output none
}

recycle_redis_tcp_ingress() {
  echo "==> ${REDIS_APP}: recycling internal TCP ingress on port ${REDIS_PORT}"
  az containerapp ingress disable \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --output none
  sleep 10
  enable_redis_tcp_ingress
}

ensure_redis_tcp_ingress() {
  if ! az containerapp show --name "$REDIS_APP" --resource-group "$RG" &>/dev/null; then
    echo "==> ${REDIS_APP}: not found; skip Redis ingress reconcile" >&2
    return 0
  fi

  if [[ "${INFRA_FORCE_TCP_INGRESS_RECYCLE:-}" == "1" ]]; then
    recycle_redis_tcp_ingress
    return 0
  fi

  if redis_ingress_ok; then
    echo "==> ${REDIS_APP}: TCP ingress on ${REDIS_PORT} already configured"
    return 0
  fi

  local fqdn
  fqdn="$(az containerapp show \
    --name "$REDIS_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv 2>/dev/null || true)"
  if [[ -n "$fqdn" ]]; then
    update_redis_tcp_ingress
  else
    enable_redis_tcp_ingress
  fi
}

container_app_env_value() {
  local app_name="$1"
  local env_name="$2"
  az containerapp show \
    --name "$app_name" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].env[?name=='${env_name}'].value | [0]" \
    --output tsv 2>/dev/null || true
}

container_app_managed_environment_id() {
  local app_name="${1:-$REDIS_APP}"
  az containerapp show \
    --name "$app_name" \
    --resource-group "$RG" \
    --query properties.managedEnvironmentId \
    --output tsv
}

run_container_app_exec() {
  local probe_app="$1"
  local container_name="$2"
  local command="$3"
  local exec_script rc

  exec_script="$(mktemp)"
  cat >"$exec_script" <<EOF
#!/usr/bin/env bash
set -euo pipefail
az containerapp exec \\
  --name $(printf '%q' "$probe_app") \\
  --resource-group $(printf '%q' "$RG") \\
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

exec_probe_failed_with_ioctl() {
  local err_file="$1"
  grep -qiE 'inappropriate ioctl for device|not a tty' "$err_file"
}

# az containerapp exec often exits 0 even when the remote command fails (see "exit code N, code: 0").
exec_probe_reported_failure() {
  local err_file="$1"
  grep -qiE 'ClusterExecFailure|non-zero exit code|command terminated with' "$err_file"
}

restart_container_app_active_revision() {
  local app_name="$1"
  local revision

  if ! az containerapp show --name "$app_name" --resource-group "$RG" &>/dev/null; then
    return 0
  fi

  revision="$(az containerapp revision list \
    --name "$app_name" \
    --resource-group "$RG" \
    --query "[?properties.active==\`true\`].name | [0]" \
    --output tsv 2>/dev/null || true)"
  if [[ -z "$revision" ]]; then
    echo "==> ${app_name}: no active revision to restart" >&2
    return 0
  fi

  echo "==> ${app_name}: restarting active revision ${revision} to refresh internal routing"
  az containerapp revision restart \
    --name "$app_name" \
    --resource-group "$RG" \
    --revision "$revision" \
    --output none
}

probe_redis_tcp_via_exec() {
  local host="$1"
  local port="$2"
  local probe_app="${REDIS_TCP_PROBE_APP:-$API_APP}"
  local command err_file rc

  if ! az containerapp show --name "$probe_app" --resource-group "$RG" &>/dev/null; then
    echo "==> ${probe_app}: not found; skipping exec-based Redis TCP probe" >&2
    return 1
  fi

  # Double-quoted bash -c survives az containerapp exec; single quotes break inside the remote shell.
  command="timeout 5 bash -c \"echo >/dev/tcp/${host}/${port}\""
  echo "==> Probing Redis TCP via exec from ${probe_app} to $(redis_endpoint_log_label "$host" "$port")"
  err_file="$(mktemp)"
  # Do not trust az exit code alone — exec returns 0 when the remote command times out or fails.
  run_container_app_exec "$probe_app" "$probe_app" "$command" 2>"$err_file" || true

  if exec_probe_failed_with_ioctl "$err_file"; then
    echo "==> exec probe unavailable in non-interactive shell; will try job probe" >&2
    rm -f "$err_file"
    return 2
  fi
  if exec_probe_reported_failure "$err_file"; then
    redact_log_stream <"$err_file" >&2
    rm -f "$err_file"
    return 1
  fi
  rm -f "$err_file"
  return 0
}

ensure_redis_probe_job() {
  local host="$1"
  local port="$2"
  local env_id image probe_cmd
  local job_name="${REDIS_PROBE_JOB_NAME:-tangle-study-redis-probe}"

  image="${DEBIAN_IMAGE:-debian:bookworm-slim}"
  env_id="$(container_app_managed_environment_id)"
  probe_cmd='timeout 5 bash -c "echo > /dev/tcp/${REDIS_HOST}/${REDIS_PORT}"'

  if az containerapp job show --name "$job_name" --resource-group "$RG" &>/dev/null; then
    echo "==> ${job_name}: updating probe job image and target"
    az containerapp job update \
      --name "$job_name" \
      --resource-group "$RG" \
      --image "$image" \
      --command "bash" "-c" "$probe_cmd" \
      --set-env-vars "REDIS_HOST=${host}" "REDIS_PORT=${port}" \
      --output none
    return 0
  fi

  echo "==> ${job_name}: creating one-shot Redis TCP probe job"
  az containerapp job create \
    --name "$job_name" \
    --resource-group "$RG" \
    --environment "$env_id" \
    --trigger-type Manual \
    --replica-timeout 120 \
    --replica-retry-limit 0 \
    --image "$image" \
    --cpu 0.25 \
    --memory 0.5Gi \
    --command "bash" "-c" "$probe_cmd" \
    --env-vars "REDIS_HOST=${host}" "REDIS_PORT=${port}" \
    --output none
}

probe_redis_tcp_via_job() {
  local host="$1"
  local port="$2"
  local job_name="${REDIS_PROBE_JOB_NAME:-tangle-study-redis-probe}"
  local execution_name status deadline
  local lib_dir

  lib_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  # shellcheck source=scripts/lib/container-app-job-logs.sh
  source "$lib_dir/container-app-job-logs.sh"

  ensure_redis_probe_job "$host" "$port"

  echo "==> Probing Redis TCP via job ${job_name} to $(redis_endpoint_log_label "$host" "$port")"
  execution_name="$(az containerapp job start \
    --name "$job_name" \
    --resource-group "$RG" \
    --query name \
    --output tsv)"

  deadline=$((SECONDS + 120))
  status="Unknown"
  while (( SECONDS < deadline )); do
    status="$(az containerapp job execution show \
      --name "$job_name" \
      --resource-group "$RG" \
      --job-execution-name "$execution_name" \
      --query properties.status \
      --output tsv 2>/dev/null || echo "Running")"

    case "$status" in
      Succeeded)
        echo "==> Redis TCP probe job succeeded (${execution_name})"
        return 0
        ;;
      Failed|Stopped)
        echo "==> Redis TCP probe job failed (${execution_name}, status: ${status})" >&2
        dump_container_app_job_logs "$job_name" "$RG" "$execution_name" "$job_name" 40 || true
        return 1
        ;;
    esac
    sleep 5
  done

  echo "==> Redis TCP probe job timed out (last status: ${status})" >&2
  dump_container_app_job_logs "$job_name" "$RG" "$execution_name" "$job_name" 40 || true
  return 1
}

probe_redis_tcp() {
  local host="$1"
  local port="${2:-$REDIS_PORT}"
  local rc

  probe_redis_tcp_via_exec "$host" "$port"
  rc=$?
  if (( rc == 0 )); then
    return 0
  fi
  # Job probe is only a fallback when exec cannot run (non-TTY / ioctl). Real TCP failures
  # from the API pod must fail CD — job networking is not a substitute for app connectivity.
  if (( rc == 2 )); then
    echo "==> Falling back to Redis TCP probe job"
    probe_redis_tcp_via_job "$host" "$port"
    return $?
  fi
  return 1
}

wait_for_redis_tcp() {
  local host="$1"
  local max_attempts="${2:-6}"
  local interval="${3:-15}"
  local attempt

  for (( attempt=1; attempt<=max_attempts; attempt++ )); do
    if probe_redis_tcp "$host" "$REDIS_PORT"; then
      echo "==> Redis TCP probe succeeded ($(redis_endpoint_log_label "$host" "$REDIS_PORT"))"
      return 0
    fi
    if (( attempt < max_attempts )); then
      echo "==> Redis TCP probe failed (attempt ${attempt}/${max_attempts}); retry in ${interval}s..."
      sleep "$interval"
    fi
  done

  echo "==> Redis TCP probe failed after ${max_attempts} attempts ($(redis_endpoint_log_label "$host" "$REDIS_PORT"))" >&2
  return 1
}

try_redis_tcp_hosts() {
  local domain="$1"
  local fqdn_host short_host

  fqdn_host="$(redis_internal_host "$domain")"
  if wait_for_redis_tcp "$fqdn_host"; then
    REDIS_TCP_HOST="$fqdn_host"
    return 0
  fi

  short_host="$REDIS_APP"
  if [[ "$short_host" == "$fqdn_host" ]]; then
    return 1
  fi

  echo "==> Internal FQDN probe failed; trying ACA short app name ${short_host}:${REDIS_PORT}" >&2
  if wait_for_redis_tcp "$short_host"; then
    echo "==> Short app name ${short_host} reachable; using it for Redis connection env" >&2
    REDIS_TCP_HOST="$short_host"
    return 0
  fi

  return 1
}

ensure_redis_cross_app_tcp() {
  local domain recycled=false
  domain="$(container_app_env_default_domain "$REDIS_APP")"
  REDIS_TCP_HOST=""

  if try_redis_tcp_hosts "$domain"; then
    return 0
  fi

  if [[ "${INFRA_FORCE_TCP_INGRESS_RECYCLE:-}" != "1" ]] && redis_ingress_ok; then
    echo "==> Ingress metadata looks correct but cross-app TCP failed; recycling Redis TCP ingress"
    recycle_redis_tcp_ingress
    recycled=true
    echo "==> Waiting 30s after Redis TCP ingress recycle..."
    sleep 30
    restart_container_app_active_revision "$REDIS_APP"
    restart_container_app_active_revision "$API_APP"
    echo "==> Waiting for ${API_APP} revision after routing refresh..."
    wait_for_container_app_revision_healthy "$API_APP" "$RG" "${API_READY_TIMEOUT_SEC:-300}"
    sleep 15
    if try_redis_tcp_hosts "$domain"; then
      return 0
    fi
  fi

  if [[ "$recycled" == "true" ]]; then
    echo "==> Redis cross-app TCP still failing after ingress recycle" >&2
  fi
  return 1
}

ensure_api_redis_env() {
  local domain conn current
  if ! az containerapp show --name "$API_APP" --resource-group "$RG" &>/dev/null; then
    return 0
  fi

  domain="$(container_app_env_default_domain "$API_APP")"
  conn="$(redis_api_connection_string "$domain")"
  current="$(container_app_env_value "$API_APP" "Redis__ConnectionString")"

  if [[ "$current" == "$conn" ]]; then
    echo "==> ${API_APP}: Redis__ConnectionString already set"
    return 0
  fi

  echo "==> ${API_APP}: updating Redis__ConnectionString"
  az containerapp update \
    --name "$API_APP" \
    --resource-group "$RG" \
    --set-env-vars \
      "Redis__Enabled=true" \
      "Redis__ConnectionString=${conn}" \
    --output none
}

ensure_worker_redis_url() {
  local worker domain url current
  domain="$(container_app_env_default_domain "$REDIS_APP")"
  url="$(redis_url "$domain")"

  for worker in tangle-study-worker-chat tangle-study-worker-media tangle-study-worker-location; do
    if ! az containerapp show --name "$worker" --resource-group "$RG" &>/dev/null; then
      continue
    fi
    current="$(container_app_env_value "$worker" "REDIS_URL")"
    if [[ "$current" == "$url" ]]; then
      echo "==> ${worker}: REDIS_URL already set"
      continue
    fi
    echo "==> ${worker}: updating REDIS_URL"
    az containerapp update \
      --name "$worker" \
      --resource-group "$RG" \
      --set-env-vars "REDIS_URL=${url}" \
      --output none
  done
}

ensure_redis_exporter_addr() {
  local domain addr current
  if ! az containerapp show --name tangle-study-redis-exporter --resource-group "$RG" &>/dev/null; then
    return 0
  fi

  domain="$(container_app_env_default_domain "$REDIS_APP")"
  addr="$(redis_host_port "$domain")"
  current="$(container_app_env_value tangle-study-redis-exporter REDIS_ADDR)"

  if [[ "$current" == "$addr" ]]; then
    echo "==> tangle-study-redis-exporter: REDIS_ADDR already set"
    return 0
  fi

  echo "==> tangle-study-redis-exporter: updating REDIS_ADDR"
  az containerapp update \
    --name tangle-study-redis-exporter \
    --resource-group "$RG" \
    --set-env-vars "REDIS_ADDR=${addr}" \
    --output none
}

reconcile_redis_connection_env() {
  ensure_api_redis_env
  ensure_worker_redis_url
  ensure_redis_exporter_addr
}
