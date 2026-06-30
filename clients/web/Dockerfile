# syntax=docker/dockerfile:1

ARG NODE_IMAGE=node:bookworm-slim
ARG NGINX_IMAGE=nginx:alpine

FROM ${NODE_IMAGE} AS build
WORKDIR /app

COPY clients/web/package.json clients/web/package-lock.json ./
RUN npm ci

COPY clients/web/ ./
RUN npm run build

FROM ${NGINX_IMAGE} AS final
RUN apk add --no-cache gettext
COPY infra/nginx/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY infra/nginx/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]