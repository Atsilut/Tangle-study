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
docker compose --profile workers build rust-worker
docker compose --profile workers up rust-worker
```

With the default stack (`docker compose up`), start Redis and the API first so jobs can be enqueued; the worker only needs Redis.

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `REDIS_URL` | `redis://127.0.0.1:6379` | Redis connection URL |
| `WORKER_STREAM_PREFIX` | `tangle:queue:` | Prefix for stream keys (matches API `Redis:WorkQueueStreamPrefix`) |
| `WORKER_STREAM_KEY` | `chat.message.created` | Stream key suffix |
| `WORKER_CONSUMER_GROUP` | `tangle-workers` | Redis consumer group name |
| `WORKER_CONSUMER_NAME` | `tangle-worker-{pid}` | Consumer name within the group |
| `WORKER_BLOCK_MS` | `5000` | `XREADGROUP` block timeout (ms) |
| `WORKER_BATCH_COUNT` | `10` | Max entries per read |
| `WORKER_MAX_ATTEMPTS` | `5` | Max deliveries before terminal ack (DLQ in next milestone) |
| `WORKER_RETRY_BASE_MS` | `1000` | Base backoff for first retry (ms) |
| `WORKER_RETRY_MAX_MS` | `60000` | Backoff cap (ms) |
| `WORKER_RETRY_JITTER_PCT` | `0.1` | Jitter fraction added to backoff (0–1) |
| `WORKER_DLQ_STREAM_SUFFIX` | `.dlq` | DLQ stream suffix |
| `WORKER_LOG_JSON` | `false` | Emit JSON logs |
| `RUST_LOG` | `info` | `tracing` filter (e.g. `tangle_worker=debug`) |

## Consumer behavior

- Creates the consumer group on startup (`XGROUP CREATE … MKSTREAM`, idempotent).
- Reads with `XREADGROUP` (`>`), batch size and block timeout from env.
- Decodes `type` + `payload` fields (same shape as API `RedisStreamWorkQueue`).
- Runs the handler, then `XACK` on success.
- Handler failures leave the message in the pending entries list; after `WORKER_RETRY_BASE_MS` × 2^(attempt−1) (capped, with jitter) the worker `XCLAIM`s and retries.
- After `WORKER_MAX_ATTEMPTS` deliveries, the message is acknowledged and logged as exhausted (DLQ publish in next milestone).
- Malformed messages are acked after a warning so they do not block the group.

## Layout

```
src/
  main.rs       # entrypoint, redis health check
  config.rs     # env configuration
  consumer.rs   # XREADGROUP loop and XACK
  message.rs    # stream field decoding
  job.rs        # payload types (API contract)
  handlers/     # per-job-type processors
  retry.rs      # backoff helpers
  dlq.rs        # dead-letter publishing
  telemetry.rs  # logging setup
```

## Tests

```bash
cd workers/rust-worker
cargo test
```
