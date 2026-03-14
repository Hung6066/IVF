#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
#  deploy-wazuh.sh — Deploy Wazuh Manager stack + update Caddy config
#  Chạy trên VPS1 (manager node): bash deploy-wazuh.sh
# ═══════════════════════════════════════════════════════════════════
set -euo pipefail
REPO_DIR="${REPO_DIR:-/opt/ivf}"

echo "=== [1/4] Git pull latest code ==="
cd "$REPO_DIR"
git pull origin main

echo "=== [2/4] Setup Wazuh SSL certs + Docker secrets ==="
if [[ ! -f "$REPO_DIR/docker/wazuh/config/wazuh_indexer_ssl_certs/root-ca.pem" ]]; then
  bash "$REPO_DIR/docker/wazuh/setup-certs.sh"
else
  echo "  Certs already exist — bỏ qua (xóa thư mục certs để tạo lại)"
fi

echo "=== [3/4] Deploy Wazuh stack ==="
# Ensure ivf-monitoring network exists (created by monitoring stack)
if ! docker network ls --filter name=ivf-monitoring --format '{{.Name}}' | grep -q ivf-monitoring; then
  echo "  Creating ivf-monitoring overlay network..."
  docker network create --driver overlay --attachable ivf-monitoring || true
fi

docker stack deploy \
  -c "$REPO_DIR/docker/wazuh/docker-compose.yml" \
  wazuh

echo "  Waiting for Wazuh services to start (60s)..."
sleep 60
docker stack ps wazuh --no-trunc | head -20

echo "=== [4/4] Update ivf Caddy stack (caddyfile_v12) ==="
# Docker configs are immutable; create new v12 config
if ! docker config ls | grep -q caddyfile_v12; then
  docker config create caddyfile_v12 "$REPO_DIR/Caddyfile"
  echo "  Created Docker config: caddyfile_v12"
else
  echo "  caddyfile_v12 already exists"
fi

docker stack deploy \
  -c "$REPO_DIR/docker-compose.stack.yml" \
  ivf

echo ""
echo "=== ✓ Deploy xong ==="
echo "  Wazuh Dashboard: https://natra.site/wazuh/"
echo "  Credentials: monitor / <monitoring password>"
echo ""
echo "  Kiểm tra Wazuh:"
echo "    docker service ls | grep wazuh"
echo "    docker stack ps wazuh"
