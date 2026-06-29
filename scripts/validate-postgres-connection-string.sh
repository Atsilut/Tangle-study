#!/usr/bin/env bash
# Validate POSTGRES_CONNECTION_STRING before deploy (same rules as azure-cd-inject-secrets.sh).
#
# Usage:
#   POSTGRES_CONNECTION_STRING='...' ./scripts/validate-postgres-connection-string.sh
#   ./scripts/validate-postgres-connection-string.sh 'Host=...;SSL Mode=VerifyFull;...'
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/postgres-connection-string.sh
source "$ROOT/scripts/lib/postgres-connection-string.sh"

if [[ $# -gt 0 ]]; then
  POSTGRES_CONNECTION_STRING="$1"
fi

: "${POSTGRES_CONNECTION_STRING:?POSTGRES_CONNECTION_STRING is required}"

validate_postgres_connection_string "$POSTGRES_CONNECTION_STRING"
echo "POSTGRES_CONNECTION_STRING looks valid."
