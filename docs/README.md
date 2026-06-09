# Tangle documentation

Central index for architecture and migration docs. Deep-dive guides for specific subsystems stay in their source folders; this hub links to them.

## Status legend

| Label | Meaning |
|-------|---------|
| **Implemented** | In the repo and runnable |
| **In progress** | Partially built; see notes |
| **Planned** | On the roadmap; not in code yet |

## Architecture and migration

| Doc | Purpose | Status |
|-----|---------|--------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Current modular monolith vs target MSA | Implemented (describes as-built + target) |
| [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) | `Domain/` folder → future microservice mapping | Implemented |
| [MSA_MIGRATION.md](MSA_MIGRATION.md) | Prerequisites, extraction order, checklist | Planned (Phase 9) |

## Development phases

See [README.md](../README.md#development-phases) for the full phased roadmap (Phases 1–9).

| Phase | Focus | Doc pointers |
|-------|-------|--------------|
| 1–3 | Core API, chat, Redis | [AGENTS.md](../services/Api/AGENTS.md), [CHAT.md](../services/Api/Domain/Chat/CHAT.md), [REDIS.md](../services/Api/Global/REDIS.md) |
| 4 | Rust worker + media | [QUEUE.md](../services/Api/Global/Queue/QUEUE.md), [rust-worker README](../workers/rust-worker/README.md) — **Done** |
| 5 | Monitoring (thin Prometheus / Grafana) | [ARCHITECTURE.md](ARCHITECTURE.md), [infra/](../infra/) |
| 6 | Web client (React) — scaffold, then map UI after Phase 7 | [clients/web](../clients/web) |
| 7 | Location / Memory Map (monolith first) | [SERVICE_BOUNDARIES.md#location-service](SERVICE_BOUNDARIES.md#location-service) |
| 8 | MSA prep (during Phase 7) | [SERVICE_BOUNDARIES.md#msa-prep-rules](SERVICE_BOUNDARIES.md#msa-prep-rules) |
| 9 | MSA migration | [MSA_MIGRATION.md](MSA_MIGRATION.md) |

## Subsystem deep-dives

| Doc | Location | Topic |
|-----|----------|-------|
| Api service layer | [services/Api/AGENTS.md](../services/Api/AGENTS.md) | Repository boundaries, DTO naming, HTTP semantics |
| Redis | [services/Api/Global/REDIS.md](../services/Api/Global/REDIS.md) | Cache, SignalR backplane, pub/sub, Streams producer |
| Work queue | [services/Api/Global/Queue/QUEUE.md](../services/Api/Global/Queue/QUEUE.md) | Redis Streams contracts, API → worker flow |
| Chat | [services/Api/Domain/Chat/CHAT.md](../services/Api/Domain/Chat/CHAT.md) | REST routes, SignalR hub contract |
| Rust worker | [workers/rust-worker/README.md](../workers/rust-worker/README.md) | Consumer, retry, DLQ, replay |

## Quick links

- Project overview: [README.md](../README.md)
- Local development (Docker): [README.md#local-development-docker-only](../README.md#local-development-docker-only)
- Api exception conventions: [.cursor/rules/api-exceptions.mdc](../.cursor/rules/api-exceptions.mdc)
