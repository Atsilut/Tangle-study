# Tangle Rust worker

Consumes durable jobs from Redis Streams produced by the API (`IWorkQueue`). See [services/Api/Global/Queue/QUEUE.md](../../services/Api/Global/Queue/QUEUE.md) for stream contracts.

## Run locally (host)

Requires Rust 1.96+ (`rust-toolchain.toml`). Redis must be reachable.

```bash
cd workers/rust-worker
export REDIS_URL=redis://127.0.0.1:6379
export WORKER_STREAM_PREFIX=tangle:queue:
cargo run
```

## Run with Docker Compose

From the repository root:

```bash
docker compose --profile workers build rust-worker rust-worker-media
docker compose --profile workers up rust-worker rust-worker-media
```

- `rust-worker` — consumes `chat.message.created` (stub handler today)
- `rust-worker-media` — consumes `media.uploaded` (download → encode → upload → API callback)

With the default stack (`docker compose up`), start Redis, the API, and Azurite first so jobs can be enqueued. The chat worker only needs Redis; the media worker also needs the API, Azure/Azurite, and a matching `WORKER_CALLBACK_SECRET` (see `Media__WorkerCallbackSecret` on the API).

## Configuration

### Queue and consumer

| Variable | Default | Description |
|----------|---------|-------------|
| `REDIS_URL` | `redis://127.0.0.1:6379` | Redis connection URL |
| `WORKER_STREAM_PREFIX` | `tangle:queue:` | Prefix for stream keys (matches API `Redis:WorkQueueStreamPrefix`) |
| `WORKER_STREAM_KEY` | `chat.message.created` | Stream key suffix; one worker process per key |
| `WORKER_CONSUMER_GROUP` | `tangle-study-workers` | Redis consumer group name |
| `WORKER_CONSUMER_NAME` | `tangle-worker-{hostname}-{pid}` | Consumer name within the group |
| `WORKER_BLOCK_MS` | `5000` | `XREADGROUP` block timeout (ms) |
| `WORKER_BATCH_COUNT` | `10` | Max entries per read |
| `WORKER_MAX_ATTEMPTS` | `5` | Max deliveries before publishing to the DLQ stream and acking the source message |
| `WORKER_RETRY_BASE_MS` | `1000` | Base backoff for first retry (ms) |
| `WORKER_RETRY_MAX_MS` | `60000` | Backoff cap (ms) |
| `WORKER_RETRY_JITTER_PCT` | `0.1` | Jitter fraction added to backoff (0–1) |
| `WORKER_DLQ_STREAM_SUFFIX` | `.dlq` | DLQ stream suffix |
| `WORKER_REPLAY_COUNT` | `10` | Max DLQ entries to replay per run |
| `WORKER_REPLAY_DRY_RUN` | `false` | Log replay actions without enqueuing |
| `WORKER_REPLAY_DELETE` | `true` | Remove DLQ entry after successful replay |
| `WORKER_LOG_JSON` | `false` | Emit JSON logs |
| `WORKER_METRICS_PORT` | `9090` | Prometheus scrape HTTP port (`GET /metrics`; consumer mode only) |
| `RUST_LOG` | `info` | `tracing` filter (e.g. `tangle_worker=debug`) |

### Media worker (`WORKER_STREAM_KEY=media.uploaded`)

Required in consumer mode (validated at startup). Not required for `cargo run -- replay`.

| Variable | Default | Description |
|----------|---------|-------------|
| `API_BASE_URL` | `http://127.0.0.1:5000` | Base URL for `PATCH /internal/media/{id}/processed` |
| `WORKER_CALLBACK_SECRET` | *(empty)* | Shared secret sent as `X-Worker-Callback-Secret`; must match API `Media__WorkerCallbackSecret` |
| `AZURE_STORAGE_CONNECTION_STRING` | *(empty)* | Azure Blob connection string (Azurite in local compose) |
| `MEDIA_CONTAINER_NAME` | `tangle-media` | Blob container for originals and processed output |
| `WORKER_CALLBACK_TIMEOUT_MS` | `30000` | HTTP request timeout for API callbacks (ms) |
| `WORKER_CALLBACK_CONNECT_TIMEOUT_MS` | `10000` | HTTP connect timeout for API callbacks (ms) |
| `WORKER_CALLBACK_MAX_RETRIES` | `3` | Callback attempts before the job is left in the PEL for full retry |
| `WORKER_CALLBACK_RETRY_BASE_MS` | `500` | Base backoff between callback retries (ms) |

## Consumer behavior

- Creates the consumer group on startup (`XGROUP CREATE … MKSTREAM`, idempotent).
- Reads with `XREADGROUP` (`>`), batch size and block timeout from env.
- Decodes `type` + `payload` fields (same shape as API `RedisStreamWorkQueue`).
- Runs the handler, then `XACK` on success.
- Handler failures leave the message in the pending entries list; after `WORKER_RETRY_BASE_MS` × 2^(attempt−1) (capped, with jitter) the worker `XCLAIM`s and retries.
- After `WORKER_MAX_ATTEMPTS` deliveries, the worker publishes a record to the DLQ stream (`{stream}.dlq`) and acks the source message.
- Malformed messages are acked after a warning so they do not block the group.

### DLQ replay

Re-drive failed jobs from the DLQ back onto the main work stream:

```bash
cd workers/rust-worker
cargo run -- replay
```

Docker Compose:

```bash
docker compose --profile workers run --rm rust-worker replay
```

Optional env: `WORKER_REPLAY_COUNT`, `WORKER_REPLAY_DRY_RUN=true`, `WORKER_REPLAY_DELETE=false`.

## Metrics

Consumer mode starts a Prometheus HTTP listener on `WORKER_METRICS_PORT` (default `9090`). Replay mode does not expose metrics.

| Metric | Type | Description |
|--------|------|-------------|
| `tangle_worker_jobs_processed_total{stream_key,outcome}` | Counter | Jobs handled; `outcome` is `success`, `failure` (retryable error, stays in PEL), `malformed`, or `dlq` (terminal exhaustion) |
| `tangle_worker_pending_messages{stream_key}` | Gauge | Pending entries in the consumer group (`XPENDING`) |
| `tangle_worker_dlq_length{stream_key}` | Gauge | Length of the DLQ stream (`XLEN`) |
| `tangle_worker_callback_requests_total{code}` | Counter | Final API callback outcome per attempt chain (`204`, HTTP status code, or `transport_error`) |

The callback counter records one increment per callback invocation (not per HTTP retry). Grafana alerts on `WorkerCallbackHigh5xxRate` when 5xx callback responses occur. `WorkerHighDlqRate` alerts on terminal `dlq` outcomes only — `failure` counts retryable handler errors and is not alerted.

Scraped by Prometheus when the Compose `monitoring` profile is active. See [infra/README.md](../../infra/README.md).

## Logging

`telemetry.rs` configures a `tracing` subscriber (plain or JSON via `WORKER_LOG_JSON`). Filter with `RUST_LOG`. Distributed tracing is not implemented — planned later with Grafana Alloy + Loki + Tempo.

## Layout

```
src/
  main.rs                          # entrypoint; consumer or replay mode
  config.rs                        # env configuration and startup validation
  consumer.rs                      # XREADGROUP loop, retry, ack, DLQ handoff
  message.rs                       # stream envelope decode; malformed-entry detection
  job.rs                           # payload types (API contract)
  handlers/
    mod.rs                         # dispatch by WORKER_STREAM_KEY
    chat_message_created.rs        # chat.message.created (stub)
    media_uploaded.rs              # media.uploaded pipeline orchestration
  processing.rs                    # ffmpeg encode stage machine
  encode_plan.rs                   # ffmpeg argument planning
  probe.rs                         # ffprobe metadata and feasibility checks
  storage.rs                       # Azure Blob download/upload
  api_callback.rs                  # PATCH callback to API (success/failure)
  retry.rs                         # PEL backoff and eligibility
  dlq.rs                           # dead-letter publish and replay
  metrics.rs                       # Prometheus counters/gauges + HTTP listener
  telemetry.rs                     # tracing subscriber setup
```

## Tests

Unit tests (decode, encode planning, callback URLs). Also run via `./scripts/run-all-tests.sh` from the repo root.

```bash
cd workers/rust-worker
cargo test
```

Or Docker-only (no host Rust / MSVC):

```bash
docker run --rm -v "$(pwd):/src" -w /src/workers/rust-worker rust:bookworm cargo test
```

### Automated media pipeline harness (API + worker)

Full end-to-end path through Redis, Azurite, ffmpeg, and the API callback — not covered by `cargo test` alone:

```bash
./scripts/run-media-harness.sh
```

From the repository root. Brings up `db`, `redis`, `azurite`, `api`, `rust-worker-media`, then runs `Category=Harness` tests in `services/Api.Tests`. Requires Docker.

Against an already-running stack on the host:

```bash
export TANGLE_HARNESS_API_BASE_URL=http://127.0.0.1:5000
docker compose --profile test run --rm test test services/Api.Tests/Api.Tests.csproj -c Release --filter "Category=Harness"
```
