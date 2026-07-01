#!/bin/sh
# Generate ACA scrape configs using short app names (see scripts/cd/libs/azure-aca-urls.sh).
set -e

: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"

mkdir -p /etc/prometheus/scrape

cat > /etc/prometheus/scrape/aca.yml <<EOF
scrape_configs:
  - job_name: api
    static_configs:
      - targets: ["tangle-study-api"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: postgres
    static_configs:
      - targets: ["tangle-study-postgres-exporter"]

  - job_name: redis
    static_configs:
      - targets: ["tangle-study-redis-exporter"]
EOF

cat > /etc/prometheus/scrape/workers.yml <<EOF
scrape_configs:
  - job_name: rust-worker-chat
    static_configs:
      - targets: ["tangle-study-worker-chat"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: rust-worker-media
    static_configs:
      - targets: ["tangle-study-worker-media"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: rust-worker-location
    static_configs:
      - targets: ["tangle-study-worker-location"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}
EOF

exec /bin/prometheus "$@"
