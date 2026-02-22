#!/bin/bash
# =====================================================
# Certificate Generation Script for IVF System
# =====================================================
# Generates self-signed certificates for internal mTLS:
#   1. Internal CA (root + intermediate)
#   2. SignServer TLS certificate
#   3. API client certificate (for mTLS with SignServer)
#   4. EJBCA TLS certificate
#   5. Admin certificate
#
# For production with EJBCA-issued certs:
#   Use EJBCA Admin UI to issue proper certificates.
#   This script is for internal/staging environments where
#   EJBCA is not yet configured or for bootstrapping.
# =====================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
CERTS_DIR="$PROJECT_DIR/certs"
SECRETS_DIR="$PROJECT_DIR/secrets"

# Certificate parameters
CA_DAYS=3650        # 10 years
SERVER_DAYS=825     # ~2.25 years (Apple max)
CLIENT_DAYS=365     # 1 year
KEY_SIZE=4096
COUNTRY="VN"
STATE="Ho Chi Minh"
ORG="IVF Clinic"
OU="IT Department"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ─── Check dependencies ───
check_deps() {
    if ! command -v openssl &>/dev/null; then
        log_error "openssl is required but not installed"
        exit 1
    fi
    log_info "OpenSSL version: $(openssl version)"
}

# ─── Create directories ───
setup_dirs() {
    log_info "Creating certificate directories..."
    mkdir -p "$CERTS_DIR/ca"
    mkdir -p "$CERTS_DIR/signserver"
    mkdir -p "$CERTS_DIR/api"
    mkdir -p "$CERTS_DIR/ejbca"
    mkdir -p "$CERTS_DIR/admin"
    mkdir -p "$SECRETS_DIR"
    chmod 700 "$CERTS_DIR" "$SECRETS_DIR"
}

# ─── Step 1: Internal Root CA ───
generate_root_ca() {
    log_info "Step 1: Generating Internal Root CA..."
    
    local ca_dir="$CERTS_DIR/ca"
    
    if [ -f "$ca_dir/ca.key" ]; then
        log_warn "Root CA already exists, skipping (delete $ca_dir/ca.key to regenerate)"
        return
    fi
    
    # Generate CA private key
    openssl genrsa -aes256 -passout pass:changeit -out "$ca_dir/ca.key" $KEY_SIZE
    chmod 400 "$ca_dir/ca.key"
    
    # Generate CA certificate
    openssl req -new -x509 \
        -key "$ca_dir/ca.key" \
        -passin pass:changeit \
        -days $CA_DAYS \
        -sha256 \
        -out "$ca_dir/ca.pem" \
        -subj "/C=$COUNTRY/ST=$STATE/O=$ORG/OU=$OU/CN=IVF Internal Root CA"
    
    # Create CA chain (just root for self-signed)
    cp "$ca_dir/ca.pem" "$CERTS_DIR/ca-chain.pem"
    
    log_info "  ✓ Root CA: $ca_dir/ca.pem"
    log_info "  ✓ CA Chain: $CERTS_DIR/ca-chain.pem"
}

# ─── Step 2: SignServer TLS Certificate ───
generate_signserver_tls() {
    log_info "Step 2: Generating SignServer TLS certificate..."
    
    local ss_dir="$CERTS_DIR/signserver"
    local ca_dir="$CERTS_DIR/ca"
    
    if [ -f "$ss_dir/signserver-tls.p12" ]; then
        log_warn "SignServer TLS cert already exists, skipping"
        return
    fi
    
    # Generate key
    openssl genrsa -out "$ss_dir/signserver-tls.key" $KEY_SIZE
    chmod 400 "$ss_dir/signserver-tls.key"
    
    # Create CSR
    openssl req -new \
        -key "$ss_dir/signserver-tls.key" \
        -out "$ss_dir/signserver-tls.csr" \
        -subj "/C=$COUNTRY/ST=$STATE/O=$ORG/OU=$OU/CN=signserver.ivf.local"
    
    # Create extensions file for SAN
    cat > "$ss_dir/signserver-tls.ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature, keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=@alt_names

[alt_names]
DNS.1=signserver
DNS.2=signserver.ivf.local
DNS.3=localhost
IP.1=127.0.0.1
EOF
    
    # Sign with CA
    openssl x509 -req \
        -in "$ss_dir/signserver-tls.csr" \
        -CA "$ca_dir/ca.pem" \
        -CAkey "$ca_dir/ca.key" \
        -passin pass:changeit \
        -CAcreateserial \
        -out "$ss_dir/signserver-tls.pem" \
        -days $SERVER_DAYS \
        -sha256 \
        -extfile "$ss_dir/signserver-tls.ext"
    
    # Create PKCS#12 for SignServer (Java keystore)
    local ss_tls_pass
    ss_tls_pass=$(openssl rand -base64 32)
    echo -n "$ss_tls_pass" > "$SECRETS_DIR/signserver_tls_password.txt"
    chmod 400 "$SECRETS_DIR/signserver_tls_password.txt"
    
    openssl pkcs12 -export \
        -in "$ss_dir/signserver-tls.pem" \
        -inkey "$ss_dir/signserver-tls.key" \
        -certfile "$ca_dir/ca.pem" \
        -out "$ss_dir/signserver-tls.p12" \
        -name "signserver-tls" \
        -passout "pass:$ss_tls_pass"
    
    # Cleanup temp files
    rm -f "$ss_dir/signserver-tls.csr" "$ss_dir/signserver-tls.ext"
    
    log_info "  ✓ TLS Cert: $ss_dir/signserver-tls.pem"
    log_info "  ✓ TLS P12:  $ss_dir/signserver-tls.p12"
}

# ─── Step 3: API Client Certificate (for mTLS) ───
generate_api_client_cert() {
    log_info "Step 3: Generating API client certificate for mTLS..."
    
    local api_dir="$CERTS_DIR/api"
    local ca_dir="$CERTS_DIR/ca"
    
    if [ -f "$api_dir/api-client.p12" ]; then
        log_warn "API client cert already exists, skipping"
        return
    fi
    
    # Generate key
    openssl genrsa -out "$api_dir/api-client.key" $KEY_SIZE
    chmod 400 "$api_dir/api-client.key"
    
    # Create CSR
    openssl req -new \
        -key "$api_dir/api-client.key" \
        -out "$api_dir/api-client.csr" \
        -subj "/C=$COUNTRY/ST=$STATE/O=$ORG/OU=$OU/CN=ivf-api-client"
    
    # Extensions for client auth
    cat > "$api_dir/api-client.ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature
extendedKeyUsage=clientAuth
subjectAltName=@alt_names

[alt_names]
DNS.1=api
DNS.2=ivf-api
DNS.3=localhost
EOF
    
    # Sign with CA
    openssl x509 -req \
        -in "$api_dir/api-client.csr" \
        -CA "$ca_dir/ca.pem" \
        -CAkey "$ca_dir/ca.key" \
        -passin pass:changeit \
        -CAcreateserial \
        -out "$api_dir/api-client.pem" \
        -days $CLIENT_DAYS \
        -sha256 \
        -extfile "$api_dir/api-client.ext"
    
    # Create PKCS#12 for .NET HttpClient
    local api_cert_pass
    api_cert_pass=$(openssl rand -base64 32)
    echo -n "$api_cert_pass" > "$SECRETS_DIR/api_cert_password.txt"
    chmod 400 "$SECRETS_DIR/api_cert_password.txt"
    
    openssl pkcs12 -export \
        -in "$api_dir/api-client.pem" \
        -inkey "$api_dir/api-client.key" \
        -certfile "$ca_dir/ca.pem" \
        -out "$api_dir/api-client.p12" \
        -name "ivf-api-client" \
        -passout "pass:$api_cert_pass"
    
    # Extract serial number for SignServer AUTHORIZED_CLIENTS
    local serial
    serial=$(openssl x509 -in "$api_dir/api-client.pem" -serial -noout | cut -d= -f2)
    echo "$serial" > "$api_dir/api-client-serial.txt"
    
    # Cleanup
    rm -f "$api_dir/api-client.csr" "$api_dir/api-client.ext"
    
    log_info "  ✓ Client Cert: $api_dir/api-client.pem"
    log_info "  ✓ Client P12:  $api_dir/api-client.p12"
    log_info "  ✓ Serial:      $serial"
}

# ─── Step 4: Admin Certificate ───
generate_admin_cert() {
    log_info "Step 4: Generating admin certificate..."
    
    local admin_dir="$CERTS_DIR/admin"
    local ca_dir="$CERTS_DIR/ca"
    
    if [ -f "$admin_dir/admin.p12" ]; then
        log_warn "Admin cert already exists, skipping"
        return
    fi
    
    # Generate key
    openssl genrsa -out "$admin_dir/admin.key" $KEY_SIZE
    chmod 400 "$admin_dir/admin.key"
    
    # Create CSR
    openssl req -new \
        -key "$admin_dir/admin.key" \
        -out "$admin_dir/admin.csr" \
        -subj "/C=$COUNTRY/ST=$STATE/O=$ORG/OU=$OU/CN=IVF Admin"
    
    # Extensions
    cat > "$admin_dir/admin.ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature
extendedKeyUsage=clientAuth
EOF
    
    # Sign with CA
    openssl x509 -req \
        -in "$admin_dir/admin.csr" \
        -CA "$ca_dir/ca.pem" \
        -CAkey "$ca_dir/ca.key" \
        -passin pass:changeit \
        -CAcreateserial \
        -out "$admin_dir/admin.pem" \
        -days $CLIENT_DAYS \
        -sha256 \
        -extfile "$admin_dir/admin.ext"
    
    # Create PKCS#12 for browser import
    local admin_pass
    admin_pass=$(openssl rand -base64 16)
    echo -n "$admin_pass" > "$SECRETS_DIR/admin_cert_password.txt"
    chmod 400 "$SECRETS_DIR/admin_cert_password.txt"
    
    openssl pkcs12 -export \
        -in "$admin_dir/admin.pem" \
        -inkey "$admin_dir/admin.key" \
        -certfile "$ca_dir/ca.pem" \
        -out "$admin_dir/admin.p12" \
        -name "ivf-admin" \
        -passout "pass:$admin_pass"
    
    # Extract serial for EJBCA/SignServer admin config
    local serial
    serial=$(openssl x509 -in "$admin_dir/admin.pem" -serial -noout | cut -d= -f2)
    echo "$serial" > "$admin_dir/admin-serial.txt"
    
    # Cleanup
    rm -f "$admin_dir/admin.csr" "$admin_dir/admin.ext"
    
    log_info "  ✓ Admin Cert: $admin_dir/admin.pem"
    log_info "  ✓ Admin P12:  $admin_dir/admin.p12 (import into browser)"
    log_info "  ✓ Serial:     $serial"
    log_info "  ✓ Password:   see $SECRETS_DIR/admin_cert_password.txt"
}

# ─── Step 5: Generate secrets ───
generate_secrets() {
    log_info "Step 5: Generating service passwords..."
    
    local files=(
        "ivf_db_password"
        "ejbca_db_password"
        "signserver_db_password"
        "keystore_password"
        "jwt_secret"
        "minio_access_key"
        "minio_secret_key"
    )
    
    for name in "${files[@]}"; do
        local file="$SECRETS_DIR/${name}.txt"
        if [ -f "$file" ]; then
            log_warn "  $name already exists, skipping"
            continue
        fi
        
        if [ "$name" = "minio_access_key" ]; then
            # MinIO access key is like a username (alphanumeric)
            openssl rand -hex 16 > "$file"
        elif [ "$name" = "jwt_secret" ]; then
            # JWT needs longer key
            openssl rand -base64 64 > "$file"
        else
            openssl rand -base64 48 > "$file"
        fi
        
        chmod 400 "$file"
        log_info "  ✓ Generated $name"
    done
}

# ─── Summary ───
print_summary() {
    echo ""
    echo "═══════════════════════════════════════════════════════════"
    echo "  Certificate Generation Complete"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    echo "  Generated files:"
    echo "  ─────────────────────────────────────────────────────────"
    find "$CERTS_DIR" -type f \( -name "*.pem" -o -name "*.p12" \) | sort | while read -r f; do
        echo "    $(basename "$f") → $f"
    done
    echo ""
    echo "  Secrets:"
    echo "  ─────────────────────────────────────────────────────────"
    find "$SECRETS_DIR" -name "*.txt" | sort | while read -r f; do
        echo "    $(basename "$f")"
    done
    echo ""
    echo "  Next steps:"
    echo "  ─────────────────────────────────────────────────────────"
    echo "  1. Run: ./scripts/signserver-init.sh"
    echo "  2. Import admin.p12 into your browser"
    echo "  3. Configure docker-compose.production.yml env vars:"
    
    if [ -f "$CERTS_DIR/admin/admin-serial.txt" ]; then
        local admin_serial
        admin_serial=$(cat "$CERTS_DIR/admin/admin-serial.txt")
        echo "     EJBCA_ADMIN_CERT_SERIAL=$admin_serial"
        echo "     SIGNSERVER_ADMIN_CERT_SERIAL=$admin_serial"
    fi
    
    if [ -f "$CERTS_DIR/api/api-client-serial.txt" ]; then
        local api_serial
        api_serial=$(cat "$CERTS_DIR/api/api-client-serial.txt")
        echo "     API_CLIENT_CERT_SERIAL=$api_serial"
    fi
    
    echo ""
    echo "  4. Deploy: docker compose -f docker-compose.yml \\"
    echo "               -f docker-compose.production.yml up -d"
    echo ""
    echo "═══════════════════════════════════════════════════════════"
}

# ─── Main ───
main() {
    echo "═══════════════════════════════════════════════════════════"
    echo "  IVF Certificate Generation"
    echo "  $(date '+%Y-%m-%d %H:%M:%S')"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    
    check_deps
    setup_dirs
    generate_root_ca
    generate_signserver_tls
    generate_api_client_cert
    generate_admin_cert
    generate_secrets
    print_summary
}

main "$@"
