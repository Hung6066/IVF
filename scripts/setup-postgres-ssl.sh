#!/bin/bash
# =====================================================
# PostgreSQL SSL — Enable TLS in Container
# =====================================================
# Generates self-signed server certificates and enables
# SSL for the PostgreSQL container. After running,
# clients can connect with sslmode=require.
#
# This script:
#   1. Generates CA + server cert (10-year validity)
#   2. Copies certs into the PostgreSQL data volume
#   3. Enables SSL in postgresql.conf
#   4. Reloads PostgreSQL (no restart needed)
#
# Usage:
#   ssh root@45.134.226.56 'bash -s' < scripts/setup-postgres-ssl.sh
#
# After running:
#   - DBeaver/DataGrip: change SSL to "require" or "verify-ca"
#   - Connection string: add sslmode=require
#   - API connection: update ASPNETCORE connection string
#
# The script is idempotent — safe to re-run.
# =====================================================

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ─── Configuration ───
DB_CONTAINER=$(docker ps -q -f name=ivf_db)
CERT_DIR="/tmp/pg-ssl-certs"
CERT_VALIDITY=3650  # 10 years

if [ -z "$DB_CONTAINER" ]; then
    log_error "PostgreSQL container (ivf_db) not found!"
    exit 1
fi

# ─── Pre-flight: Check if SSL already enabled ───
SSL_STATUS=$(docker exec "$DB_CONTAINER" psql -U postgres -d ivf_db -t -c "SHOW ssl;" 2>/dev/null | tr -d ' ')
if [ "$SSL_STATUS" = "on" ]; then
    log_info "PostgreSQL SSL is already enabled"
    docker exec "$DB_CONTAINER" psql -U postgres -d ivf_db -c "
        SELECT ssl, version, cipher, bits, client_dn
        FROM pg_stat_ssl
        JOIN pg_stat_activity ON pg_stat_ssl.pid = pg_stat_activity.pid
        WHERE state = 'active' LIMIT 5;"
    exit 0
fi

# ─── Step 1: Generate certificates ───
log_info "Step 1: Generating SSL certificates..."

mkdir -p "$CERT_DIR"
cd "$CERT_DIR"

# Generate CA key + cert
if [ ! -f ca.key ]; then
    openssl genrsa -out ca.key 4096
    openssl req -new -x509 -key ca.key -out ca.crt \
        -days "$CERT_VALIDITY" \
        -subj "/CN=IVF PostgreSQL CA/O=IVF Clinic/ST=Ho Chi Minh/C=VN"
    log_info "  ✓ CA certificate generated"
fi

# Generate server key + CSR + cert
if [ ! -f server.key ]; then
    openssl genrsa -out server.key 2048
    openssl req -new -key server.key -out server.csr \
        -subj "/CN=ivf-postgres/O=IVF Clinic/ST=Ho Chi Minh/C=VN"

    # Create SAN extension
    cat > server-ext.cnf << EXTEOF
[v3_req]
subjectAltName = @alt_names
[alt_names]
DNS.1 = db
DNS.2 = localhost
DNS.3 = ivf_db
IP.1 = 127.0.0.1
EXTEOF

    openssl x509 -req -in server.csr \
        -CA ca.crt -CAkey ca.key -CAcreateserial \
        -out server.crt \
        -days "$CERT_VALIDITY" \
        -extfile server-ext.cnf -extensions v3_req

    log_info "  ✓ Server certificate generated (SANs: db, localhost, 127.0.0.1)"
fi

# Fix permissions (PostgreSQL requires specific perms)
chmod 600 server.key
chmod 644 server.crt ca.crt

log_info "  Certificates in: $CERT_DIR"

# ─── Step 2: Copy certs into container ───
log_info "Step 2: Copying certificates into PostgreSQL container..."

# PostgreSQL data dir
PG_DATA="/var/lib/postgresql/data"

docker cp "$CERT_DIR/server.crt" "$DB_CONTAINER:$PG_DATA/server.crt"
docker cp "$CERT_DIR/server.key" "$DB_CONTAINER:$PG_DATA/server.key"
docker cp "$CERT_DIR/ca.crt" "$DB_CONTAINER:$PG_DATA/root.crt"

# Fix ownership (postgres user = UID 70 in alpine, 999 in debian)
docker exec "$DB_CONTAINER" sh -c "
    chown postgres:postgres $PG_DATA/server.crt $PG_DATA/server.key $PG_DATA/root.crt
    chmod 600 $PG_DATA/server.key
    chmod 644 $PG_DATA/server.crt $PG_DATA/root.crt
"

log_info "  ✓ Certificates copied to container"

# ─── Step 3: Enable SSL in postgresql.conf ───
log_info "Step 3: Enabling SSL in postgresql.conf..."

docker exec "$DB_CONTAINER" sh -c "
    PG_CONF=\"$PG_DATA/postgresql.conf\"

    # Enable SSL
    if grep -q '^ssl = on' \"\$PG_CONF\"; then
        echo '  SSL already enabled in config'
    elif grep -q '^#*ssl = ' \"\$PG_CONF\"; then
        sed -i \"s/^#*ssl = .*/ssl = on/\" \"\$PG_CONF\"
    else
        echo '' >> \"\$PG_CONF\"
        echo '# IVF: Enable SSL' >> \"\$PG_CONF\"
        echo 'ssl = on' >> \"\$PG_CONF\"
    fi

    # Set cert paths
    if ! grep -q '^ssl_cert_file' \"\$PG_CONF\"; then
        echo \"ssl_cert_file = 'server.crt'\" >> \"\$PG_CONF\"
    fi
    if ! grep -q '^ssl_key_file' \"\$PG_CONF\"; then
        echo \"ssl_key_file = 'server.key'\" >> \"\$PG_CONF\"
    fi
    if ! grep -q '^ssl_ca_file' \"\$PG_CONF\"; then
        echo \"ssl_ca_file = 'root.crt'\" >> \"\$PG_CONF\"
    fi
"

log_info "  ✓ SSL enabled in postgresql.conf"

# ─── Step 4: Update pg_hba.conf to prefer SSL ───
log_info "Step 4: Updating pg_hba.conf..."

docker exec "$DB_CONTAINER" sh -c "
    PG_HBA=\"$PG_DATA/pg_hba.conf\"

    # Add hostssl line for external connections (before existing host lines)
    if ! grep -q '^hostssl' \"\$PG_HBA\"; then
        # Allow SSL connections from any host with scram-sha-256
        echo '' >> \"\$PG_HBA\"
        echo '# IVF: SSL connections (preferred)' >> \"\$PG_HBA\"
        echo 'hostssl all all 0.0.0.0/0 scram-sha-256' >> \"\$PG_HBA\"
        echo 'hostssl all all ::/0 scram-sha-256' >> \"\$PG_HBA\"
    fi
"

log_info "  ✓ pg_hba.conf updated"

# ─── Step 5: Reload PostgreSQL ───
log_info "Step 5: Reloading PostgreSQL..."

docker exec "$DB_CONTAINER" psql -U postgres -c "SELECT pg_reload_conf();"
sleep 2

log_info "  ✓ PostgreSQL reloaded"

# ─── Step 6: Verify ───
log_info "Step 6: Verification..."

SSL_CHECK=$(docker exec "$DB_CONTAINER" psql -U postgres -d ivf_db -t -c "SHOW ssl;" | tr -d ' ')
if [ "$SSL_CHECK" = "on" ]; then
    log_info "  ✓ SSL is ON"
else
    log_error "  ✗ SSL is still OFF (may need container restart)"
    log_warn "  Try: docker service update --force ivf_db"
    exit 1
fi

# Show SSL info
docker exec "$DB_CONTAINER" psql -U postgres -d ivf_db -c "
    SELECT name, setting FROM pg_settings
    WHERE name LIKE 'ssl%'
    ORDER BY name;"

# ─── Step 7: Save CA cert for clients ───
log_info "Step 7: Saving CA cert for client distribution..."

# Save to a persistent location on host
PERSIST_DIR="/root/ivf/certs/postgres"
mkdir -p "$PERSIST_DIR"
cp "$CERT_DIR/ca.crt" "$PERSIST_DIR/pg-ca.crt"
cp "$CERT_DIR/server.crt" "$PERSIST_DIR/pg-server.crt"
log_info "  ✓ CA cert saved: $PERSIST_DIR/pg-ca.crt"

# Clean up temp
rm -rf "$CERT_DIR"

echo ""
log_info "═══════════════════════════════════════════════════"
log_info "  PostgreSQL SSL enabled!"
log_info ""
log_info "  Client connection (DBeaver/DataGrip):"
log_info "    SSL Mode: require (or verify-ca)"
log_info "    CA Cert: download from VPS: $PERSIST_DIR/pg-ca.crt"
log_info ""
log_info "  Connection string (sslmode=require):"
log_info "    Host=127.0.0.1;Port=15433;Database=ivf_db;"
log_info "    Username=postgres;Password=***;Ssl Mode=Require"
log_info ""
log_info "  Note: Internal Docker connections (API → db) can"
log_info "  still use sslmode=disable (trusted Docker network)."
log_info "═══════════════════════════════════════════════════"
