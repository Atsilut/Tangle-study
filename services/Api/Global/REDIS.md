# Redis in Tangle

Redis is an **optional infrastructure dependency**. Chat and the REST API work with `Redis:Enabled=false` (in-memory cache, in-process SignalR, no-op event publisher and work queue).

Enable Redis for Docker Compose `api`, multi-instance deployments, and integration tests that exercise the Redis stack.

## Configuration

| Setting | Purpose |
|---------|---------|
| `Redis:Enabled` | Master switch (`false` in appsettings default and test host) |
| `Redis:ConnectionString` | e.g. `redis:6379` in Compose, `localhost:6379` locally |
| `Redis:InstanceName` | Prefix for `IDistributedCache` keys |
| `Redis:SignalRChannelPrefix` | SignalR backplane channel prefix |
| `Redis:WorkQueueStreamPrefix` | Prefix for Redis Stream keys |

Compose `api` sets `Redis__Enabled=true` and depends on the `redis` service. The `test` profile keeps `Redis__Enabled=false` for most integration tests.

## What Redis is used for

| Feature | Implementation | User-visible? |
|---------|----------------|---------------|
| Nickname cache | `IDistributedCache` + `NicknameCacheService` | Faster reads; PG remains source of truth |
| SignalR scale-out | `AddStackExchangeRedis` backplane | Live chat across multiple API replicas |
| Domain events | `IEventPublisher` / Redis pub/sub | Server-side; subscriber currently logs — see [Events/EVENTS.md](Events/EVENTS.md) |
| Async jobs (producer) | `IWorkQueue` / Redis Streams | Consumed by [rust-worker](../../../workers/rust-worker/README.md) — see [Queue/QUEUE.md](Queue/QUEUE.md) |
| Live location positions | `LiveLocationRedisStore` (direct Redis / `IDistributedCache`) | Group-scoped TTL keys; refreshed on position update |
| Map cluster cache | `LocationClusterService` (direct Redis) | Interim clustering results (5 min TTL) |
| Safety alert dedupe | `IDistributedCache` | Per-session stale-alert keys |

Chat message delivery to clients is **SignalR**, not pub/sub or Streams.

## Location keys (live sharing)

| Key pattern | TTL | Purpose |
|-------------|-----|---------|
| `{InstanceName}location:live:{groupId}:{userId}` | 5 min (reset on update) | Live position snapshot JSON |
| `location:safety:stale:{sessionId}` | 12 h | Dedupe stale-position alerts until next position push |

Cluster cache keys are written by the API after `rust-worker-location` callbacks — see [LOCATION.md](../Domain/Location/LOCATION.md).

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
docker compose up --build   # api + db + redis (Redis enabled on api)
```

Inspect streams (example):

```bash
docker compose exec redis redis-cli XLEN tangle:queue:chat.message.created
```

## Tests

| Collection | Redis | Scope |
|------------|-------|--------|
| `IntegrationTests` (default) | Off | REST / matrix tests; `ChatInProcessRealtimeIntegrationTests.cs` |
| `RedisRealtimeIntegrationTests` | Testcontainers Redis | Chat hub, location hub, nickname cache |

SignalR scale-out across multiple API replicas is not covered by integration tests (requires real networked hosts). Verify with scaled Compose when needed; see [QUEUE.md](Queue/QUEUE.md) for Streams/worker E2E later.

Run all tests: `docker compose --profile test run --rm test`

## Related docs

- [Domain/Chat/CHAT.md](../Domain/Chat/CHAT.md) — hub contract and client flow
- [Domain/Location/LOCATION.md](../Domain/Location/LOCATION.md) — live sharing, Redis keys, SignalR events
- [Global/Queue/QUEUE.md](Queue/QUEUE.md) — Streams producer and workers
- [Global/Events/EVENTS.md](Events/EVENTS.md) — pub/sub event contracts
