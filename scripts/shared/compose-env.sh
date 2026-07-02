# shellcheck shell=bash
# Shared Docker Compose env-file helper. Source after ROOT and common.sh are set.
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
      if declare -F log_error >/dev/null 2>&1; then
        log_error "COMPOSE_ENV_FILE not found: $env_path"
      else
        echo "COMPOSE_ENV_FILE not found: $env_path" >&2
      fi
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
