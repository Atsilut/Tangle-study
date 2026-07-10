#!/bin/sh
set -e

mkdir -p /etc/prometheus/scrape

# Worker services exist only with the Compose `workers` profile. Skip worker scrape
# configs when the profile is off so Prometheus has no `up=0` series and
# WorkerScrapeTargetDown does not false-positive.
if ping -c1 -W2 rust-worker-chat 2>&1 | grep -q '^PING rust-worker-chat' \
  || ping -c1 -W2 rust-worker-media 2>&1 | grep -q '^PING rust-worker-media' \
  || ping -c1 -W2 rust-worker-location 2>&1 | grep -q '^PING rust-worker-location'; then
  cp /etc/prometheus/scrape-src/workers.yml /etc/prometheus/scrape/workers.yml
fi

exec /bin/prometheus "$@"
