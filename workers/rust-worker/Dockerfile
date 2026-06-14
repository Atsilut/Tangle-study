# syntax=docker/dockerfile:1

FROM rust:1.96.0-bookworm AS builder
WORKDIR /build
COPY workers/rust-worker/ .
RUN cargo build --release

FROM debian:bookworm-slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=builder /build/target/release/tangle-worker /app/tangle-worker
EXPOSE 9090
ENTRYPOINT ["/app/tangle-worker"]
