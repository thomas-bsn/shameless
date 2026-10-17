# Le front est construit depuis le contexte nommé « web » (voir compose.yaml).
FROM node:24-alpine AS build
WORKDIR /web
COPY --from=web package.json package-lock.json ./
RUN npm ci
COPY --from=web . .
RUN npm run build

FROM caddy:2.10-alpine
COPY Caddyfile Caddyfile.forgeyard Caddyfile.setup /etc/caddy/
COPY entrypoint.sh /entrypoint.sh
COPY --from=build /web/dist /srv
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
