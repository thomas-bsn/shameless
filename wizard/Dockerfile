# Le front du wizard est construit depuis le contexte nommé « web » (voir compose.override.yaml
# et .github/workflows/publier.yml), comme l'image proxy.
FROM node:24-alpine AS front
WORKDIR /web
COPY --from=web package.json package-lock.json ./
RUN npm ci
COPY --from=web . .
RUN npm run build:wizard && mv dist-wizard/wizard.html dist-wizard/index.html

# Le serveur n'a aucune dépendance d'exécution : Node 24 exécute le TypeScript tel quel.
FROM node:24-alpine
WORKDIR /app
COPY src ./src
COPY entrypoint.sh /entrypoint.sh
COPY --from=front /web/dist-wizard ./public
# /instance est le volume partagé qui reçoit instance.env puis le marqueur ready.
RUN mkdir /instance && chmod +x /entrypoint.sh
ENV NODE_ENV=production
EXPOSE 8080
# Démarre en root pour préparer le volume, puis lance le serveur en utilisateur « node ».
ENTRYPOINT ["/entrypoint.sh"]
