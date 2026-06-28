# syntax=docker/dockerfile:1

ARG RUST_IMAGE=rust:bookworm
ARG DEBIAN_IMAGE=debian:bookworm-slim

FROM ${RUST_IMAGE} AS builder
WORKDIR /build

# Cache dependency compilation separately from application source.
COPY workers/rust-worker/Cargo.toml workers/rust-worker/Cargo.lock ./
RUN mkdir -p src \
    && printf 'fn main() {}\n' > src/main.rs \
    && cargo build --release \
    && rm -rf src

COPY workers/rust-worker/src ./src
RUN touch src/main.rs && cargo build --release

FROM ${DEBIAN_IMAGE}
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=builder /build/target/release/tangle-worker /app/tangle-worker
EXPOSE 9090
ENTRYPOINT ["/app/tangle-worker"]
