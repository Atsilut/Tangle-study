# Work queue (Redis Streams)

Producer-only foundation for phase 4 Rust workers. Chat and the API work without consumers. Media upload flow: [services/Media/MEDIA.md](../../../Media/MEDIA.md).

Pub/sub event contracts (separate mechanism): [Events/EVENTS.md](../Events/EVENTS.md).

## Schema versioning

Every job payload includes `schemaVersion` (starting at `1`). Serialized as camelCase JSON inside the stream entry `payload` field.

- **Producers** set `SchemaVersion = 1` on contract records in [`WorkQueueContracts.cs`](WorkQueueContracts.cs).
- **Consumers** (Rust workers) should ignore unknown JSON fields; reject unsupported major versions when breaking changes land.
- **Bump** `schemaVersion` when removing or renaming fields; optional new fields may omit a bump if old consumers ignore them.

Pub/sub events follow the same convention — see [EVENTS.md](../Events/EVENTS.md).

## Streams

| Stream key (`WorkQueueStreams`) | Payload | `schemaVersion` | Enqueued when |
|--------------------------------|---------|-----------------|---------------|
| `chat.message.created` | `ChatMessageCreatedJob` | `1` | After a chat message is persisted (`ChatMessageService`) |
| `media.uploaded` | `MediaUploadedJob` | `1` | After upload is confirmed in blob storage (`MediaService.CompleteUploadAsync`) |
| `location.cluster` | `LocationClusterJob` | `1` | When clustered pins are requested or pins change (`LocationClusterService` / `MapPinService`) |

`ChatMessageCreatedJob` fields: `messageId`, `chatRoomId`, `senderUserId`, `body`, `sentAt`, `schemaVersion`.

`LocationClusterJob` fields: `minLatitude`, `maxLatitude`, `minLongitude`, `maxLongitude`, `zoom` (2–4), `schemaVersion`. Worker fetches pin coordinates via `GET /internal/location/cluster-points`, clusters them, and stores results with `PUT /internal/location/clusters` (Redis cache, 5 min TTL). Public read: `GET /api/location/clusters`.

`MediaUploadedJob` fields: `mediaAssetId`, `intendedContext`, `kind`, `mimeType`, `originalObjectKey`, `originalSizeBytes`, `targetMaxBytes`, `schemaVersion`. `targetMaxBytes` is the per-file **storage** cap from [`media-config.yml`](../../Media/media-config.yml) (not the ingress cap).

Full Redis stream name: `{WorkQueueStreamPrefix}{streamKey}` (default prefix `tangle:queue:` from each service's `*-config.yml` or `Redis__WorkQueueStreamPrefix` env).

## Configuration

`Redis:Enabled` must be `true` and `Redis:ConnectionString` set for real enqueue. Otherwise `NoOpWorkQueue` is registered and enqueue is a no-op (tests, single-instance dev).

```json
"Redis": {
  "Enabled": true,
  "ConnectionString": "redis:6379",
  "WorkQueueStreamPrefix": "tangle:queue:"
}
```

## Phase 4 consumer

Rust worker crates: [`workers/README.md`](../../../../workers/README.md).

```
API → XADD stream → Rust worker (XREADGROUP) → process → result storage
```

Workers should:

- Use a consumer group per stream (e.g. `tangle-study-workers`)
- `XACK` after successful processing
- Treat Postgres as source of truth; stream jobs are notifications / async work, not chat delivery

The Rust workers implement `XGROUP CREATE` (mkstream), `XREADGROUP`, handler dispatch, `XACK`, PEL retry via `XPENDING`/`XCLAIM` with exponential backoff and jitter, and DLQ publish. Replay: `worker-chat replay`, `worker-media replay`, or `worker-location replay`.

## Metrics (Phase 5)

| Component | Endpoint | Metrics |
|-----------|----------|---------|
| API | `GET /metrics` | `http_requests_*` (prometheus-net); `tangle_workqueue_enqueue_total{stream}` and `tangle_workqueue_enqueue_failed_total{stream}` when Redis is enabled |
| rust-worker | `GET /metrics` on `WORKER_METRICS_PORT` (default `9090`) | `tangle_worker_jobs_processed_total`, `tangle_worker_pending_messages`, `tangle_worker_dlq_length`, `tangle_worker_callback_requests_total` |

Prometheus scrape config: [`infra/prometheus/prometheus.yml`](../../../../infra/prometheus/prometheus.yml). Grafana dashboard: [infra/README.md](../../../../infra/README.md).

## End-to-end harness (`media.uploaded`)

Automated cross-language smoke (API + Azurite + `rust-worker-media`, no fakes):

```bash
./scripts/ci/run-media-harness.sh
```

Runs opt-in xUnit tests (`Category=Harness`) against a live Compose stack. Validates upload-init → blob PUT → complete → worker ffmpeg → `PATCH /internal/media/{id}/processed` → asset `Ready`. Fast integration tests in `Api.Tests` still use `FakeMediaStorage` and a simulated callback.

## Relation to pub/sub and SignalR

| Mechanism | Purpose |
|-----------|---------|
| SignalR + backplane | Live chat to connected clients |
| Redis pub/sub (`IEventPublisher`) | Cross-service events (optional subscribers) — [Events/EVENTS.md](../Events/EVENTS.md) |
| Redis Streams (`IWorkQueue`) | Durable async jobs for workers |

Do not use Streams as the client realtime channel.

## Location jobs

Live position sharing uses Redis string keys and SignalR (see [REDIS.md](../REDIS.md)). Only map **clustering** uses Streams today:

| Stream | Worker service | Callback |
|--------|----------------|----------|
| `location.cluster` | `rust-worker-location` | `PUT /internal/location/clusters` |

Start: `docker compose --profile workers up -d rust-worker-location`. Details: [LOCATION.md](../../Domain/Location/LOCATION.md).
