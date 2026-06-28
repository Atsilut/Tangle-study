# Prometheus for Azure Container Apps — bundles recording rules + ACA scrape entrypoint.
ARG PROMETHEUS_IMAGE=prom/prometheus:v3.12.0
FROM ${PROMETHEUS_IMAGE}

USER root
COPY infra/prometheus/recording_rules.yml /etc/prometheus/recording_rules.yml
COPY infra/azure/monitoring/prometheus/prometheus-base.yml /etc/prometheus/prometheus.yml
COPY infra/azure/monitoring/prometheus/prometheus-start.sh /prometheus-start.sh
RUN chmod +x /prometheus-start.sh

USER nobody
ENTRYPOINT ["/prometheus-start.sh"]
CMD ["--config.file=/etc/prometheus/prometheus.yml", "--storage.tsdb.path=/prometheus", "--web.enable-lifecycle"]
