# syntax=docker/dockerfile:1
# Builds the React SPA and serves it with Nginx (edge proxy + static host).
# Build context: repo root (see docker-compose.yml).

ARG NODE_IMAGE=node:bookworm-slim
ARG NGINX_IMAGE=nginx:alpine

FROM ${NODE_IMAGE} AS build
WORKDIR /app

COPY clients/web/package.json clients/web/package-lock.json ./
RUN npm ci

COPY clients/web/ ./
RUN npm run build

FROM ${NGINX_IMAGE} AS final

# Local Compose: default nginx.conf (includes Azurite proxy).
# Azure / production: --build-arg NGINX_CONF=nginx.production.conf
ARG NGINX_CONF=nginx.conf
COPY infra/nginx/${NGINX_CONF} /etc/nginx/conf.d/default.conf
COPY infra/nginx/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]
