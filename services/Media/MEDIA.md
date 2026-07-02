# Media API

Direct-to-storage uploads (Azure Blob / Azurite locally), async processing via Redis Streams + [worker-media](../../../workers/rust-worker/README.md), and attachment by ID on posts, comments, and chat messages.

Stream contract: [Api QUEUE.md](../Api/Global/Queue/QUEUE.md). Size limits: [`media-limits.yml`](media-limits.yml).

---

## REST

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `GET` | `/api/media/{id}` | Bearer | Metadata + `processingStatus` (`Pending`, `Ready`, `Failed`) |
| `GET` | `/api/media/{id}/content` | Anonymous* | Stream processed bytes; range requests supported |
| `POST` | `/api/media/upload-init` | Bearer | Returns presigned upload URL + new `mediaAssetId` |
| `POST` | `/api/media/{id}/complete` | Bearer | Confirms blob upload; enqueues `media.uploaded` job |
| `DELETE` | `/api/media/{id}` | Bearer | Delete **unlinked** asset owned by caller |
| `PATCH` | `/internal/media/{id}/processed` | Worker secret | Worker callback after ffmpeg processing |

\* Content is public when the asset is linked to a post or comment the caller could read; chat attachments require auth.

Swagger: `http://localhost:8080/api` (via nginx) or direct `http://localhost:5000/api` on the monolith during migration.

---

## Upload flow (client)

1. `POST /api/media/upload-init` with `{ intendedContext, kind, fileName, contentType, sizeBytes }`.
2. `PUT` bytes to the returned `uploadUrl` (Azurite in dev: proxied via Nginx `/devstoreaccount1`).
3. `POST /api/media/{id}/complete` — verifies blob, enqueues worker job, returns asset (usually `Pending`).
4. Poll `GET /api/media/{id}` until `processingStatus` is `Ready` (or handle `Failed`).
5. Attach `mediaAssetId` / `mediaAssetIds` when creating a post, comment, or chat message.

Web client implementation: [clients/web/src/features/media](../../../../clients/web/src/features/media).

---

## Async processing

```text
CompleteUpload → XADD media.uploaded → rust-worker-media
  → download original → ffmpeg transcode/thumbnail → upload processed blob
  → PATCH /internal/media/{id}/processed → status Ready
```

Worker config: `WORKER_STREAM_KEY=media.uploaded`, `API_BASE_URL` (media-service base, e.g. `http://media:8080` in Compose), `WORKER_CALLBACK_SECRET` (must match `Media:WorkerCallbackSecret` on media-service).

Harness smoke: `./scripts/ci/run-media-harness.sh` — see [QUEUE.md](../../Global/Queue/QUEUE.md).

---

## Limits

Per-context caps in [`media-limits.yml`](../../media-limits.yml). Ingress allows `IngressMultiplier` × storage cap so oversized uploads can be rejected or transcoded down. Enforcement is in `MediaLimitPolicy` at upload-init and attach time.

---

## Storage

- **Local dev:** Azurite (`docker compose up` includes `azurite`; connection string `UseDevelopmentStorage=true`).
- **Docker:** `Media:ConnectionString` in `appsettings.Docker.json`.
- **Tests:** `FakeMediaStorage` — no real blob I/O in fast integration tests.

Blob keys and CDN URLs stay inside the media domain; other aggregates store `mediaAssetId` references only ([SERVICE_BOUNDARIES.md](../../../../docs/SERVICE_BOUNDARIES.md#media-service)).
