# syntax=docker/dockerfile:1
# Chat + location worker (no ffmpeg).

ARG RUST_IMAGE=rust:bookworm
ARG DEBIAN_IMAGE=debian:bookworm-slim

FROM ${RUST_IMAGE} AS builder
WORKDIR /build

COPY workers/Cargo.toml workers/rust-toolchain.toml ./
COPY workers/crates/worker-core/Cargo.toml crates/worker-core/
COPY workers/crates/worker-media/Cargo.toml crates/worker-media/
COPY workers/rust-worker/Cargo.toml rust-worker/

RUN mkdir -p crates/worker-core/src crates/worker-media/src rust-worker/src \
    && printf 'pub fn dummy() {}\n' > crates/worker-core/src/lib.rs \
    && printf 'fn main() {}\n' > crates/worker-media/src/main.rs \
    && printf 'fn main() {}\n' > rust-worker/src/main.rs

RUN cargo generate-lockfile \
    && cargo build --release -p tangle-worker

COPY workers/crates/worker-core/src ./crates/worker-core/src
COPY workers/rust-worker/src ./rust-worker/src
RUN touch crates/worker-core/src/lib.rs rust-worker/src/main.rs \
    && cargo build --release -p tangle-worker

FROM ${DEBIAN_IMAGE}
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=builder /build/target/release/tangle-worker /app/tangle-worker
EXPOSE 9090
ENTRYPOINT ["/app/tangle-worker"]
