#!/usr/bin/env bash
# Update the production stack on the server: cd /opt/akiron-crm && ./deploy.sh
set -euo pipefail
cd "$(dirname "$0")"

git pull --ff-only
docker compose -f docker-compose.prod.yml up -d --build
docker image prune -f >/dev/null

for _ in $(seq 1 30); do
  if docker exec crm-web wget -qO- http://crm-api:8080/health/ready >/dev/null 2>&1; then
    echo "crm is up"
    exit 0
  fi
  sleep 3
done
echo "crm-api did not become ready; see: docker logs crm-api --tail 100" >&2
exit 1
