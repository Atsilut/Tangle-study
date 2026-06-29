# Grafana for Azure Container Apps — bundles provisioning from infra/grafana/.
ARG GRAFANA_IMAGE=grafana/grafana:13.0.2
FROM ${GRAFANA_IMAGE}

USER root
COPY infra/grafana/provisioning /usr/share/tangle/grafana-provisioning
COPY infra/azure/monitoring/grafana/grafana-start.sh /grafana-start.sh
RUN chmod +x /grafana-start.sh

USER grafana
ENTRYPOINT ["/grafana-start.sh"]
