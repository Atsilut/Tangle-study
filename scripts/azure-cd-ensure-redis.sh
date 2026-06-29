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
#   REDIS_ENSURE_PHASE (early|late, default: late)
#   REDIS_SKIP_TCP_PROBE (set to 1 to skip cross-app TCP probe in late phase)
#   INFRA_FORCE_TCP_INGRESS_RECYCLE (set to 1 to disable/enable ingress)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"
REDIS_APP="${REDIS_APP_NAME:-tangle-study-redis}"
API_APP="${API_APP_NAME:-tangle-study-api}"
REDIS_PORT=6379
PHASE="${REDIS_ENSURE_PHASE:-late}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/versions-prod-env.sh
source "$ROOT/scripts/lib/versions-prod-env.sh"
load_versions_prod_env "$ROOT"
DEBIAN_IMAGE="${DEBIAN_IMAGE:-debian:bookworm-slim}"
# shellcheck source=scripts/lib/azure-redis-readiness.sh
source "$ROOT/scripts/lib/azure-redis-readiness.sh"
# shellcheck source=scripts/lib/azure-container-apps-readiness.sh
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"

if ! az containerapp show --name "$REDIS_APP" --resource-group "$RG" &>/dev/null; then
  echo "==> ${REDIS_APP} not found in ${RG}; skipping Redis ensure"
  exit 0
fi

echo "==> Redis ensure phase: ${PHASE}"

ensure_redis_tcp_ingress

if [[ "$PHASE" == "early" ]]; then
  echo "==> Waiting 15s for Redis TCP ingress routing to stabilize..."
  sleep 15
  reconcile_redis_connection_env
  echo "==> Redis ingress and connection env reconciled (early; TCP probe deferred)."
  exit 0
fi

echo "==> Waiting 15s for Redis TCP ingress routing to stabilize..."
sleep 15

if [[ "${REDIS_SKIP_TCP_PROBE:-}" == "1" ]]; then
  echo "==> WARNING: REDIS_SKIP_TCP_PROBE=1; skipping cross-app TCP probe" >&2
  reconcile_redis_connection_env
  echo "==> Waiting for ${API_APP} revision after Redis env reconcile..."
  wait_for_container_app_revision_healthy "$API_APP" "$RG" "${API_READY_TIMEOUT_SEC:-300}"
  echo "==> Redis ingress and connection env reconciled (probe skipped)."
  exit 0
fi

if ! ensure_redis_cross_app_tcp; then
  echo "==> Redis cross-app TCP probe failed; CD cannot continue safely." >&2
  exit 1
fi

reconcile_redis_connection_env

echo "==> Waiting for ${API_APP} revision after Redis env reconcile..."
wait_for_container_app_revision_healthy "$API_APP" "$RG" "${API_READY_TIMEOUT_SEC:-300}"

echo "==> Redis ingress, cross-app TCP, and connection env reconciled."
