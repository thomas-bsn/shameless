#!/bin/sh
# Le volume /instance peut avoir été créé par un autre conteneur (propriétaire root) : on le
# rend inscriptible puis on abandonne les droits root avant de lancer le serveur.
set -eu
chown node:node /instance
exec su -s /bin/sh node -c 'exec node /app/src/main.ts'
