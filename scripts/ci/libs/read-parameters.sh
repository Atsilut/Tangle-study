# shellcheck shell=bash
# jq helpers for infra/azure/parameters.prod.json. Set PARAM_FILE before sourcing.

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"

param_infra_image() {
  jq -r --arg k "$1" '.parameters.infra.value[$k].image' "$PARAM_FILE"
}

param_build_image() {
  jq -r --arg k "$1" '.parameters.buildImages.value[$k]' "$PARAM_FILE"
}
