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
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[MIGRATE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"

MODE="${1:-local}"

if [[ "$MODE" == "--production" ]]; then
  require_env ConnectionStrings__DefaultConnection

  log_step "PRODUCTION MIGRATE"
  tangle_compose build api
  tangle_compose run --rm --no-deps \
    -e ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}" \
    -e "ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection}" \
    api dotnet Api.dll --migrate
elif [[ "$MODE" == "local" ]]; then
  log_step "LOCAL MIGRATE"
  tangle_compose build api
  tangle_compose run --rm --no-deps api dotnet Api.dll --migrate
else
  fail "usage: $0 [local|--production]"
fi

log_info "migration completed"
