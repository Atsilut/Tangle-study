#!/usr/bin/env bash
# Wipe application data from the local Docker Compose Postgres (dev only).
#
# Keeps the schema and __EFMigrationsHistory; deletes all rows and resets
# identity sequences. Does not touch Redis, Azurite blobs, or volumes.
#
# Usage:
#   ./scripts/dev-clear-db.sh          # interactive confirm
#   ./scripts/dev-clear-db.sh --yes    # skip confirm
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[DEV]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

DB_USER="tangle"
DB_NAME="tangledb"
SKIP_CONFIRM=false

for arg in "$@"; do
  case "$arg" in
    --yes | -y) SKIP_CONFIRM=true ;;
    -h | --help)
      echo "Usage: $0 [--yes]"
      echo
      echo "Delete all application rows from the local Compose Postgres ($DB_NAME on db:5432)."
      echo "Schema and EF migration history are preserved."
      exit 0
      ;;
    *)
      fail "unknown option: $arg (usage: $0 [--yes])"
      ;;
  esac
done

[[ -f docker-compose.yml ]] || fail "run from the Tangle repo root (docker-compose.yml not found)"

if ! docker compose ps --status running db --quiet 2>/dev/null | grep -q .; then
  fail "the Compose db service is not running (start with: docker compose up -d db)"
fi

if [[ "$SKIP_CONFIRM" != true ]]; then
  log_info "this will DELETE ALL application data in local Postgres:"
  log_info "database=$DB_NAME host=localhost:5433"
  log_info "schema and migrations are kept; Redis and blob storage are NOT cleared"
  read -r -p "Type 'yes' to continue: " answer
  [[ "$answer" == "yes" ]] || fail "aborted"
fi

log_step "TRUNCATE APPLICATION TABLES"

docker compose exec -T db psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 <<'SQL'
DO $truncate$
DECLARE
  stmt text;
BEGIN
  SELECT 'TRUNCATE TABLE '
         || string_agg(format('%I', tablename), ', ' ORDER BY tablename)
         || ' RESTART IDENTITY CASCADE'
  INTO stmt
  FROM pg_tables
  WHERE schemaname = 'public'
    AND tablename <> '__EFMigrationsHistory';

  IF stmt IS NULL THEN
    RAISE NOTICE 'No application tables to truncate.';
  ELSE
    EXECUTE stmt;
  END IF;
END $truncate$;
SQL

log_info "local dev database is empty (schema intact)"
log_info "next: clear saved login in DevTools or sign out, then sign up at http://localhost:5173/register"
