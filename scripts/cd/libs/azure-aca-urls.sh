#!/usr/bin/env bash
# ACA cross-app hostnames for CD (short Container App names within the environment).
#
# HTTP ingress: callers use http://<short-name> (ingress port 80 → container targetPort).
# Do not append :8080, :9090, etc. — that hits the pod IP and times out.
#
# Redis (TCP): use the short name only; clients default to port 6379.

ACA_API_HOST="${ACA_API_HOST:-tangle-study-api}"
ACA_PROMETHEUS_HOST="${ACA_PROMETHEUS_HOST:-tangle-study-prometheus}"
ACA_REDIS_HOST="${ACA_REDIS_HOST:-tangle-study-redis}"

ACA_API_HTTP_URL="${ACA_API_HTTP_URL:-http://${ACA_API_HOST}}"
ACA_PROMETHEUS_HTTP_URL="${ACA_PROMETHEUS_HTTP_URL:-http://${ACA_PROMETHEUS_HOST}}"
ACA_REDIS_URL="${ACA_REDIS_URL:-redis://${ACA_REDIS_HOST}}"
ACA_REDIS_ADDR="${ACA_REDIS_ADDR:-${ACA_REDIS_HOST}}"
