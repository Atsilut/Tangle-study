# Media API

Direct-to-storage uploads (Azure Blob / Azurite locally), async processing via Redis Streams + [worker-media](../../../workers/README.md), and attachment by ID on posts, comments, and chat messages.

Stream contract: [QUEUE.md](../../docs/QUEUE.md). Consistency: [CONSISTENCY.md](../../docs/CONSISTENCY.md). Service config: [`media-config.yml`](media-config.yml).

---

## State machine

```text
PendingUpload  --complete (blob exists)-->  Processing  --worker success-->  Ready
     |                                          |
     |                                          +--worker failure / timeout-->  Failed
     +--(never completed)--> stays PendingUpload
```

| Status | Client meaning |
|--------|----------------|
| `PendingUpload` | Presigned upload issued; blob not confirmed |
| `Processing` | Upload confirmed; worker job durable via outbox (eventual) |
| `Ready` | Processed blob available; safe to link |
| `Failed` | Terminal; includes `processing_timeout` after sweeper max age |

`POST /complete` returns **`Processing`** (not Pending). Poll `GET /api/media/{id}` until `Ready` or `Failed`. Stuck `Processing` is re-enqueued by `MediaProcessingRecovery`, then marked `Failed` with reason `processing_timeout` if still stuck.

`ReportProcessedAsync` is idempotent for terminal states (Ready→Ready / Failed→Failed no-ops; cross-terminal transitions rejected). Workers are at-least-once.

---

## REST

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `GET` | `/api/media/{id}` | Bearer | Metadata + `processingStatus` |
| `GET` | `/api/media/{id}/content` | Anonymous* | Stream processed bytes; range requests supported |
| `POST` | `/api/media/upload-init` | Bearer | Returns presigned upload URL + new `mediaAssetId` |
| `POST` | `/api/media/{id}/complete` | Bearer | Confirms blob; writes outbox `media.uploaded`; returns `Processing` |
| `DELETE` | `/api/media/{id}` | Bearer | Delete **unlinked** asset owned by caller |
| `PATCH` | `/internal/media/{id}/processed` | Worker secret | Worker callback after ffmpeg processing |

\* Content is public when the asset is linked to a post or comment the caller could read; chat attachments require auth.

Swagger: `http://localhost:8080/api` (via nginx → gateway).

---

## Upload flow (client)

1. `POST /api/media/upload-init` with `{ intendedContext, kind, fileName, contentType, sizeBytes }`.
2. `PUT` bytes to the returned `uploadUrl` (Azurite in dev: proxied via Nginx `/devstoreaccount1`).
3. `POST /api/media/{id}/complete` — verifies blob, persists `Processing` + outbox job, returns asset with status **`Processing`**.
4. Poll `GET /api/media/{id}` until `processingStatus` is `Ready` (or handle `Failed`).
5. Attach `mediaAssetId` / `mediaAssetIds` when creating a post, comment, or chat message.

Web client implementation: [clients/web/src/features/media](../../../../clients/web/src/features/media).

---

## Async processing

```text
CompleteUpload → outbox → dispatcher XADD media.uploaded → rust-worker-media
  → download original → ffmpeg transcode/thumbnail → upload processed blob
  → PATCH /internal/media/{id}/processed → status Ready
```

If the process crashes after saving `Processing` but before Redis XADD, the outbox dispatcher retries. If the worker never completes, recovery re-enqueues then eventually marks `Failed` (`processing_timeout`).

Worker config: `WORKER_STREAM_KEY=media.uploaded`, `API_BASE_URL` (media-service base, e.g. `http://media:8080` in Compose), `WORKER_CALLBACK_SECRET` (must match `Media:WorkerCallbackSecret` on media-service).

Harness smoke: `./scripts/ci/run-media-harness.sh` — see [QUEUE.md](../../docs/QUEUE.md).

---

## Limits

Per-context caps in [`media-config.yml`](media-config.yml). Ingress allows `IngressMultiplier` × storage cap so oversized uploads can be rejected or transcoded down. Enforcement is in `MediaLimitPolicy` at upload-init and attach time.

---

## Storage

- **Local dev:** Azurite (`docker compose up` includes `azurite`; connection string `UseDevelopmentStorage=true`).
- **Docker:** `Media:ConnectionString` in `appsettings.Docker.json`.
- **Tests:** `FakeMediaStorage` — no real blob I/O in fast integration tests.

Blob keys and CDN URLs stay inside the media domain; other aggregates store `mediaAssetId` references only ([SERVICE_BOUNDARIES.md](../../docs/SERVICE_BOUNDARIES.md#media-service)).
