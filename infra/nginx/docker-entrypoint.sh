#!/bin/sh
set -eu

# Optional override for Azure Container Apps internal DNS (host:port).
# Also sets proxy Host to the API internal hostname — ACA ingress routes by hostname,
# not the public web FQDN ($host).
if [ -n "${TANGLE_API_UPSTREAM:-}" ]; then
  api_host="${TANGLE_API_UPSTREAM%%:*}"
  sed -e "s|server tangle-study-api:8080;|server ${TANGLE_API_UPSTREAM};|" \
      -e "s|server api:8080;|server ${TANGLE_API_UPSTREAM};|" \
      -e "s|proxy_set_header Host \$host;|proxy_set_header Host ${api_host};|g" \
    /etc/nginx/conf.d/default.conf > /tmp/default.conf
  mv /tmp/default.conf /etc/nginx/conf.d/default.conf
fi

exec nginx -g 'daemon off;'
