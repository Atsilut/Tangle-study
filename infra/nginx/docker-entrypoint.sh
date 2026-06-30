#!/bin/sh
set -eu

: "${TANGLE_API_UPSTREAM:=api:8080}"
: "${TANGLE_API_HOST:=${TANGLE_API_UPSTREAM%%:*}}"

export TANGLE_API_UPSTREAM
export TANGLE_API_HOST

envsubst '${TANGLE_API_UPSTREAM} ${TANGLE_API_HOST}' \
  < /etc/nginx/templates/default.conf.template \
  > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'