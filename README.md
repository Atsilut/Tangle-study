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

### High-Level Structure

```
Clients
 ├─ Web (React)
 └─ Mobile (MAUI)
        │
        ▼
ASP.NET Core API (Gateway)
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

## Development Phases

1. Core API (auth, community, friends)
2. Real-time chat (SignalR) — see [services/Api/Domain/Chat/CHAT.md](services/Api/Domain/Chat/CHAT.md) for hub contract
3. Redis integration (cache + pub/sub + Streams producer) — see [services/Api/Global/Queue/QUEUE.md](services/Api/Global/Queue/QUEUE.md)
4. Rust workers (media processing; consume Streams)
5. Location features (Memory Map)
6. Monitoring setup
7. Optional client (MAUI)

---

## Disclaimer

This project is built **for learning purposes only**.

* Not optimized for production use
* May include experimental design decisions
* Focused on exploring architecture rather than delivering a finished product

---

## Future Considerations

* Replace Redis Streams with Kafka (if scaling demands it)
* Introduce service decomposition (microservices)
* Add distributed tracing (e.g., OpenTelemetry)
* Improve fault tolerance and recovery strategies

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
docker compose --profile test run --rm test
```

Integration tests start their own Postgres containers via Testcontainers (using the host Docker engine through the mounted socket). The compose `db` service is not required for tests. Docker Desktop must be running.

Most integration tests run with **Redis disabled** (`ApiWebApplicationFactory` forces `Redis:Enabled=false`). Realtime hub tests use a separate collection with a Testcontainers Redis instance — see `RedisRealtimeIntegrationTestCollection` in [services/Api/Global/REDIS.md](services/Api/Global/REDIS.md).

### Rust worker (optional)

```bash
docker compose --profile workers build rust-worker
docker compose --profile workers up rust-worker
```

Requires Redis (e.g. `docker compose up redis` or the full stack). See [workers/rust-worker/README.md](workers/rust-worker/README.md).

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
