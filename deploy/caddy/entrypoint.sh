#!/bin/sh
# Tant que le wizard n'a pas écrit le marqueur /instance/ready, Caddy ne sert que le wizard.
# Dès qu'il apparaît, Caddy redémarre avec la vraie configuration (adresse publique et HTTPS
# automatique lus dans /instance/instance.env). Une Instance déjà installée démarre directement.
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

set -a
. /instance/instance.env
set +a
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
