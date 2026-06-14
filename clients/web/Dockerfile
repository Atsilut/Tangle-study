# Builds the React SPA and serves it with Nginx (edge proxy + static host).
# Build context: repo root (see docker-compose.yml).

FROM node:26.3-bookworm-slim AS build
WORKDIR /app

COPY clients/web/package.json clients/web/package-lock.json ./
RUN npm ci

COPY clients/web/ ./
RUN npm run build

FROM nginx:1.31.1-alpine AS final
COPY infra/nginx/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
