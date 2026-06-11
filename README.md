# Tangle

## Overview

**Tangle** is a personal study project focused on designing and implementing a scalable, real-time distributed system.
The goal of this project is not commercialization, but to explore practical backend architecture, real-time communication, and DevOps patterns in a production-like environment.

This project combines multiple technologies and languages to simulate a modern service architecture, including API servers, real-time messaging, background workers, and monitoring systems.

---

## Objectives

* Build a **real-time backend system** with chat, social features, and location sharing
* Practice **distributed system design**
* Explore **multi-language architecture** (C#, Rust, Go)
* Implement **DevOps workflows** (CI/CD, containerization, observability)
* Understand trade-offs between **performance, complexity, and scalability**

---

## Core Features

* Community platform (posts, comments, nested replies)
* Friend and group management
* Real-time chat (1:1 and group)
* Media sharing (images/videos via posts and chat)
* Memory Map (location-based content visualization)
* Real-time location sharing with safety alerts

---

## Architecture

**Today:** one ASP.NET Core monolith (`services/Api`) plus an optional Rust worker. **Target:** domain-aligned microservices behind a gateway (Phase 9). Full current vs target diagrams: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

### High-Level Structure

```
Clients
 ├─ Web (React)
 └─ Mobile (MAUI)
        │
        ▼
ASP.NET Core API (monolith today → gateway at Phase 9)
        │
 ┌──────┼──────────────┐
 ▼      ▼              ▼
DB    Redis        Queue (Streams)
         │              │
         ▼              ▼
     SignalR       Rust Workers
         │
         ▼
   Real-time Events

Monitoring:
API / Workers → Prometheus → Grafana
```

Api service-layer conventions (one repository per service, peer services for other aggregates): see [services/Api/AGENTS.md](services/Api/AGENTS.md).

Service boundaries and MSA migration plan: [docs/SERVICE_BOUNDARIES.md](docs/SERVICE_BOUNDARIES.md), [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md).

---

## Tech Stack

### Backend

* **ASP.NET Core**

  * Main API server
  * Handles authentication, business logic, and routing

* **PostgreSQL**

  * Primary relational database

---

### Real-Time & Caching

* **Redis**

  * Caching layer
  * Pub/Sub for real-time messaging
  * Streams for lightweight queueing
  * TTL-based storage for location data

* **SignalR**

  * Real-time communication (chat, location updates)

---

### Workers

* **Rust**

  * High-performance background processing
  * Media processing (image/video)
  * Event handling and aggregation
  * Location clustering for Memory Map

---

### DevOps & Tooling

* **Docker**

  * Containerized services

* **GitHub Actions**

  * CI/CD pipeline

* **Go**

  * CLI utilities
  * Load testing tools
  * Custom exporters (if needed)

---

### Monitoring

* **Prometheus**

  * Metrics collection

* **Grafana**

  * Visualization dashboards

---

## Design Decisions

### Monorepo Structure

All services, workers, and tools are managed in a single repository for clarity

```
/services
  /api
/clients
  /web
/workers
/libs
/tools
/infra
```

---

### Technology Separation by Responsibility

* **C# (ASP.NET Core)** → API and business logic
* **Rust** → Performance-critical and asynchronous processing
* **Go** → DevOps tooling
* React -> Frontend

---

### About Message Queue

Redis will be used as:

* Cache
* Real-time messaging backbone
* Lightweight queue (Streams)

To reduce premature complexity while still supporting scalability.

But Kafka or other systems can be introduced later if needed.

---

### Event-Driven Processing

Expected heavy or asynchronous tasks,

```
API → Queue → Rust Worker → Result Storage
```

This improves responsiveness and system scalability.

---

### Monitoring

* Prometheus
* Grafana

---

## Documentation

Central index: [docs/README.md](docs/README.md)

| Doc | Topic |
|-----|-------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Current monolith vs target MSA |
| [docs/SERVICE_BOUNDARIES.md](docs/SERVICE_BOUNDARIES.md) | Domain → microservice mapping |
| [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md) | Extraction order and checklist |

---

## Development Phases

| Phase | Focus | Status |
|-------|-------|--------|
| 1 | Core API (auth, community, friends) | Done |
| 2 | Real-time chat (SignalR) — [CHAT.md](services/Api/Domain/Chat/CHAT.md) | Done |
| 3 | Redis (cache + pub/sub + Streams producer) — [QUEUE.md](services/Api/Global/Queue/QUEUE.md) | Done |
| 4 | Rust workers + media on post/comment/chat — [rust-worker README](workers/rust-worker/README.md) | Done (`chat.message.created` worker handler is an intentional stub; delivery is SignalR) |
| 5 | Monitoring (Prometheus / Grafana) — thin stack in [infra/](infra/) | Done |
| 6 | Web client (React) in [clients/web](clients/web/README.md) — scaffold + UI kit done; features in progress, map UI after Phase 7 | In progress |
| 7 | Location / Memory Map in monolith — [SERVICE_BOUNDARIES.md#location-service](docs/SERVICE_BOUNDARIES.md#location-service) | Planned |
| 8 | MSA prep — cross-service contracts during Phase 7; document events in [QUEUE.md](services/Api/Global/Queue/QUEUE.md) | Planned |
| 9 | MSA migration — follow [MSA_MIGRATION.md](docs/MSA_MIGRATION.md) | Planned |

Phase 9 starts only after Phases 5–7 are complete end-to-end (metrics, location API, React map). MAUI remains optional after the React path works.

---

## Disclaimer

This project is built **for learning purposes only**.

* Not optimized for production use
* May include experimental design decisions
* Focused on exploring architecture rather than delivering a finished product

---

## Future Considerations

* Replace Redis Streams with Kafka (if scaling demands it)
* Add distributed tracing and logs (Grafana Alloy + Loki + Tempo)
* Improve fault tolerance and recovery strategies
* Service mesh (beyond gateway + Compose) if operational needs grow

Service decomposition is Phase 9 — see [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md).

---

## Local development (Docker only)

All .NET build, test, EF, and API runtime run in Docker so nothing writes `.nuget/` or `.dotnet-tools/` into the repo.

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Runtime (API + Postgres + Redis)

```bash
docker compose up --build
```

- API: http://localhost:5000  
- Swagger: http://localhost:5000/api  
- Postgres: `localhost:5433` (user `tangle`, db `tangledb`)
- Redis: `localhost:6379` (enabled on the `api` service for cache, SignalR backplane, pub/sub, Streams producer)

Migrations run automatically on API startup when `ASPNETCORE_ENVIRONMENT=Development`.

Redis details: [services/Api/Global/REDIS.md](services/Api/Global/REDIS.md). Chat hub contract: [services/Api/Domain/Chat/CHAT.md](services/Api/Domain/Chat/CHAT.md).

### Build / EF / other CLI

```bash
chmod +x scripts/docker-dotnet.sh

./scripts/docker-dotnet.sh build Tangle.slnx
./scripts/docker-dotnet.sh ef migrations add MyMigration --project services/Api
./scripts/docker-dotnet.sh ef database update --project services/Api
```

Equivalent without the script:

```bash
docker compose --profile tools run --rm sdk build Tangle.slnx
docker compose --profile tools run --rm sdk ef migrations add MyMigration --project services/Api
```

### Tests (Testcontainers needs Docker socket)

```bash
chmod +x scripts/docker-test.sh

./scripts/docker-test.sh

# Filtered run
./scripts/docker-test.sh test services/Api.Tests/Api.Tests.csproj -c Release --filter "FullyQualifiedName~MetricsIntegrationTests"
```

Equivalent without the script:

```bash
docker compose --profile test run --rm test
```

Use `docker-test.sh` (or `--profile test`), not `docker-dotnet.sh` / `sdk` — integration tests need the host Docker socket mounted by the `test` service.

Integration tests start their own Postgres containers via Testcontainers (using the host Docker engine through the mounted socket). The compose `db` service is not required for tests. Docker Desktop must be running.

Most integration tests run with **Redis disabled** (`ApiWebApplicationFactory` forces `Redis:Enabled=false`). Realtime hub tests use a separate collection with a Testcontainers Redis instance — see `RedisRealtimeIntegrationTestCollection` in [services/Api/Global/REDIS.md](services/Api/Global/REDIS.md).

### Rust worker (optional)

```bash
docker compose --profile workers build rust-worker
docker compose --profile workers up rust-worker
```

Requires Redis (e.g. `docker compose up redis` or the full stack). See [workers/rust-worker/README.md](workers/rust-worker/README.md).

### Monitoring (optional)

Prometheus and Grafana use the Compose `monitoring` profile. See [infra/README.md](infra/README.md).

```bash
docker compose --profile monitoring up --build
docker compose --profile monitoring --profile workers up --build
```

- Grafana: http://localhost:3000 (admin / admin)
- Prometheus: http://localhost:9090
- API metrics: http://localhost:5000/metrics (requires `X-Metrics-Secret` in Docker; see [infra/README.md](infra/README.md))

### Cleanup local SDK artifacts

If `.nuget/` or `.dotnet-tools/` were created in the repo earlier, delete them (they are gitignored):

```bash
rm -rf .nuget .dotnet-tools
```

---

## Summary

Tangle is an exploration of:

* Real-time systems
* Distributed architecture
* Multi-language backend design
* Practical DevOps workflows

The project prioritizes **learning depth and architectural clarity** over completeness or production readiness.
