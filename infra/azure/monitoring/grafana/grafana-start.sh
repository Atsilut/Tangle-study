#!/bin/sh
# Point provisioned Prometheus datasource at ACA internal URL.
# Provisioning is copied to a writable path — /etc/grafana/provisioning is root-owned in the image.
set -e

: "${PROMETHEUS_URL:?PROMETHEUS_URL is required}"

PROV_ROOT="/var/lib/grafana/provisioning"
mkdir -p "$PROV_ROOT"
cp -a /usr/share/tangle/grafana-provisioning/. "$PROV_ROOT/"

sed -i "s|http://prometheus:9090|${PROMETHEUS_URL}|g" \
  "$PROV_ROOT/datasources/prometheus.yml"

export GF_PATHS_PROVISIONING="$PROV_ROOT"
exec /run.sh "$@"
