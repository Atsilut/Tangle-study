#!/bin/sh
set -eu

# Optional override for Azure Container Apps internal DNS (e.g. tangle-api:8080).
if [ -n "${TANGLE_API_UPSTREAM:-}" ]; then
  sed "s|server api:8080;|server ${TANGLE_API_UPSTREAM};|" \
    /etc/nginx/conf.d/default.conf > /tmp/default.conf
  mv /tmp/default.conf /etc/nginx/conf.d/default.conf
fi

exec nginx -g 'daemon off;'
