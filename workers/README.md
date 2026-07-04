# Tangle Rust workers

Redis Streams consumers for async jobs from .NET services (`IWorkQueue`). See [services/Api/Global/Queue/QUEUE.md](../services/Api/Global/Queue/QUEUE.md).

## Workspace layout

| Crate / binary | Stream | Callback target |
|----------------|--------|-----------------|
| `crates/worker-core` | — | Shared consumer loop, DLQ, retry, metrics |
| `crates/worker-media` (`worker-media`) | `media.uploaded` | `http://media:8080` |
| `crates/worker-chat` (`worker-chat`) | `chat.message.created` | `http://chat:8080` (stub today) |
| `crates/worker-location` (`worker-location`) | `location.cluster` | `http://location:8080` |

Each domain worker is a **separate binary and Docker image**, matching MSA service boundaries.

## Configuration

Environment variables are documented in one place:

| File | Purpose |
|------|---------|
| [`config/workers.yml`](config/workers.yml) | Canonical reference — all env vars, defaults, per-worker requirements |
| [`config/compose-workers.yml`](config/compose-workers.yml) | Compose dev values (mirror of `x-worker-*` anchors in root `docker-compose.yml`) |
| [`config/local.env.example`](config/local.env.example) | Template for host-side `cargo run` |

Rust code loads env at runtime (`worker-core` `StreamConfig` / `CallbackConfig`); the YAML files are the shared source of truth for ops. When adding a new env var, update `workers.yml`, sync `compose-workers.yml` and the `x-worker-*` block in `docker-compose.yml`, and `worker-core/src/config.rs`.

## Build

```bash
./scripts/ci/build-workers-release.sh          # cargo test + release binaries
./scripts/ci/build-worker-images.sh            # all three runtime images
```

## Run locally

```bash
cd workers
# Optional: copy config/local.env.example → local.env and source it
export REDIS_URL=redis://127.0.0.1:6379
export WORKER_STREAM_PREFIX=tangle:queue:

cargo run -p worker-chat    # stub; see workers.yml for optional vars
cargo run -p worker-media   # needs AZURE_STORAGE_CONNECTION_STRING, WORKER_CALLBACK_SECRET
cargo run -p worker-location
```

## Docker Compose

```bash
docker compose --profile workers up --build rust-worker-chat rust-worker-media rust-worker-location
```

| Compose service | Binary | `API_BASE_URL` |
|-----------------|--------|----------------|
| `rust-worker-chat` | `worker-chat` | `http://chat:8080` |
| `rust-worker-media` | `worker-media` | `http://media:8080` |
| `rust-worker-location` | `worker-location` | `http://location:8080` |

Stream keys are fixed per crate (`config.rs`); Compose env comes from `config/compose-workers.yml`.

## DLQ replay

```bash
docker compose --profile workers run --rm rust-worker-chat replay
docker compose --profile workers run --rm rust-worker-media replay
docker compose --profile workers run --rm rust-worker-location replay
```
