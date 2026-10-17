#!/bin/sh
# Tant que le wizard n'a pas écrit le marqueur /instance/ready, Caddy ne sert que le wizard.
# Dès qu'il apparaît, Caddy redémarre avec la vraie configuration (adresse publique et HTTPS
# automatique lus dans /instance/instance.env), puis relit ce fichier s'il change. Une Instance
# déjà installée démarre directement.
set -eu

if [ ! -f /instance/ready ]; then
  caddy run --config /etc/caddy/Caddyfile.setup --adapter caddyfile &
  pid=$!
  trap 'kill "$pid" 2>/dev/null; exit 0' TERM INT
  while [ ! -f /instance/ready ]; do
    sleep 1 &
    wait $! || true
  done
  trap - TERM INT
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
fi

# Une configuration corrigée par le wizard après un échec (« Réessayer ») réécrit instance.env :
# Caddy la relit à chaud, sans redémarrer le conteneur.
charger() { set -a; . /instance/instance.env; set +a; }
empreinte() { cksum < /instance/instance.env; }

charger
derniere=$(empreinte)
caddy run --config /etc/caddy/Caddyfile --adapter caddyfile &
pid=$!
trap 'kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; exit 0' TERM INT

while kill -0 "$pid" 2>/dev/null; do
  sleep 2 &
  wait $! || true
  actuelle=$(empreinte)
  if [ "$actuelle" != "$derniere" ]; then
    derniere=$actuelle
    echo "instance.env a changé : rechargement de la configuration."
    (charger && caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile) || echo "Rechargement impossible : redémarrez le proxy (docker compose restart proxy)."
  fi
done
wait "$pid"
