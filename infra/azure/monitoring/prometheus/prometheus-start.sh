#!/bin/sh
# Generate ACA scrape configs from internal FQDNs (see infra/azure/README.md).
set -e

: "${ACA_DEFAULT_DOMAIN:?ACA_DEFAULT_DOMAIN is required}"
: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"

mkdir -p /etc/prometheus/scrape

cat > /etc/prometheus/scrape/aca.yml <<EOF
scrape_configs:
  - job_name: api
    static_configs:
      - targets: ["tangle-study-api.internal.${ACA_DEFAULT_DOMAIN}:8080"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: postgres
    static_configs:
      - targets: ["tangle-study-postgres-exporter.internal.${ACA_DEFAULT_DOMAIN}:9187"]

  - job_name: redis
    static_configs:
      - targets: ["tangle-study-redis-exporter.internal.${ACA_DEFAULT_DOMAIN}:9121"]
EOF

cat > /etc/prometheus/scrape/workers.yml <<EOF
scrape_configs:
  - job_name: rust-worker-chat
    static_configs:
      - targets: ["tangle-study-worker-chat.internal.${ACA_DEFAULT_DOMAIN}:9090"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: rust-worker-media
    static_configs:
      - targets: ["tangle-study-worker-media.internal.${ACA_DEFAULT_DOMAIN}:9090"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: rust-worker-location
    static_configs:
      - targets: ["tangle-study-worker-location.internal.${ACA_DEFAULT_DOMAIN}:9090"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}
EOF

exec /bin/prometheus "$@"
