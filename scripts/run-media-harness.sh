#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

COMPOSE="docker compose -f docker-compose.yml -f docker-compose.harness.yml --profile harness"

cleanup() {
  $COMPOSE down -v
}
trap cleanup EXIT

$COMPOSE build api rust-worker-media harness
$COMPOSE up -d --wait
$COMPOSE run --rm harness
