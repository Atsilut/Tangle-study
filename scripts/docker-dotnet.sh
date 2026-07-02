#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[DOTNET]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"

if [[ $# -eq 0 ]]; then
  fail "usage: $0 <dotnet-args...> (example: $0 ef migrations add MyMigration --project services/Api)"
fi

log_step "BUILD SDK IMAGE"
tangle_compose --profile tools build sdk

log_step "RUN DOTNET"
tangle_compose --profile tools run --rm sdk "$@"
