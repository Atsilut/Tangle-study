# Architecture

Tangle is a learning project that simulates a distributed system. Today it runs as a **modular monolith** with one optional background worker, an optional **React web client** behind an Nginx edge, and an optional **Prometheus / Grafana** monitoring profile. The target is **domain-aligned microservices** (Phase 9) after Phases 6–7 complete: React web client and location in the monolith.

Service-layer conventions inside the monolith: [services/Api/AGENTS.md](../services/Api/AGENTS.md).

---

## Current state (as-built)

One ASP.NET Core deployable (`services/Api`) owns all business domains. PostgreSQL is the source of truth for every aggregate. Redis is optional (cache, SignalR backplane, pub/sub, Streams producer).

```mermaid
flowchart TB
  Web["React web client (optional, clients/web)"]
  Nginx["Nginx edge (optional, infra/nginx)"]
  API["ASP.NET Core Api monolith"]
  PG[(PostgreSQL)]
  Redis[(Redis)]
  Worker["rust-worker (optional)"]
  Prom["Prometheus (optional)"]
  Graf["Grafana (optional)"]

  Web -->|"/api, /hubs"| Nginx
  Nginx --> API
  API --> PG
  API --> Redis
  Redis --> Worker
  Worker -.->|"future: write results"| PG
  API -->|"GET /metrics, /health"| Prom
  Worker -->|"GET /metrics"| Prom
  Prom --> Graf
```

The web client talks to the API same-origin through Nginx (the API has no CORS): in dev the Vite dev server proxies `/api` and `/hubs` to Nginx; in prod Nginx serves the built SPA and proxies the same paths. See [clients/web/README.md](../clients/web/README.md).

### In-process boundaries

Domains live under `services/Api/Domain/`. Each aggregate service owns one repository; cross-aggregate access goes through peer services, not foreign repositories. Orchestrators coordinate multi-step workflows without repositories.

This is **modular monolith** design — clear boundaries inside one process, one database schema (`AppDbContext`), one deployment unit.

### Async boundary

The only cross-process work path today:

```text
API (ChatMessageService) → Redis Stream XADD → rust-worker XREADGROUP → handler → XACK
```

See [QUEUE.md](../services/Api/Global/Queue/QUEUE.md) and [rust-worker README](../workers/rust-worker/README.md). The chat handler is currently a stub; worker infra (consumer group, retry, DLQ, replay) is implemented.

### Realtime

Chat uses SignalR (`/hubs/chat`) in-process. With Redis enabled, the SignalR backplane allows multiple API replicas. Client delivery is **not** pub/sub or Streams — see [REDIS.md](../services/Api/Global/REDIS.md) and [CHAT.md](../services/Api/Domain/Chat/CHAT.md).

### Observability

Prometheus + Grafana stack under [`infra/`](../infra/) with provisioned alerts, recording rules, and infra exporters.

**Metrics**

| Source | Endpoint | Key metrics |
|--------|----------|-------------|
| API | `GET /metrics` | `http_requests_received_total{code, controller}`, `http_request_duration_seconds`, `aspnetcore_healthcheck_status`, `tangle_workqueue_enqueue_total`, `tangle_workqueue_enqueue_failed_total` |
| Workers | `GET /metrics` on `WORKER_METRICS_PORT` | `tangle_worker_jobs_processed_total`, `tangle_worker_pending_messages`, `tangle_worker_dlq_length`, `tangle_worker_callback_requests_total` |
| Postgres / Redis | sidecar exporters | `pg_stat_activity_count`, `redis_memory_used_bytes`, etc. |

**Health** — `GET /health` returns plain-text `Healthy` / `Unhealthy` for PostgreSQL and Redis. Compose healthcheck and Grafana `ApiDependencyUnhealthy` alert use this signal; per-check gauges are on `/metrics`.

**Metrics scrape auth** — Docker enables `Metrics:RequireScrapeSecret` with `X-Metrics-Secret`; Prometheus scrape config sends the header. Local dev keeps `/metrics` open.

**Alerts** — Grafana provisioned rules (folder: Tangle) for HTTP 4xx/5xx, latency p95 SLO, scrape health, worker DLQ/backlog, and infra limits. UI-only (no Alertmanager). Runbook: [infra/README.md#alerting](../infra/README.md#alerting).

**Tracing and logs** — not implemented. Planned later via Grafana Alloy + Loki + Tempo.

Start with `docker compose --profile monitoring up` (add `--profile workers` for worker scrape targets). Details: [infra/README.md](../infra/README.md).

### Docker Compose (default)

| Service | Role |
|---------|------|
| `api` | Monolith |
| `db` | PostgreSQL |
| `redis` | Cache, backplane, pub/sub, Streams |
| `nginx` | Optional (`--profile web`) — edge proxy + SPA host |
| `rust-worker` | Optional (`--profile workers`) |
| `prometheus` | Optional (`--profile monitoring`) |
| `grafana` | Optional (`--profile monitoring`) |
| `postgres-exporter` | Optional (`--profile monitoring`) |
| `redis-exporter` | Optional (`--profile monitoring`) |

---

## Target state (MSA)

After Phase 9, the monolith decomposes into domain-aligned microservices behind a **gateway or BFF**. The gateway handles routing, JWT validation, and response composition — not domain business logic.

```mermaid
flowchart TB
  Clients["Clients"]
  GW["Gateway / BFF"]
  Users["users-service"]
  Posts["posts-service"]
  Comments["comments-service"]
  Groups["groups-service"]
  Friendships["friendships-service"]
  UserBlocks["user-blocks-service"]
  Chat["chat-service"]
  Media["media-service"]
  Location["location-service"]
  PG[(PostgreSQL per service)]
  Redis[(Redis)]
  Worker["rust-worker"]

  Clients --> GW
  GW --> Users
  GW --> Posts
  GW --> Comments
  GW --> Groups
  GW --> Friendships
  GW --> UserBlocks
  GW --> Chat
  GW --> Media
  GW --> Location
  Users --> PG
  Posts --> PG
  Comments --> PG
  Groups --> PG
  Friendships --> PG
  UserBlocks --> PG
  Chat --> PG
  Media --> PG
  Location --> PG
  Chat --> Redis
  Location --> Redis
  Media --> Redis
  Redis --> Worker
```

Service mapping detail: [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md). Extraction plan: [MSA_MIGRATION.md](MSA_MIGRATION.md).

### Database strategy

**End goal:** database-per-service (each service owns its schema and migrations).

**Interim option (learning project):** shared PostgreSQL instance with **schema-per-service** before splitting physical databases. Avoid cross-schema FKs; use IDs and service calls/events instead.

### Rust worker

The worker stays a **separate process**, not a microservice per handler. Handlers grow by domain (`media.*`, `location.cluster`, etc.). Extract to a dedicated service only if CPU isolation or independent scaling demands it.

---

## Communication patterns

| Pattern | Use when | Today | Target |
|---------|----------|-------|--------|
| In-process service call | Same deployable, strong consistency | `PostService` → `UserService` | Replaced by HTTP/gRPC client |
| Sync HTTP / gRPC | Cross-service reads, auth checks, enrichment | N/A (monolith) | Primary sync boundary |
| Redis pub/sub | Fire-and-forget domain events | `IEventPublisher` | Cross-service notifications |
| Redis Streams | Durable async work | `IWorkQueue` → rust-worker | Same; may add Kafka later |
| SignalR | Client push (chat, location) | In-process hub | Owned by chat / location services |

Do **not** use Streams as the client realtime channel. SignalR (or WebSocket) delivers live updates; Streams handle background processing.

---

## Monorepo layout

```
/services
  /Api          ← monolith today; shrinks during Phase 9
/clients/web    ← React client (Phase 6, in progress); MAUI optional later
/workers
  /rust-worker  ← async job processor
/libs           ← planned shared contracts
/tools          ← planned Go CLI / load testing
/infra          ← Prometheus / Grafana, Nginx edge ([infra/README.md](../infra/README.md))
  /nginx        ← edge reverse proxy (serves SPA + proxies /api, /hubs)
/docs           ← architecture and migration docs (this folder)
```

Solution file (`Tangle.slnx`) currently includes only `Api` and `Api.Tests`. Workers and infra are folders outside the .NET solution.

---

## What is not MSA today

- README diagram label "Gateway" is **aspirational** — there is no separate gateway service yet.
- No inter-service HTTP boundaries beyond API → Redis → worker.
- No distributed tracing or log aggregation (Grafana Alloy + Loki + Tempo planned in Future Considerations).
- No service mesh.

These are intentional. The monolith keeps deploy-and-run simple while Phases 5–7 land; MSA extraction (Phase 9) starts only after that vertical slice works. See [README.md](../README.md#development-phases).
