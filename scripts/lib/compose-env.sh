# shellcheck shell=bash
# Shared Docker Compose env-file helper. Source from repo scripts after setting ROOT.
#
#   COMPOSE_ENV_FILE=docker/versions.prod.env  # optional; omit for dev (latest) defaults

tangle_compose() {
  local args=()
  if [[ -n "${COMPOSE_ENV_FILE:-}" ]]; then
    local env_path="$COMPOSE_ENV_FILE"
    if [[ "$env_path" != /* ]]; then
      env_path="${ROOT}/${env_path}"
    fi
    if [[ ! -f "$env_path" ]]; then
      echo "COMPOSE_ENV_FILE not found: $env_path" >&2
      return 1
    fi
    args+=(--env-file "$env_path")
  fi
  if ((${#args[@]})); then
    docker compose "${args[@]}" "$@"
  else
    docker compose "$@"
  fi
}
