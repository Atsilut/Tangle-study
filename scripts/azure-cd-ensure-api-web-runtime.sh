#!/usr/bin/env bash
# Reconcile API ingress port and web nginx upstream after real images replace placeholders.
#
# Infra deployed with usePlaceholderImages=true sets API targetPort=80 and
# TANGLE_API_UPSTREAM to the short app name on port 80. Real API images listen on 8080;
# web nginx then proxies /health to the wrong port and smoke gets HTTP 404.
#
# Cross-app HTTP on ACA is hostname-path dependent (same as Redis TCP). CD probes from
# the web pod: short app name first, then internal FQDN; reconciles TANGLE_API_UPSTREAM
# and worker API_BASE_URL to whichever path works.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   API_APP_NAME (default: tangle-study-api)
#   WEB_APP_NAME (default: tangle-study-web)
#   WORKER_MEDIA_APP_NAME (default: tangle-study-worker-media)
#   WORKER_LOCATION_APP_NAME (default: tangle-study-worker-location)
#   API_TARGET_PORT (default: 8080)
#   RUNTIME_WAIT_TIMEOUT_SEC (default: 300)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"
API_APP="${API_APP_NAME:-tangle-study-api}"
WEB_APP="${WEB_APP_NAME:-tangle-study-web}"
WORKER_MEDIA_APP="${WORKER_MEDIA_APP_NAME:-tangle-study-worker-media}"
WORKER_LOCATION_APP="${WORKER_LOCATION_APP_NAME:-tangle-study-worker-location}"
API_TARGET_PORT="${API_TARGET_PORT:-8080}"
RUNTIME_WAIT_TIMEOUT_SEC="${RUNTIME_WAIT_TIMEOUT_SEC:-300}"
API_PORT_UPDATED=0
WEB_UPSTREAM_UPDATED=0
WORKER_API_URL_UPDATED=0
EXPECTED_UPSTREAM=""

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/azure-container-apps-readiness.sh
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"

container_app_image() {
  local app_name="$1"
  az containerapp show \
    --name "$app_name" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].image" \
    --output tsv 2>/dev/null || true
}

api_uses_placeholder_image() {
  local image="$1"
  [[ "$image" == *"/k8se/quickstart:"* || "$image" == *"k8se/quickstart"* ]]
}

ensure_api_target_port() {
  local current
  if ! az containerapp show --name "$API_APP" --resource-group "$RG" &>/dev/null; then
    echo "==> ${API_APP}: not found; skip API ingress reconcile"
    return 0
  fi

  current="$(az containerapp show \
    --name "$API_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.targetPort" \
    --output tsv 2>/dev/null || true)"

  if [[ "$current" == "$API_TARGET_PORT" ]]; then
    echo "==> ${API_APP}: ingress targetPort already ${API_TARGET_PORT}"
    return 0
  fi

  echo "==> ${API_APP}: setting ingress targetPort ${current:-unset} -> ${API_TARGET_PORT}"
  az containerapp ingress update \
    --name "$API_APP" \
    --resource-group "$RG" \
    --target-port "$API_TARGET_PORT" \
    --output none
  API_PORT_UPDATED=1
}

resolve_api_http_upstream() {
  if ! az containerapp show --name "$WEB_APP" --resource-group "$RG" &>/dev/null; then
    echo "==> ${WEB_APP}: not found; cannot probe API HTTP upstream" >&2
    return 1
  fi

  wait_for_container_app_revision_healthy "$API_APP" "$RG" "$RUNTIME_WAIT_TIMEOUT_SEC"
  wait_for_container_app_revision_healthy "$WEB_APP" "$RG" "$RUNTIME_WAIT_TIMEOUT_SEC"

  if ! try_api_http_hosts_from_web "$WEB_APP" "$API_APP" "$RG" "$API_TARGET_PORT"; then
    echo "==> Cross-app HTTP from ${WEB_APP} to ${API_APP} failed (short name and internal FQDN)." >&2
    echo "==> Hint: check ${API_APP} internal ingress, revision health, and consider restarting API/web revisions." >&2
    return 1
  fi

  EXPECTED_UPSTREAM="$API_HTTP_UPSTREAM"
  echo "==> Resolved API HTTP upstream from ${WEB_APP}: ${EXPECTED_UPSTREAM}"
}

ensure_web_upstream() {
  local current
  if ! az containerapp show --name "$WEB_APP" --resource-group "$RG" &>/dev/null; then
    echo "==> ${WEB_APP}: not found; skip web upstream reconcile"
    return 0
  fi

  current="$(container_app_env_var "$WEB_APP" "$RG" "TANGLE_API_UPSTREAM")"

  if [[ "$current" == "$EXPECTED_UPSTREAM" ]]; then
    echo "==> ${WEB_APP}: TANGLE_API_UPSTREAM already ${EXPECTED_UPSTREAM}"
    return 0
  fi

  echo "==> ${WEB_APP}: setting TANGLE_API_UPSTREAM=${EXPECTED_UPSTREAM} (was ${current:-unset})"
  az containerapp update \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --set-env-vars "TANGLE_API_UPSTREAM=${EXPECTED_UPSTREAM}" \
    --output none
  WEB_UPSTREAM_UPDATED=1
}

ensure_worker_api_base_url() {
  local worker_app="$1"
  local expected_url="http://${EXPECTED_UPSTREAM}"
  local current

  if ! az containerapp show --name "$worker_app" --resource-group "$RG" &>/dev/null; then
    echo "==> ${worker_app}: not found; skip API_BASE_URL reconcile"
    return 0
  fi

  current="$(container_app_env_var "$worker_app" "$RG" "API_BASE_URL")"

  if [[ "$current" == "$expected_url" ]]; then
    echo "==> ${worker_app}: API_BASE_URL already ${expected_url}"
    return 0
  fi

  echo "==> ${worker_app}: setting API_BASE_URL=${expected_url} (was ${current:-unset})"
  az containerapp update \
    --name "$worker_app" \
    --resource-group "$RG" \
    --set-env-vars "API_BASE_URL=${expected_url}" \
    --output none
  WORKER_API_URL_UPDATED=1
}

api_image="$(container_app_image "$API_APP")"
if [[ -z "$api_image" ]]; then
  echo "==> ${API_APP} not found in ${RG}; skipping API/web runtime reconcile"
  exit 0
fi

if api_uses_placeholder_image "$api_image"; then
  echo "==> ${API_APP} still uses placeholder image (${api_image}); skip port/upstream reconcile"
  exit 0
fi

ensure_api_target_port

if [[ "$API_PORT_UPDATED" == "1" ]]; then
  echo "==> Waiting for ${API_APP} revision after ingress targetPort change..."
  wait_for_container_app_revision_healthy "$API_APP" "$RG" "$RUNTIME_WAIT_TIMEOUT_SEC"
fi

resolve_api_http_upstream
ensure_web_upstream
ensure_worker_api_base_url "$WORKER_MEDIA_APP"
ensure_worker_api_base_url "$WORKER_LOCATION_APP"

if [[ "$WEB_UPSTREAM_UPDATED" == "1" ]]; then
  echo "==> Waiting for ${WEB_APP} revision after TANGLE_API_UPSTREAM change..."
  wait_for_container_app_revision_healthy "$WEB_APP" "$RG" "$RUNTIME_WAIT_TIMEOUT_SEC"
fi

if [[ "$WORKER_API_URL_UPDATED" == "1" ]]; then
  echo "==> Worker API_BASE_URL updated; allow revisions to pick up env..."
  sleep 15
fi

echo "==> API/web runtime reconciled."
