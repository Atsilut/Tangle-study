#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"

if [[ $# -eq 0 ]]; then
  echo "Usage: $0 <dotnet-args...>" >&2
  echo "Example: $0 ef migrations add MyMigration --project services/Api" >&2
  exit 1
fi

tangle_compose --profile tools build sdk
tangle_compose --profile tools run --rm sdk "$@"
