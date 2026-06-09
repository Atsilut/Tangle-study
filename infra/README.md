# Monitoring infrastructure

Thin Prometheus + Grafana stack for Phase 5 observability. Scrapes metrics from the API and Rust workers; does not run by default.

## Layout

```
infra/
  prometheus/prometheus.yml          # scrape targets
  grafana/provisioning/
    datasources/prometheus.yml       # Grafana → Prometheus datasource
    dashboards/
      dashboard.yml                  # file provider config
      tangle-overview.json           # provisioned dashboard
```

## Start

Monitoring uses the Compose `monitoring` profile. The API must be running for the `api` scrape target; workers need the `workers` profile.

```bash
# API + Prometheus + Grafana
docker compose --profile monitoring up --build

# Include Rust workers (chat + media scrape targets)
docker compose --profile monitoring --profile workers up --build
```

## URLs (host)

| Service | URL | Notes |
|---------|-----|-------|
| Grafana | http://localhost:3000 | Login: `admin` / `admin` (dev default) |
| Prometheus | http://localhost:9090 | Targets page shows scrape health |
| API metrics | http://localhost:5000/metrics | Raw Prometheus text (after API instrumentation) |

## Scrape targets

Configured in [prometheus/prometheus.yml](prometheus/prometheus.yml):

| Job | Target | Metrics |
|-----|--------|---------|
| `api` | `api:8080` | HTTP request rate, latency, status codes; work queue enqueue counter |
| `rust-worker-chat` | `rust-worker:9090` | Job processing, pending queue, DLQ length |
| `rust-worker-media` | `rust-worker-media:9090` | Same |

Worker targets appear as **DOWN** until the `workers` profile is active.

Optional host debug ports (not mapped by default): exec into a worker container and `wget -qO- http://127.0.0.1:9090/metrics`.

## Dashboard

**Tangle Overview** (`uid: tangle-overview`) is auto-provisioned under the **Tangle** folder in Grafana:

- API request rate and latency (p50 / p95)
- API 5xx error rate
- Worker jobs processed by outcome
- Worker pending messages and DLQ length
- Work queue enqueue rate from the API

Panels use metrics from `prometheus-net` (API) and custom `tangle_*` counters/gauges (workers).

## Related docs

- [README.md](../README.md#development-phases) — phased roadmap
- [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) — system overview
- [services/Api/Global/Queue/QUEUE.md](../services/Api/Global/Queue/QUEUE.md) — async job contracts
