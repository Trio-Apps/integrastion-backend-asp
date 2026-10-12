#!/usr/bin/env bash
# Zero-downtime API deploy: rolling update of the two API instances behind nginx.
#
#   ./deploy-api.sh              build web_api + angular, then roll them out
#   ./deploy-api.sh --migrate    also build and run db_migrator first (push has an EF migration)
#   ./deploy-api.sh --no-build   roll out the images already built
#
# For each of web_api_2 then web_api: check the OTHER instance is healthy, take this one out
# of nginx (graceful reload, then let its in-flight requests finish), recreate it, wait until
# healthy, put it back. The other instance serves every request meanwhile.
set -euo pipefail
cd "$(dirname "$0")"

BUILD=1
MIGRATE=0
for arg in "$@"; do
  case "$arg" in
    --no-build) BUILD=0 ;;
    --migrate) MIGRATE=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

INSTANCES=(web_api web_api_2)
SERVERS=nginx/active/api-servers.conf
HEALTH_TIMEOUT=180

log() { echo "[$(date -u +%H:%M:%S)] $*"; }

healthy() {
  docker exec reverse_proxy wget -q -T 3 -O /dev/null "http://$1:8080/health-status" 2>/dev/null
}

wait_healthy() {
  local svc=$1 deadline=$((SECONDS + HEALTH_TIMEOUT))
  until healthy "$svc"; do
    if ((SECONDS > deadline)); then
      log "ERROR: $svc not healthy after ${HEALTH_TIMEOUT}s"
      return 1
    fi
    sleep 2
  done
  log "$svc is healthy"
}

# write_servers [instance-to-take-out]
write_servers() {
  local out=${1:-} svc line
  : > "$SERVERS.new"
  for svc in "${INSTANCES[@]}"; do
    line="server $svc:8080 resolve max_fails=1 fail_timeout=30s"
    [ "$svc" = "$out" ] && line+=" down"
    echo "$line;" >> "$SERVERS.new"
  done
  mv "$SERVERS.new" "$SERVERS"
  docker exec reverse_proxy nginx -t -q
  docker exec reverse_proxy nginx -s reload
}

other_than() { [ "$1" = web_api ] && echo web_api_2 || echo web_api; }

if ((BUILD)); then
  services=(web_api angular)
  ((MIGRATE)) && services+=(db_migrator)
  log "Building ${services[*]}"
  docker compose build "${services[@]}"
fi

if ((MIGRATE)); then
  log "Running db_migrator"
  docker compose run --rm db_migrator
fi

docker compose up -d --no-deps redis >/dev/null

for svc in web_api_2 web_api; do
  other=$(other_than "$svc")
  if ! healthy "$other"; then
    log "ERROR: $other is not healthy, so $svc cannot be taken out. Nothing changed for $svc."
    exit 1
  fi

  log "Taking $svc out of nginx"
  write_servers "$svc"
  sleep 10   # requests already on $svc finish (the container also drains on stop)

  log "Recreating $svc"
  docker compose up -d --no-deps --force-recreate "$svc"
  if ! wait_healthy "$svc"; then
    log "$svc stays out of nginx; $other keeps serving. Fix $svc, then run: $0 --no-build"
    exit 1
  fi

  log "Putting $svc back"
  write_servers
  sleep 5
done

log "Updating the console (angular)"
docker compose up -d --no-deps --force-recreate angular

for svc in "${INSTANCES[@]}"; do
  healthy "$svc" && log "$svc healthy" || log "WARNING: $svc not healthy"
done
log "Done."
