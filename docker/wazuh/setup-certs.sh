#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  Wazuh Manager — Khởi tạo SSL certificates và Docker secrets
#  Chạy 1 lần trên vps1 (manager node) trước khi deploy stack
#
#  Sử dụng:
#    ssh root@45.134.226.56 "bash -s" < docker/wazuh/setup-certs.sh
# ═══════════════════════════════════════════════════════════
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CERTS_DIR="$SCRIPT_DIR/config/wazuh_indexer_ssl_certs"

echo "=== [1/4] Tạo thư mục certs ==="
mkdir -p "$CERTS_DIR"
cd "$CERTS_DIR"

echo "=== [2/4] Generate CA và certificates ==="
# Root CA
if [[ ! -f root-ca.key ]]; then
  openssl genrsa -out root-ca.key 4096
  openssl req -new -x509 -days 3650 -key root-ca.key -out root-ca.pem \
    -subj "/C=US/O=Wazuh/OU=Wazuh/CN=Wazuh Root CA"
fi

# Helper function tạo cert
gen_cert() {
  local name=$1
  local cn=$2
  openssl genrsa -out "${name}-key.pem" 2048
  openssl req -new -key "${name}-key.pem" -out "${name}.csr" \
    -subj "/C=US/O=Wazuh/OU=Wazuh/CN=${cn}"
  openssl x509 -req -days 3650 -in "${name}.csr" \
    -CA root-ca.pem -CAkey root-ca.key -CAcreateserial \
    -out "${name}.pem"
  rm -f "${name}.csr"
}

gen_cert "wazuh-indexer"  "wazuh-indexer"
gen_cert "wazuh-manager"  "wazuh-manager"
gen_cert "wazuh-dashboard" "wazuh-dashboard"
gen_cert "admin"          "admin"

# Copy root-ca cho manager
cp root-ca.pem root-ca-manager.pem

echo "=== [3/4] Tạo Docker secrets ==="
# Sinh mật khẩu ngẫu nhiên nếu chưa tồn tại
create_secret() {
  local name=$1
  local value=${2:-$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)}
  if ! docker secret inspect "$name" &>/dev/null; then
    echo "$value" | docker secret create "$name" -
    echo "  Created secret: $name"
  else
    echo "  Secret already exists: $name (bỏ qua)"
  fi
}

INDEXER_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)
API_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)
DASHBOARD_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)

create_secret "wazuh_indexer_password"   "$INDEXER_PASS"
create_secret "wazuh_api_password"       "$API_PASS"
create_secret "wazuh_dashboard_password" "$DASHBOARD_PASS"

# Lưu passwords vào file tạm (xóa sau)
echo "=== Passwords (LƯU LẠI TRƯỚC KHI XÓA) ==="
echo "  wazuh_indexer_password:   $INDEXER_PASS"
echo "  wazuh_api_password:       $API_PASS"
echo "  wazuh_dashboard_password: $DASHBOARD_PASS"

echo "=== [4/4] Cập nhật internal_users.yml với password hash ==="
INDEXER_HASH=$(docker run --rm wazuh/wazuh-indexer:4.9.2 \
  /usr/share/wazuh-indexer/plugins/opensearch-security/tools/hash.sh -p "$INDEXER_PASS" 2>/dev/null | tail -1)

cat > "$SCRIPT_DIR/config/wazuh_indexer/internal_users.yml" <<EOF
_meta:
  type: "internalusers"
  config_version: 2

admin:
  hash: "$INDEXER_HASH"
  reserved: true
  backend_roles:
    - "admin"
  description: "Wazuh admin user"

kibanaserver:
  hash: "$INDEXER_HASH"
  reserved: true
  description: "Kibana server user"
EOF

echo ""
echo "✓ Setup hoàn tất. Deploy Wazuh stack:"
echo "  docker stack deploy -c $SCRIPT_DIR/docker-compose.yml wazuh"
