# Work queue (Redis Streams)

Producer-only foundation for phase 4 Rust workers. Chat and the API work without consumers.

## Streams

| Stream key (`WorkQueueStreams`) | Payload | Enqueued when |
|--------------------------------|---------|---------------|
| `chat.message.created` | `ChatMessageCreatedJob` | After a chat message is persisted (`ChatMessageService`) |
| `media.uploaded` | `MediaUploadedJob` | After upload is confirmed in blob storage (`MediaService.CompleteUploadAsync`) |

`MediaUploadedJob` fields: `mediaAssetId`, `intendedContext`, `kind`, `mimeType`, `originalObjectKey`, `originalSizeBytes`, `targetMaxBytes`. `targetMaxBytes` is the per-file **storage** cap from [`media-limits.yml`](../../media-limits.yml) (not the ingress cap).

Full Redis stream name: `{WorkQueueStreamPrefix}{streamKey}` (default prefix `tangle:queue:`).

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

Rust worker crate: [`workers/rust-worker`](../../../../workers/rust-worker/README.md).

```
API → XADD stream → Rust worker (XREADGROUP) → process → result storage
```

Workers should:

- Use a consumer group per stream (e.g. `tangle-workers`)
- `XACK` after successful processing
- Treat Postgres as source of truth; stream jobs are notifications / async work, not chat delivery

The Rust worker implements `XGROUP CREATE` (mkstream), `XREADGROUP`, handler dispatch, `XACK`, PEL retry via `XPENDING`/`XCLAIM` with exponential backoff and jitter, and DLQ publish for `chat.message.created`. Replay: `tangle-worker replay`. Metrics are still planned.

## Relation to pub/sub and SignalR

| Mechanism | Purpose |
|-----------|---------|
| SignalR + backplane | Live chat to connected clients |
| Redis pub/sub (`IEventPublisher`) | Cross-service events (optional subscribers) |
| Redis Streams (`IWorkQueue`) | Durable async jobs for workers |

Do not use Streams as the client realtime channel.
