#!/usr/bin/env bash
# Reconcile API ingress port and web nginx upstream after real images replace placeholders.
#
# Infra deployed with usePlaceholderImages=true sets API targetPort=80 and
# TANGLE_API_UPSTREAM to the API internal FQDN on port 80. Real API images listen on 8080;
# web nginx then proxies /health to the wrong port and smoke gets HTTP 404.
#
# Cross-app HTTP on ACA uses internal FQDN (tangle-study-api.internal.<domain>:8080);
# the short app name can hang from the web app (same class of issue as Redis TCP).
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   API_APP_NAME (default: tangle-study-api)
#   WEB_APP_NAME (default: tangle-study-web)
#   API_TARGET_PORT (default: 8080)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"
API_APP="${API_APP_NAME:-tangle-study-api}"
WEB_APP="${WEB_APP_NAME:-tangle-study-web}"
API_TARGET_PORT="${API_TARGET_PORT:-8080}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/azure-container-apps-readiness.sh
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"

# HTTP cross-app calls use internal FQDN (short app name can hang on ACA); see prometheus scrape targets.
EXPECTED_UPSTREAM="$(aca_internal_app_upstream "$API_APP" "$RG" "$API_TARGET_PORT")"

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
}

ensure_web_upstream() {
  local current
  if ! az containerapp show --name "$WEB_APP" --resource-group "$RG" &>/dev/null; then
    echo "==> ${WEB_APP}: not found; skip web upstream reconcile"
    return 0
  fi

  current="$(az containerapp show \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].env[?name=='TANGLE_API_UPSTREAM'].value | [0]" \
    --output tsv 2>/dev/null || true)"

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
ensure_web_upstream

echo "==> Waiting 20s for API/web revisions to pick up runtime config..."
sleep 20

echo "==> API/web runtime reconciled."
