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
      echo "Unknown option: $arg" >&2
      echo "Usage: $0 [--yes]" >&2
      exit 1
      ;;
  esac
done

if [[ ! -f docker-compose.yml ]]; then
  echo "error: run from the Tangle repo root (docker-compose.yml not found)." >&2
  exit 1
fi

if ! docker compose ps --status running db --quiet 2>/dev/null | grep -q .; then
  echo "error: the Compose db service is not running." >&2
  echo "Start it with: docker compose up -d db" >&2
  exit 1
fi

if [[ "$SKIP_CONFIRM" != true ]]; then
  echo "This will DELETE ALL application data in local Postgres:"
  echo "  database: $DB_NAME"
  echo "  host:     localhost:5433 (Compose db service)"
  echo
  echo "Schema and migrations are kept. Redis and blob storage are NOT cleared."
  echo
  read -r -p "Type 'yes' to continue: " answer
  if [[ "$answer" != "yes" ]]; then
    echo "Aborted."
    exit 1
  fi
fi

echo "Truncating application tables..."

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

echo "Done. Local dev database is empty (schema intact)."
echo
echo "Next steps for a clean frontend smoke test:"
echo "  - Clear saved login: DevTools -> Application -> Local Storage -> remove 'tangle-auth'"
echo "    (or use Sign out if the app still has a session)."
echo "  - Sign up again at http://localhost:5173/register"
