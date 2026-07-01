#!/bin/sh
# Point provisioned Prometheus datasource at ACA internal URL.
# PROMETHEUS_URL must be the short app name without port (e.g. http://tangle-study-prometheus).
# Appending :9090 hits the pod IP and times out — same ACA rule as web→API nginx upstream.
# Provisioning is copied to a writable path — /etc/grafana/provisioning is root-owned in the image.
set -e

: "${PROMETHEUS_URL:?PROMETHEUS_URL is required}"

PROV_ROOT="/var/lib/grafana/provisioning"
mkdir -p "$PROV_ROOT"
cp -a /usr/share/tangle/grafana-provisioning/. "$PROV_ROOT/"

sed -i "s|http://prometheus:9090|${PROMETHEUS_URL}|g" \
  "$PROV_ROOT/datasources/prometheus.yml"

sed -i "s|path: /etc/grafana/provisioning/dashboards|path: ${PROV_ROOT}/dashboards|g" \
  "$PROV_ROOT/dashboards/dashboard.yml"

export GF_PATHS_PROVISIONING="$PROV_ROOT"
exec /run.sh "$@"
