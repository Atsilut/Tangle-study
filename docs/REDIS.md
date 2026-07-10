# Redis in Tangle

Redis is an **optional infrastructure dependency**. Chat and REST APIs work with `Redis:Enabled=false` (in-memory cache, in-process SignalR, no-op event publisher and work queue).

Enable Redis for Docker Compose domain services, multi-instance deployments, and integration tests that exercise the Redis stack.

## Configuration

| Setting | Purpose |
|---------|---------|
| `Redis:Enabled` | Master switch (`false` in appsettings default and test host) |
| `Redis:ConnectionString` | e.g. `redis:6379` in Compose, `localhost:6379` locally |
| `Redis:InstanceName` | Prefix for `IDistributedCache` keys |
| `Redis:SignalRChannelPrefix` | SignalR backplane channel prefix |
| `Redis:WorkQueueStreamPrefix` | Prefix for Redis Stream keys |

Compose services set `Redis__Enabled=true` and depend on the `redis` service. The `test` profile keeps `Redis__Enabled=false` for most integration tests.

## What Redis is used for

| Feature | Implementation | User-visible? |
|---------|----------------|---------------|
| Nickname cache | `IDistributedCache` + `NicknameCacheService` | Faster reads; PG remains source of truth |
| SignalR scale-out | `AddStackExchangeRedis` backplane | Live chat across multiple replicas |
| Domain events | `IEventPublisher` / Redis pub/sub | Server-side; see [EVENTS.md](EVENTS.md) |
| Async jobs (producer) | `IWorkQueue` / Redis Streams | Consumed by [workers](../workers/README.md) — see [QUEUE.md](QUEUE.md) |
| Live location positions | `LiveLocationRedisStore` (direct Redis / `IDistributedCache`) | Group-scoped TTL keys; refreshed on position update |
| Map cluster cache | location-service `LocationClusterService` (direct Redis) | Interim clustering results (5 min TTL) |
| Safety alert dedupe | `IDistributedCache` | Per-session stale-alert keys |

Chat message delivery to clients is **SignalR**, not pub/sub or Streams.

## Location keys (live sharing)

| Key pattern | TTL | Purpose |
|-------------|-----|---------|
| `{InstanceName}location:live:{groupId}:{userId}` | 5 min (reset on update) | Live position snapshot JSON |
| `location:safety:stale:{sessionId}` | 12 h | Dedupe stale-position alerts until next position push |

Cluster cache keys are written by **location-service** after `rust-worker-location` callbacks — see [LOCATION.md](../services/Location/LOCATION.md).

## Location data flow

```text
POST/PATCH session position → Postgres (LocationSession)
                           → Redis location:live:{groupId}:{userId}
                           → SignalR session:{sessionId} (LocationUpdated)
                           → SignalR group-alerts:{groupId} (SafetyAlertRaised, when rules fire)

GET active/members → Postgres sessions + Redis live snapshots
```

Live sharing does **not** use Streams or pub/sub for client delivery.

## Chat data flow

```text
POST message → Postgres (persist)
            → SignalR group room:{id} (live UI; backplane when Redis on)
            → pub/sub event (optional cross-service)
            → Stream XADD (rust-worker)
```

History and late join: `GET /api/chat/rooms/{roomId}/messages` (Postgres). WebSocket only delivers messages after `JoinRoom`.

## Local development

```bash
docker compose up --build   # gateway + domain services + db + redis
```

Inspect streams (example):

```bash
docker compose exec redis redis-cli XLEN tangle:queue:chat.message.created
```

## Tests

| Collection | Redis | Scope |
|------------|-------|--------|
| `IntegrationTests` (default) | Off | REST / matrix tests; in-process SignalR tests |
| `RedisRealtimeIntegrationTests` | Testcontainers Redis | Chat hub, location hub, nickname cache |

SignalR scale-out across multiple replicas is not covered by integration tests (requires real networked hosts). Verify with scaled Compose when needed; see [QUEUE.md](QUEUE.md) for Streams/worker E2E.

Run all tests: `docker compose --profile test run --rm test`

## Related docs

- [services/Chat/CHAT.md](../services/Chat/CHAT.md) — hub contract and client flow
- [services/Location/LOCATION.md](../services/Location/LOCATION.md) — live sharing, Redis keys, SignalR events
- [QUEUE.md](QUEUE.md) — Streams producer and workers
- [EVENTS.md](EVENTS.md) — pub/sub event contracts
