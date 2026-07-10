#!/usr/bin/env bash
# Apply EF Core migrations for all domain services against the configured database.
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

MIGRATE_PROJECTS=(
  services/Users/Users.csproj
  services/Media/Media.csproj
  services/Chat/Chat.csproj
  services/Location/Location.csproj
  services/Community/Community.csproj
  services/Group/Group.csproj
  services/Social/Social.csproj
)

run_service_migrate() {
  local project="$1"
  local service_name
  service_name="$(basename "$(dirname "$project")")"
  log_step "MIGRATE ${service_name}"

  if [[ "$MODE" == "--production" ]]; then
    tangle_compose --profile tools run --rm --no-deps \
      -e ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}" \
      -e "ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection}" \
      sdk dotnet run --project "$project" -c Release -- --migrate
  else
    tangle_compose --profile tools run --rm --no-deps \
      sdk dotnet run --project "$project" -c Release -- --migrate
  fi
}

if [[ "$MODE" == "--production" ]]; then
  require_env ConnectionStrings__DefaultConnection
  log_step "PRODUCTION MIGRATE"
elif [[ "$MODE" == "local" ]]; then
  log_step "LOCAL MIGRATE"
else
  fail "usage: $0 [local|--production]"
fi

for project in "${MIGRATE_PROJECTS[@]}"; do
  run_service_migrate "$project"
done

log_info "migration completed"
