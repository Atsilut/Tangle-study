# shellcheck shell=bash
# Load pinned container versions from docker/versions.prod.env.
# Honors COMPOSE_ENV_FILE when set (default: docker/versions.prod.env).

versions_prod_env_path() {
  local root="$1"
  local env_file="${COMPOSE_ENV_FILE:-docker/versions.prod.env}"

  if [[ "$env_file" != /* ]]; then
    env_file="${root}/${env_file}"
  fi

  if [[ ! -f "$env_file" ]]; then
    echo "Pinned versions env file not found: $env_file" >&2
    return 1
  fi

  printf '%s' "$env_file"
}

load_versions_prod_env() {
  local root="$1"
  local env_path
  env_path="$(versions_prod_env_path "$root")" || return 1

  set -a
  # shellcheck disable=SC1090
  source "$env_path"
  set +a
}
