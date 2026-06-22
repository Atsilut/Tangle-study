#!/usr/bin/env bash
# Apply EF Core migrations against the configured database.
#
# Local (Compose db):
#   ./scripts/migrate.sh
#
# Production / staging (connection string from env or GitHub secret):
#   ASPNETCORE_ENVIRONMENT=Production \
#   ConnectionStrings__DefaultConnection="$POSTGRES_CONNECTION_STRING" \
#   ./scripts/migrate.sh --production
#
# Azure Container Apps Job (planned CD step):
#   dotnet Api.dll --migrate
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/lib/compose-env.sh
source "$ROOT/scripts/lib/compose-env.sh"

MODE="${1:-local}"

if [[ "$MODE" == "--production" ]]; then
  if [[ -z "${ConnectionStrings__DefaultConnection:-}" ]]; then
    echo "ConnectionStrings__DefaultConnection is required for --production." >&2
    exit 1
  fi

  tangle_compose build api
  tangle_compose run --rm --no-deps \
    -e ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}" \
    -e "ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection}" \
    api dotnet Api.dll --migrate
elif [[ "$MODE" == "local" ]]; then
  tangle_compose build api
  tangle_compose run --rm --no-deps api dotnet Api.dll --migrate
else
  echo "Usage: $0 [local|--production]" >&2
  exit 1
fi
