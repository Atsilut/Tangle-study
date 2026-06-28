#!/usr/bin/env bash
# Ensure Redis internal TCP ingress and cross-app connection env on ACA.
#
# Existing stacks deployed before infra-container.bicep gained TCP ingress often
# have no ingress on tangle-study-redis; API then fails StackExchange.Redis.Connect
# to the internal FQDN.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   REDIS_APP_NAME (default: tangle-study-redis)
#   API_APP_NAME (default: tangle-study-api)
#   INFRA_FORCE_TCP_INGRESS_RECYCLE (set to 1 to disable/enable ingress)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"
REDIS_APP="${REDIS_APP_NAME:-tangle-study-redis}"
API_APP="${API_APP_NAME:-tangle-study-api}"
REDIS_PORT=6379

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

redis_connection_string() {
  local domain="$1"
  echo "$(redis_internal_host "$domain"):${REDIS_PORT}"
}

redis_url() {
  local domain="$1"
  echo "redis://$(redis_connection_string "$domain")"
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

ensure_api_redis_env() {
  local domain conn current
  if ! az containerapp show --name "$API_APP" --resource-group "$RG" &>/dev/null; then
    return 0
  fi

  domain="$(container_app_env_default_domain "$API_APP")"
  conn="$(redis_connection_string "$domain")"
  current="$(container_app_env_value "$API_APP" "Redis__ConnectionString")"

  if [[ "$current" == "$conn" ]]; then
    echo "==> ${API_APP}: Redis__ConnectionString already ${conn}"
    return 0
  fi

  echo "==> ${API_APP}: setting Redis__ConnectionString=${conn} (was ${current:-unset})"
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
    echo "==> ${worker}: setting REDIS_URL=${url}"
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
  addr="$(redis_connection_string "$domain")"
  current="$(container_app_env_value tangle-study-redis-exporter REDIS_ADDR)"

  if [[ "$current" == "$addr" ]]; then
    echo "==> tangle-study-redis-exporter: REDIS_ADDR already set"
    return 0
  fi

  echo "==> tangle-study-redis-exporter: setting REDIS_ADDR=${addr}"
  az containerapp update \
    --name tangle-study-redis-exporter \
    --resource-group "$RG" \
    --set-env-vars "REDIS_ADDR=${addr}" \
    --output none
}

if ! az containerapp show --name "$REDIS_APP" --resource-group "$RG" &>/dev/null; then
  echo "==> ${REDIS_APP} not found in ${RG}; skipping Redis ensure"
  exit 0
fi

ensure_redis_tcp_ingress
echo "==> Waiting 15s for Redis TCP ingress routing to stabilize..."
sleep 15

ensure_api_redis_env
ensure_worker_redis_url
ensure_redis_exporter_addr

echo "==> Redis ingress and connection env reconciled."
