#!/usr/bin/env bash
# Inject GitHub Environment secrets into Azure Container Apps.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#   BLOB_CONNECTION_STRING
#   JWT_SECRET
#   WORKER_CALLBACK_SECRET
#   METRICS_SCRAPE_SECRET
#   POSTGRES_CONNECTION_STRING
#   GRAFANA_ADMIN_PASSWORD
#
# Optional env:
#   PLACES_API_KEY
#   POSTGRES_EXPORTER_DSN (default: derived from POSTGRES_CONNECTION_STRING for Neon)
#   APPLICATIONINSIGHTS_CONNECTION_STRING (auto-fetched from Azure when unset)
#   APP_INSIGHTS_NAME (default: tanglestudyprod-appi)
#   GHCR_REGISTRY_USERNAME / GHCR_REGISTRY_PASSWORD (private GHCR pull)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${BLOB_CONNECTION_STRING:?BLOB_CONNECTION_STRING is required}"
: "${JWT_SECRET:?JWT_SECRET is required}"
: "${WORKER_CALLBACK_SECRET:?WORKER_CALLBACK_SECRET is required}"
: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"
: "${POSTGRES_CONNECTION_STRING:?POSTGRES_CONNECTION_STRING is required}"
: "${GRAFANA_ADMIN_PASSWORD:?GRAFANA_ADMIN_PASSWORD is required}"

RG="$AZURE_RESOURCE_GROUP"
PLACES_API_KEY="${PLACES_API_KEY:-}"
APP_INSIGHTS_NAME="${APP_INSIGHTS_NAME:-tanglestudyprod-appi}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/postgres-connection-string.sh
source "$ROOT/scripts/lib/postgres-connection-string.sh"
# shellcheck source=scripts/lib/az-retry.sh
source "$ROOT/scripts/lib/az-retry.sh"

build_postgres_exporter_dsn() {
  python3 - "$1" <<'PY'
import re
import sys
import urllib.parse

conn = sys.argv[1].strip().strip('"').strip("'")


def get_key_value(key: str, raw: str) -> str:
    match = re.search(rf"(?:^|;)\s*{re.escape(key)}\s*=\s*([^;]*)", raw, re.I)
    return match.group(1).strip() if match else ""


def get_first(raw: str, keys: list[str]) -> str:
    for key in keys:
        value = get_key_value(key, raw)
        if value:
            return value
    return ""


def from_npgsql(raw: str) -> str:
    host = get_first(raw, ["Host", "Server", "Data Source"])
    database = get_first(raw, ["Database", "Initial Catalog"])
    username = get_first(raw, ["Username", "User ID", "User Id", "Uid", "User"])
    password = get_first(raw, ["Password", "Pwd"])
    port = get_first(raw, ["Port"]) or "5432"
    ssl_match = re.search(r"SSL\s*Mode\s*=\s*(Require|VerifyCA|VerifyFull)\b", raw, re.I)
    sslmode = (
        {
            "require": "require",
            "verifyca": "verify-ca",
            "verifyfull": "verify-full",
        }.get(ssl_match.group(1).replace(" ", "").lower(), "disable")
        if ssl_match
        else "disable"
    )

    if not all([host, database, username, password]):
        missing = [
            name
            for name, value in [
                ("Host", host),
                ("Database", database),
                ("Username", username),
                ("Password", password),
            ]
            if not value
        ]
        raise SystemExit(
            "POSTGRES_CONNECTION_STRING must include Host, Database, Username, and Password "
            f"(missing: {', '.join(missing)}), or use a postgresql:// URI, "
            "or set POSTGRES_EXPORTER_DSN explicitly."
        )

    user = urllib.parse.quote(username, safe="")
    pw = urllib.parse.quote(password, safe="")
    return f"postgresql://{user}:{pw}@{host}:{port}/{database}?sslmode={sslmode}"


def from_uri(raw: str) -> str:
    parsed = urllib.parse.urlparse(raw)
    if parsed.scheme not in ("postgres", "postgresql"):
        raise SystemExit(f"Unsupported connection string scheme: {parsed.scheme}")

    if not parsed.hostname:
        raise SystemExit(
            "POSTGRES_CONNECTION_STRING URI must include hostname "
            "(or set POSTGRES_EXPORTER_DSN explicitly)."
        )
    if not parsed.username or parsed.password is None:
        raise SystemExit(
            "POSTGRES_CONNECTION_STRING URI must include username and password "
            "(or set POSTGRES_EXPORTER_DSN explicitly)."
        )

    database = urllib.parse.unquote(parsed.path.lstrip("/"))
    if not database:
        raise SystemExit(
            "POSTGRES_CONNECTION_STRING URI must include database name in the path "
            "(or set POSTGRES_EXPORTER_DSN explicitly)."
        )

    query = urllib.parse.parse_qs(parsed.query, keep_blank_values=True)
    sslmode = ((query.get("sslmode") or ["require"])[0] or "require").lower()
    if sslmode not in {"require", "verify-ca", "verify-full"}:
        sslmode = "require"
    port = parsed.port or 5432
    user = urllib.parse.quote(urllib.parse.unquote(parsed.username), safe="")
    pw = urllib.parse.quote(urllib.parse.unquote(parsed.password), safe="")
    return f"postgresql://{user}:{pw}@{parsed.hostname}:{port}/{database}?sslmode={sslmode}"


if re.match(r"^postgres(ql)?://", conn, re.I):
    print(from_uri(conn))
else:
    print(from_npgsql(conn))
PY
}

echo "==> Validating POSTGRES_CONNECTION_STRING"
validate_postgres_connection_string "$POSTGRES_CONNECTION_STRING"

POSTGRES_EXPORTER_DSN="${POSTGRES_EXPORTER_DSN:-$(build_postgres_exporter_dsn "$POSTGRES_CONNECTION_STRING")}"

if [[ -z "${APPLICATIONINSIGHTS_CONNECTION_STRING:-}" ]]; then
  echo "==> Resolving Application Insights connection string from Azure (${APP_INSIGHTS_NAME})"
  APPLICATIONINSIGHTS_CONNECTION_STRING="$(az_retry az monitor app-insights component show \
    --app "$APP_INSIGHTS_NAME" \
    --resource-group "$RG" \
    --query connectionString \
    --output tsv)"
fi
: "${APPLICATIONINSIGHTS_CONNECTION_STRING:?APPLICATIONINSIGHTS_CONNECTION_STRING is required (set secret or ensure App Insights exists)}"

echo "==> API secrets (tangle-study-api)"
SECRET_ARGS=(
  "postgres-conn=${POSTGRES_CONNECTION_STRING}"
  "blob-conn=${BLOB_CONNECTION_STRING}"
  "jwt-secret=${JWT_SECRET}"
  "worker-callback=${WORKER_CALLBACK_SECRET}"
  "metrics-secret=${METRICS_SCRAPE_SECRET}"
  "appinsights-conn=${APPLICATIONINSIGHTS_CONNECTION_STRING}"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  SECRET_ARGS+=("places-api-key=${PLACES_API_KEY}")
fi

az_retry az containerapp secret set \
  --name tangle-study-api \
  --resource-group "$RG" \
  --secrets "${SECRET_ARGS[@]}" \
  --output none

API_ENV=(
  "ConnectionStrings__DefaultConnection=secretref:postgres-conn"
  "Media__ConnectionString=secretref:blob-conn"
  "Jwt__Secret=secretref:jwt-secret"
  "Media__WorkerCallbackSecret=secretref:worker-callback"
  "Metrics__ScrapeSecret=secretref:metrics-secret"
  "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-conn"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  API_ENV+=("Places__ApiKey=secretref:places-api-key")
fi

az_retry az containerapp update \
  --name tangle-study-api \
  --resource-group "$RG" \
  --set-env-vars "${API_ENV[@]}" \
  --output none

echo "==> Postgres exporter secrets (tangle-study-postgres-exporter)"
az_retry az containerapp secret set \
  --name tangle-study-postgres-exporter \
  --resource-group "$RG" \
  --secrets "postgres-dsn=${POSTGRES_EXPORTER_DSN}" \
  --output none

az_retry az containerapp update \
  --name tangle-study-postgres-exporter \
  --resource-group "$RG" \
  --set-env-vars "DATA_SOURCE_NAME=secretref:postgres-dsn" \
  --output none

echo "==> Prometheus secrets (tangle-study-prometheus)"
az_retry az containerapp secret set \
  --name tangle-study-prometheus \
  --resource-group "$RG" \
  --secrets "metrics-secret=${METRICS_SCRAPE_SECRET}" \
  --output none

az_retry az containerapp update \
  --name tangle-study-prometheus \
  --resource-group "$RG" \
  --set-env-vars "METRICS_SCRAPE_SECRET=secretref:metrics-secret" \
  --output none

echo "==> Grafana secrets (tangle-study-grafana)"
az_retry az containerapp secret set \
  --name tangle-study-grafana \
  --resource-group "$RG" \
  --secrets "grafana-admin-password=${GRAFANA_ADMIN_PASSWORD}" \
  --output none

az_retry az containerapp update \
  --name tangle-study-grafana \
  --resource-group "$RG" \
  --set-env-vars "GF_SECURITY_ADMIN_PASSWORD=secretref:grafana-admin-password" \
  --output none

echo "==> Migrate job secrets (tangle-study-migrate)"
az_retry az containerapp job secret set \
  --name tangle-study-migrate \
  --resource-group "$RG" \
  --secrets "postgres-conn=${POSTGRES_CONNECTION_STRING}" \
  --output none

az_retry az containerapp job update \
  --name tangle-study-migrate \
  --resource-group "$RG" \
  --set-env-vars "ConnectionStrings__DefaultConnection=secretref:postgres-conn" \
  --output none

inject_worker_metrics_secret() {
  local app="$1"
  shift
  local -a extra_secrets=("$@")
  local -a secrets=("metrics-secret=${METRICS_SCRAPE_SECRET}")
  if ((${#extra_secrets[@]} > 0)); then
    secrets+=("${extra_secrets[@]}")
  fi

  echo "==> Worker secrets (${app})"
  az_retry az containerapp secret set \
    --name "$app" \
    --resource-group "$RG" \
    --secrets "${secrets[@]}" \
    --output none

  local -a env_vars=("METRICS_SCRAPE_SECRET=secretref:metrics-secret")
  if [[ "$app" == "tangle-study-worker-media" ]]; then
    env_vars+=(
      "AZURE_STORAGE_CONNECTION_STRING=secretref:blob-conn"
      "WORKER_CALLBACK_SECRET=secretref:worker-callback"
    )
  fi

  az_retry az containerapp update \
    --name "$app" \
    --resource-group "$RG" \
    --set-env-vars "${env_vars[@]}" \
    --output none
}

inject_worker_metrics_secret "tangle-study-worker-chat"
inject_worker_metrics_secret "tangle-study-worker-location"
inject_worker_metrics_secret "tangle-study-worker-media" \
  "blob-conn=${BLOB_CONNECTION_STRING}" \
  "worker-callback=${WORKER_CALLBACK_SECRET}"

if [[ -n "${GHCR_REGISTRY_USERNAME:-}" && -n "${GHCR_REGISTRY_PASSWORD:-}" ]]; then
  echo "==> GHCR registry credentials (private packages)"
  REGISTRY_APPS=(
    tangle-study-api
    tangle-study-web
    tangle-study-worker-chat
    tangle-study-worker-media
    tangle-study-worker-location
    tangle-study-prometheus
    tangle-study-grafana
  )
  for app in "${REGISTRY_APPS[@]}"; do
    az_retry az containerapp registry set \
      --name "$app" \
      --resource-group "$RG" \
      --server ghcr.io \
      --username "$GHCR_REGISTRY_USERNAME" \
      --password "$GHCR_REGISTRY_PASSWORD" \
      --output none
  done
  az_retry az containerapp job registry set \
    --name tangle-study-migrate \
    --resource-group "$RG" \
    --server ghcr.io \
    --username "$GHCR_REGISTRY_USERNAME" \
    --password "$GHCR_REGISTRY_PASSWORD" \
    --output none
fi

echo "==> Reconciling Redis connection env"
REDIS_APP="${REDIS_APP_NAME:-tangle-study-redis}"
API_APP="${API_APP_NAME:-tangle-study-api}"
REDIS_PORT=6379
# shellcheck source=scripts/lib/azure-redis-readiness.sh
source "$ROOT/scripts/lib/azure-redis-readiness.sh"
reconcile_redis_connection_env

echo "==> Secrets injected."
