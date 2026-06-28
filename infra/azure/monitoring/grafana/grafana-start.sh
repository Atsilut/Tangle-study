#!/bin/sh
# Point provisioned Prometheus datasource at ACA internal FQDN.
set -e

: "${PROMETHEUS_URL:?PROMETHEUS_URL is required}"

if [ -f /etc/grafana/provisioning/datasources/prometheus.yml ]; then
  sed -i "s|http://prometheus:9090|${PROMETHEUS_URL}|g" /etc/grafana/provisioning/datasources/prometheus.yml
fi

exec /run.sh "$@"
