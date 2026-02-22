#!/bin/bash
# =====================================================
# Key Rotation Script for IVF SignServer
# =====================================================
# Rotates signing keystores for all or specific workers.
# Steps:
#   1. Generate new keypair + CSR
#   2. Get certificate from EJBCA (or self-sign)
#   3. Create new PKCS#12
#   4. Upload to SignServer worker
#   5. Reload worker
#   6. Verify signing works
#   7. Remove old keystore
#
# Usage:
#   ./rotate-keys.sh                 # Rotate all workers
#   ./rotate-keys.sh --worker 1      # Rotate specific worker
#   ./rotate-keys.sh --dry-run       # Preview only
# =====================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
KEYS_DIR="$PROJECT_DIR/keys/signserver"
SECRETS_DIR="$PROJECT_DIR/secrets"
CERTS_DIR="$PROJECT_DIR/certs"
BACKUP_DIR="$PROJECT_DIR/keys/signserver/backup"
CONTAINER_NAME="${SIGNSERVER_CONTAINER:-ivf-signserver}"
SIGNSERVER_CLI="/opt/keyfactor/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"

# Parameters
DRY_RUN=false
TARGET_WORKER=""
KEY_SIZE=2048
CERT_DAYS=365

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Worker definitions
WORKERS=(
    "1|PDFSigner|signer.p12|CN=PDFSigner,O=IVF Clinic"
    "272|PDFSigner_techinical|pdfsigner_techinical.p12|CN=Ky Thuat Vien,O=IVF Clinic"
    "444|PDFSigner_head_department|pdfsigner_head_department.p12|CN=Truong Khoa,O=IVF Clinic"
    "597|PDFSigner_doctor1|pdfsigner_doctor1.p12|CN=Bac Si,O=IVF Clinic"
    "907|PDFSigner_admin|pdfsigner_admin.p12|CN=Quan Tri,O=IVF Clinic"
)

# Parse arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --dry-run) DRY_RUN=true ;;
            --worker) TARGET_WORKER="$2"; shift ;;
            --help|-h)
                echo "Usage: $0 [--worker ID] [--dry-run]"
                exit 0
                ;;
            *) log_error "Unknown option: $1"; exit 1 ;;
        esac
        shift
    done
}

# Backup existing keystore
backup_keystore() {
    local key_file="$1"
    local worker_id="$2"
    
    mkdir -p "$BACKUP_DIR"
    
    local backup_name="${key_file%.p12}_$(date +%Y%m%d_%H%M%S).p12.bak"
    
    if [ -f "$KEYS_DIR/$key_file" ]; then
        cp "$KEYS_DIR/$key_file" "$BACKUP_DIR/$backup_name"
        chmod 400 "$BACKUP_DIR/$backup_name"
        log_info "  Backup: $BACKUP_DIR/$backup_name"
    fi
}

# Generate new keystore
rotate_worker_key() {
    local worker_id="$1"
    local worker_name="$2"
    local key_file="$3"
    local subject="$4"
    
    log_info "Rotating key for Worker $worker_id ($worker_name)..."
    
    if [ "$DRY_RUN" = true ]; then
        log_info "  [DRY RUN] Would rotate $key_file"
        return
    fi
    
    local tmp_dir
    tmp_dir=$(mktemp -d)
    
    # 1. Backup existing
    backup_keystore "$key_file" "$worker_id"
    
    # 2. Generate new keypair
    openssl genrsa -out "$tmp_dir/new.key" $KEY_SIZE 2>/dev/null
    
    # 3. Self-sign certificate (for internal use)
    #    In production, submit CSR to EJBCA instead
    openssl req -new -x509 \
        -key "$tmp_dir/new.key" \
        -out "$tmp_dir/new.pem" \
        -days $CERT_DAYS \
        -sha256 \
        -subj "/$subject" \
        2>/dev/null
    
    # 4. Create PKCS#12
    local keystore_pass
    keystore_pass=$(cat "$SECRETS_DIR/keystore_password.txt" 2>/dev/null || echo "changeit")
    
    openssl pkcs12 -export \
        -in "$tmp_dir/new.pem" \
        -inkey "$tmp_dir/new.key" \
        -out "$KEYS_DIR/$key_file" \
        -name "signer" \
        -passout "pass:$keystore_pass" \
        2>/dev/null
    
    chmod 400 "$KEYS_DIR/$key_file"
    
    # 5. Copy to container
    docker cp "$KEYS_DIR/$key_file" "$CONTAINER_NAME:$KEY_DIR/$key_file"
    docker exec "$CONTAINER_NAME" chmod 400 "$KEY_DIR/$key_file"
    docker exec "$CONTAINER_NAME" chown 10001:root "$KEY_DIR/$key_file"
    
    # 6. Reload worker
    docker exec "$CONTAINER_NAME" bash -c "$SIGNSERVER_CLI reload $worker_id" 2>/dev/null
    
    # 7. Verify
    sleep 2
    local status
    status=$(docker exec "$CONTAINER_NAME" bash -c "$SIGNSERVER_CLI getstatus brief $worker_id 2>&1" || true)
    
    if echo "$status" | grep -q "Token status  : Active"; then
        log_info "  ✓ Worker $worker_id re-activated with new key"
    else
        log_error "  ✗ Worker $worker_id failed to activate!"
        log_error "  Restoring from backup..."
        
        local latest_backup
        latest_backup=$(ls -t "$BACKUP_DIR/${key_file%.p12}_"*.p12.bak 2>/dev/null | head -1)
        if [ -n "$latest_backup" ]; then
            cp "$latest_backup" "$KEYS_DIR/$key_file"
            docker cp "$KEYS_DIR/$key_file" "$CONTAINER_NAME:$KEY_DIR/$key_file"
            docker exec "$CONTAINER_NAME" bash -c "$SIGNSERVER_CLI reload $worker_id" 2>/dev/null
            log_info "  Restored from $latest_backup"
        fi
    fi
    
    # Cleanup temp
    rm -rf "$tmp_dir"
}

# Main
main() {
    parse_args "$@"
    
    echo "═══════════════════════════════════════════════════════════"
    echo "  SignServer Key Rotation"
    echo "  $(date '+%Y-%m-%d %H:%M:%S')"
    [ "$DRY_RUN" = true ] && echo "  ** DRY RUN MODE **"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file subject <<< "$worker_def"
        
        if [ -n "$TARGET_WORKER" ] && [ "$TARGET_WORKER" != "$worker_id" ]; then
            continue
        fi
        
        rotate_worker_key "$worker_id" "$worker_name" "$key_file" "$subject"
        echo ""
    done
    
    log_info "Key rotation complete"
}

main "$@"
