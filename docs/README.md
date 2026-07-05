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
| [ARCHITECTURE.md](ARCHITECTURE.md) | Gateway-centric as-built + remaining Azure gaps | Implemented (local Compose complete) |
| [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) | Domain → microservice mapping | Implemented |
| [MSA_MIGRATION.md](MSA_MIGRATION.md) | Branch workflow, extraction order, steps 1–7 status | In progress (Compose done; Azure cutover pending) |

## Development phases

See [README.md](../README.md#development-phases) for the full phased roadmap (Phases 1–9).

| Phase | Focus | Doc pointers |
|-------|-------|--------------|
| 1–3 | Core API, chat, Redis | [AGENTS.md](AGENTS.md), [CHAT.md](../services/Chat/CHAT.md), [REDIS.md](REDIS.md) |
| 4 | Rust worker + media | [MEDIA.md](../services/Media/MEDIA.md), [QUEUE.md](QUEUE.md), [workers README](../workers/README.md) — **Done** |
| 5 | Monitoring (thin Prometheus / Grafana) | [ARCHITECTURE.md](ARCHITECTURE.md), [infra/](../infra/) — **Done** |
| 6 | Web client (React) — backend parity through media | [clients/web](../clients/web/README.md) — **Done** |
| 7 | Location / Memory Map (extracted to location-service) | [LOCATION.md](../services/Location/LOCATION.md) — **Done** |
| 8 | MSA prep (contracts, QUEUE rows) | [GROUP.md](../services/Group/GROUP.md), [EVENTS.md](EVENTS.md), [SERVICE_BOUNDARIES.md#msa-prep-rules](SERVICE_BOUNDARIES.md#msa-prep-rules) — **Done** |
| 9 | MSA migration — all domains + gateway/users in Compose; Azure cutover pending | [MSA_MIGRATION.md](MSA_MIGRATION.md) — **In progress** |

## Subsystem deep-dives

| Doc | Location | Topic |
|-----|----------|-------|
| Gateway | [services/Gateway/GATEWAY.md](../services/Gateway/GATEWAY.md) | YARP routing and JWT validation |
| Users | [services/Users/USERS.md](../services/Users/USERS.md) | Identity, login, JWT issuance |
| Service layer conventions | [AGENTS.md](AGENTS.md) | Repository boundaries, DTO naming, HTTP semantics |
| Redis | [REDIS.md](REDIS.md) | Cache, SignalR backplane, pub/sub, Streams producer |
| Work queue | [QUEUE.md](QUEUE.md) | Redis Streams contracts, service → worker flow |
| Domain events | [EVENTS.md](EVENTS.md) | Redis pub/sub event contracts |
| Group | [services/Group/GROUP.md](../services/Group/GROUP.md) | Groups, boards, membership, internal access contracts |
| Community | [services/Community/COMMUNITY.md](../services/Community/COMMUNITY.md) | Posts, comments, group-board posts |
| Social | [services/Social/SOCIAL.md](../services/Social/SOCIAL.md) | Friendships and user blocks |
| Chat | [services/Chat/CHAT.md](../services/Chat/CHAT.md) | REST routes, SignalR hub contract |
| Media | [services/Media/MEDIA.md](../services/Media/MEDIA.md) | Upload flow, limits, worker callback |
| Location | [services/Location/LOCATION.md](../services/Location/LOCATION.md) | Memory Map, live sharing, safety alerts, clustering |
| Rust workers | [workers/README.md](../workers/README.md) | Consumer, retry, DLQ, replay |
| Web client | [clients/web/README.md](../clients/web/README.md) | React SPA, Nginx proxy, feature slices |
| Monitoring | [infra/README.md](../infra/README.md) | Prometheus, Grafana, alerts, scrape auth |
| Stack E2E | [Stack.Tests](../services/Stack.Tests/) | Cross-service harness (`Category=Harness`) |

## Quick links

- Project overview: [README.md](../README.md)
- Local development (Docker): [README.md#local-development-docker-only](../README.md#local-development-docker-only)
- Api exception conventions: [.cursor/rules/api-exceptions.mdc](../.cursor/rules/api-exceptions.mdc)
