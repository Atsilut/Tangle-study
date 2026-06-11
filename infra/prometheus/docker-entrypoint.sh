#!/bin/sh
set -e

mkdir -p /etc/prometheus/scrape

# Worker services exist only with the Compose `workers` profile. Skip worker scrape
# configs when the profile is off so Prometheus has no `up=0` series and
# WorkerScrapeTargetDown does not false-positive.
if ping -c1 -W2 rust-worker 2>&1 | grep -q '^PING rust-worker'; then
  cp /etc/prometheus/scrape-src/workers.yml /etc/prometheus/scrape/workers.yml
fi

exec /bin/prometheus "$@"
