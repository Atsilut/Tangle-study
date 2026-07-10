#!/bin/sh
# Generate ACA scrape configs using short app names (MSA services + workers + exporters).
set -e

: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"

mkdir -p /etc/prometheus/scrape

cat > /etc/prometheus/scrape/aca.yml <<EOF
scrape_configs:
  - job_name: gateway
    static_configs:
      - targets: ["tangle-study-gateway"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: users
    static_configs:
      - targets: ["tangle-study-users"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: media
    static_configs:
      - targets: ["tangle-study-media"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: chat
    static_configs:
      - targets: ["tangle-study-chat"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: location
    static_configs:
      - targets: ["tangle-study-location"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: community
    static_configs:
      - targets: ["tangle-study-community"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: group
    static_configs:
      - targets: ["tangle-study-group"]
    metrics_path: /metrics
    http_headers:
      X-Metrics-Secret:
        values:
          - ${METRICS_SCRAPE_SECRET}

  - job_name: social
    static_configs:
      - targets: ["tangle-study-social"]
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
