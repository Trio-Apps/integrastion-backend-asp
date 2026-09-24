#!/usr/bin/env bash
# Zero-downtime API deploy (blue/green through nginx).
#
#   ./deploy-api.sh              build web_api + angular, then swap them in
#   ./deploy-api.sh --migrate    also build and run db_migrator first (push has an EF migration)
#   ./deploy-api.sh --no-build   swap in the images already built
#
# Flow: start web_api_green on the new image -> point nginx at it -> recreate web_api ->
# point nginx back -> remove web_api_green. nginx reloads gracefully, so no request is
# refused at any point; both API containers run together only for the swap (~1 min).
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

COMPOSE=(docker compose --profile green)
ACTIVE=nginx/active/api-upstream.conf
HEALTH_TIMEOUT=180

log() { echo "[$(date -u +%H:%M:%S)] $*"; }

wait_healthy() {
  local svc=$1 deadline=$((SECONDS + HEALTH_TIMEOUT))
  until docker exec reverse_proxy wget -q -T 3 -O /dev/null "http://$svc:8080/health-status" 2>/dev/null; do
    if ((SECONDS > deadline)); then
      log "ERROR: $svc not healthy after ${HEALTH_TIMEOUT}s"
      return 1
    fi
    sleep 2
  done
  log "$svc is healthy"
}

route_to() {
  local svc=$1
  echo "set \$backend_upstream $svc:8080;" > "$ACTIVE.new"
  mv "$ACTIVE.new" "$ACTIVE"
  docker exec reverse_proxy nginx -t -q
  docker exec reverse_proxy nginx -s reload
  log "nginx now routes the API to $svc"
  # Let requests already on the old backend finish before it is touched.
  sleep 10
}

if ((BUILD)); then
  services=(web_api angular)
  ((MIGRATE)) && services+=(db_migrator)
  log "Building ${services[*]}"
  "${COMPOSE[@]}" build "${services[@]}"
fi

if ((MIGRATE)); then
  log "Running db_migrator"
  "${COMPOSE[@]}" run --rm db_migrator
fi

log "Starting web_api_green on the new image"
"${COMPOSE[@]}" up -d --no-deps --force-recreate web_api_green
if ! wait_healthy web_api_green; then
  "${COMPOSE[@]}" rm -sf web_api_green
  log "Aborted; web_api (old version) kept serving"
  exit 1
fi
route_to web_api_green

log "Recreating web_api on the new image"
"${COMPOSE[@]}" up -d --no-deps --force-recreate web_api
if ! wait_healthy web_api; then
  log "ERROR: new web_api unhealthy; web_api_green (new version) keeps serving — investigate"
  exit 1
fi
route_to web_api

log "Removing web_api_green"
"${COMPOSE[@]}" stop -t 30 web_api_green
"${COMPOSE[@]}" rm -f web_api_green

log "Updating the console (angular)"
"${COMPOSE[@]}" up -d --no-deps --force-recreate angular

log "Done. API: $(docker exec reverse_proxy wget -q -O - http://web_api:8080/health-status | head -c 40)"
