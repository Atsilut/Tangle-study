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
| Domain events | `IEventPublisher` / Redis pub/sub | Server-side; subscriber currently logs |
| Async jobs (producer) | `IWorkQueue` / Redis Streams | No consumer yet — see [Queue/QUEUE.md](Queue/QUEUE.md) |

Chat message delivery to clients is **SignalR**, not pub/sub or Streams.

## Chat data flow

```text
POST message → Postgres (persist)
            → SignalR group room:{id} (live UI; backplane when Redis on)
            → pub/sub event (optional cross-service)
            → Stream XADD (future Rust workers)
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
| `RedisRealtimeIntegrationTests` | Testcontainers Redis | `ChatRedisRealtimeIntegrationTests.cs`, `UserNicknameCacheRedisIntegrationTests.cs` |

SignalR scale-out across multiple API replicas is not covered by integration tests (requires real networked hosts). Verify with scaled Compose when needed; see [QUEUE.md](Queue/QUEUE.md) for Streams/worker E2E later.

Run all tests: `docker compose --profile test run --rm test`

## Related docs

- [Domain/Chat/CHAT.md](../Domain/Chat/CHAT.md) — hub contract and client flow
- [Global/Queue/QUEUE.md](Queue/QUEUE.md) — Streams producer and phase 4 workers
