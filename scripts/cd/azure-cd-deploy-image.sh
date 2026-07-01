#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=scripts/cd/libs/common.sh
source "$ROOT/scripts/cd/libs/common.sh"

# Validate required environment variables
: "${AZURE_RESOURCE_GROUP:?required}"
PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"

[[ -f "$PARAM_FILE" ]] || fail "Parameter file not found: $PARAM_FILE"

# Extract registry and tag defaults for own-built images
CONTAINER_REGISTRY="${CONTAINER_REGISTRY:-$(jq -r '.parameters.containerRegistry.value // empty' "$PARAM_FILE")}"
IMAGE_TAG="${IMAGE_TAG:-$(jq -r '.parameters.imageTag.value // empty' "$PARAM_FILE")}"

log_step "INTEGRATED APPLICATION DEPLOYMENT START"
log_info "resource-group=$AZURE_RESOURCE_GROUP param-file=$PARAM_FILE"

# shellcheck source=scripts/cd/libs/azure-aca-urls.sh
source "$ROOT/scripts/cd/libs/azure-aca-urls.sh"
log_info "aca-api-url=${ACA_API_HTTP_URL} aca-prometheus-url=${ACA_PROMETHEUS_HTTP_URL} aca-redis=${ACA_REDIS_HOST}"

append_monitoring_env() {
  local app_name="$1"
  case "$app_name" in
    tangle-study-grafana)
      env_args+=("PROMETHEUS_URL=${ACA_PROMETHEUS_HTTP_URL}")
      ;;
  esac
}

append_api_env() {
  local app_name="$1"
  local arg filtered=()

  [[ "$app_name" == "tangle-study-api" ]] || return 0

  env_args+=("Redis__ConnectionString=${ACA_REDIS_ADDR}")

  [[ -n "${JWT_EXPIRY_MINUTES:-}" ]] || return 0

  for arg in "${env_args[@]}"; do
    [[ "$arg" == Jwt__ExpiryMinutes=* ]] && continue
    filtered+=("$arg")
  done
  env_args=("${filtered[@]}")
  env_args+=("Jwt__ExpiryMinutes=${JWT_EXPIRY_MINUTES}")
}

append_worker_env() {
  local app_name="$1"

  case "$app_name" in
    tangle-study-worker-*)
      ;;
    *)
      return 0
      ;;
  esac

  env_args+=("REDIS_URL=${ACA_REDIS_URL}")

  case "$app_name" in
    tangle-study-worker-media|tangle-study-worker-location)
      env_args+=("API_BASE_URL=${ACA_API_HTTP_URL}")
      ;;
  esac
}

append_infra_env() {
  local app_name="$1"
  case "$app_name" in
    tangle-study-redis-exporter)
      env_args+=("REDIS_ADDR=${ACA_REDIS_ADDR}")
      ;;
  esac
}

############################################
log_step "PROCESSING CONTAINER APPS"

# Iterate through all configured container apps exactly once
jq -c '.parameters.containerApps | to_entries[]' "$PARAM_FILE" \
  | while read -r app; do
      name=$(echo "$app" | jq -r '.key')
      echo ""
      echo "=================================================="
      log_step "DEPLOYING: $name"
      echo "=================================================="

      # 1. Resolve image reference (Own-built vs Infra/External image)
      image=$(echo "$app" | jq -r '.value.image // empty')
      infra_key=$(echo "$app" | jq -r '.value.infraImage // empty')
      ref=""

      if [[ -n "$image" ]]; then
        [[ -n "$CONTAINER_REGISTRY" && "$CONTAINER_REGISTRY" != "null" ]] || fail "[$name] containerRegistry is missing or null"
        [[ -n "$IMAGE_TAG" && "$IMAGE_TAG" != "null" ]] || fail "[$name] imageTag is missing or null"
        ref="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"
      elif [[ -n "$infra_key" ]]; then
        ref=$(jq -r --arg k "$infra_key" '.parameters.infra.value[$k].image // empty' "$PARAM_FILE")
        [[ -n "$ref" && "$ref" != "null" ]] || fail "[$name] Infra image not found for key: $infra_key"
      fi

      # 2. Parse all environment variables into a bash array
      env_json=$(echo "$app" | jq -c '.value.env // {}')
      env_args=()
      
      while IFS="=" read -r k v; do
        if [[ -n "$k" ]]; then
          env_args+=("${k}=${v}")
        fi
      done < <(echo "$env_json" | jq -r 'to_entries[] | "\(.key)=\(.value)"')

      append_monitoring_env "$name"
      append_api_env "$name"
      append_worker_env "$name"
      append_infra_env "$name"

      # 3. Build and execute a single atomic CLI command to prevent duplicate revisions
      cmd=("az" "containerapp" "update" "--name" "$name" "--resource-group" "$AZURE_RESOURCE_GROUP" "--output" "none")

      if [[ -n "$ref" ]]; then
        log_info "  -> Target image: $ref"
        cmd+=("--image" "$ref")
      fi

      if [[ ${#env_args[@]} -gt 0 ]]; then
        log_info "  -> Target envs: ${#env_args[@]} variables detected"
        cmd+=("--set-env-vars" "${env_args[@]}")
      fi

      # Trigger deployment only if there are structural changes
      if [[ -n "$ref" || ${#env_args[@]} -gt 0 ]]; then
        log_info "  -> Deploying single-shot revision..."
        "${cmd[@]}"
        log_info "  -> Successfully deployed: $name"
      else
        log_warn "  -> No image or env configuration found. Skipping: $name"
      fi
      echo ""
      echo "=================================================="
    done

############################################
echo ""
echo "=================================================="
log_step "DEPLOYING: tangle-study-migrate"
echo "=================================================="

MIGRATE_IMAGE="${CONTAINER_REGISTRY}/tangle-study-api:${IMAGE_TAG}"

log_info "Processing updates for job: tangle-study-migrate"
log_info "  -> Target image: $MIGRATE_IMAGE"

az containerapp job update \
  --name tangle-study-migrate \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --image "$MIGRATE_IMAGE" \
  --output none

log_info "  -> Successfully deployed job: tangle-study-migrate"

############################################
log_step "DEPLOYMENT SUMMARY"
log_info "status=completed"