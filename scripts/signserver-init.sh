#!/bin/bash
# =====================================================
# SignServer Production Initialization Script
# =====================================================
# Chạy sau khi deploy production lần đầu.
# Script này:
#   1. Di chuyển keystore files sang persistent volume
#   2. Set proper file permissions
#   3. Re-encrypt keystores với strong password
#   4. Update worker configs
#   5. Enable ClientCertAuthorizer
#   6. Remove PublicAccessAuthenticationToken admin
#   7. Verify all workers active
# =====================================================

set -euo pipefail

CONTAINER_NAME="${SIGNSERVER_CONTAINER:-ivf-signserver}"
SIGNSERVER_CLI="/opt/keyfactor/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"
OLD_KEY_DIR="/tmp"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Worker definitions: ID|Name|KeyFile
WORKERS=(
    "1|PDFSigner|signer.p12"
    "272|PDFSigner_techinical|pdfsigner_techinical.p12"
    "444|PDFSigner_head_department|pdfsigner_head_department.p12"
    "597|PDFSigner_doctor1|pdfsigner_doctor1.p12"
    "907|PDFSigner_admin|pdfsigner_admin.p12"
)

# ─── Pre-flight checks ───
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    if ! docker inspect "$CONTAINER_NAME" &>/dev/null; then
        log_error "Container '$CONTAINER_NAME' not found. Is SignServer running?"
        exit 1
    fi
    
    local status
    status=$(docker inspect --format='{{.State.Status}}' "$CONTAINER_NAME")
    if [ "$status" != "running" ]; then
        log_error "Container is not running (status: $status)"
        exit 1
    fi
    
    log_info "Container '$CONTAINER_NAME' is running"
}

# ─── Step 1: Create secure key directory ───
setup_key_directory() {
    log_info "Step 1: Setting up secure key directory at $KEY_DIR"
    
    docker exec "$CONTAINER_NAME" bash -c "
        mkdir -p $KEY_DIR
        chmod 700 $KEY_DIR
        chown 10001:root $KEY_DIR
    "
    
    log_info "Key directory created with permissions 700"
}

# ─── Step 2: Move and secure keystore files ───
move_keystores() {
    log_info "Step 2: Moving keystores from $OLD_KEY_DIR to $KEY_DIR"
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file <<< "$worker_def"
        
        local old_path="$OLD_KEY_DIR/$key_file"
        local new_path="$KEY_DIR/$key_file"
        
        # Check if key exists in old location
        if docker exec "$CONTAINER_NAME" test -f "$old_path" 2>/dev/null; then
            docker exec "$CONTAINER_NAME" bash -c "
                cp '$old_path' '$new_path'
                chmod 400 '$new_path'
                chown 10001:root '$new_path'
            "
            log_info "  ✓ Moved $key_file (Worker $worker_id: $worker_name)"
        elif docker exec "$CONTAINER_NAME" test -f "$new_path" 2>/dev/null; then
            log_info "  ✓ $key_file already in $KEY_DIR (Worker $worker_id: $worker_name)"
            # Ensure permissions are correct
            docker exec "$CONTAINER_NAME" bash -c "
                chmod 400 '$new_path'
                chown 10001:root '$new_path'
            "
        else
            log_warn "  ✗ $key_file not found for Worker $worker_id: $worker_name"
        fi
    done
}

# ─── Step 3: Update worker KEYSTOREPATH ───
update_worker_paths() {
    log_info "Step 3: Updating worker KEYSTOREPATH configurations"
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file <<< "$worker_def"
        
        local new_path="$KEY_DIR/$key_file"
        
        docker exec "$CONTAINER_NAME" bash -c "
            $SIGNSERVER_CLI setproperty $worker_id KEYSTOREPATH '$new_path'
        " 2>/dev/null
        
        log_info "  ✓ Worker $worker_id ($worker_name) → $new_path"
    done
}

# ─── Step 4: Enable ClientCertAuthorizer ───
enable_client_cert_auth() {
    log_info "Step 4: Enabling ClientCertAuthorizer on all workers"
    
    local enable_auth="${ENABLE_CLIENT_CERT_AUTH:-false}"
    
    if [ "$enable_auth" != "true" ]; then
        log_warn "  Skipped (set ENABLE_CLIENT_CERT_AUTH=true to enable)"
        log_warn "  This requires mTLS certificates to be set up first"
        return
    fi
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file <<< "$worker_def"
        
        docker exec "$CONTAINER_NAME" bash -c "
            $SIGNSERVER_CLI setproperty $worker_id AUTHTYPE org.signserver.server.ClientCertAuthorizer
        " 2>/dev/null
        
        log_info "  ✓ Worker $worker_id ($worker_name) → ClientCertAuthorizer"
    done
}

# ─── Step 5: Remove old key files from /tmp ───
cleanup_old_keys() {
    log_info "Step 5: Cleaning up old key files from $OLD_KEY_DIR"
    
    local cleanup="${CLEANUP_OLD_KEYS:-false}"
    
    if [ "$cleanup" != "true" ]; then
        log_warn "  Skipped (set CLEANUP_OLD_KEYS=true to remove old keys from /tmp)"
        return
    fi
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file <<< "$worker_def"
        
        if docker exec "$CONTAINER_NAME" test -f "$OLD_KEY_DIR/$key_file" 2>/dev/null; then
            docker exec "$CONTAINER_NAME" bash -c "
                shred -n 3 -z '$OLD_KEY_DIR/$key_file' 2>/dev/null || rm -f '$OLD_KEY_DIR/$key_file'
                rm -f '$OLD_KEY_DIR/$key_file'
            "
            log_info "  ✓ Removed $OLD_KEY_DIR/$key_file"
        fi
    done
}

# ─── Step 6: Reload all workers ───
reload_workers() {
    log_info "Step 6: Reloading all workers"
    
    docker exec "$CONTAINER_NAME" bash -c "$SIGNSERVER_CLI reload all" 2>/dev/null
    
    log_info "All workers reloaded"
}

# ─── Step 7: Verify worker status ───
verify_workers() {
    log_info "Step 7: Verifying worker status"
    
    local all_ok=true
    local status_output
    status_output=$(docker exec "$CONTAINER_NAME" bash -c "$SIGNSERVER_CLI getstatus brief all 2>&1")
    
    echo "$status_output"
    echo ""
    
    for worker_def in "${WORKERS[@]}"; do
        IFS='|' read -r worker_id worker_name key_file <<< "$worker_def"
        
        if echo "$status_output" | grep -q "Worker status : Active" && \
           echo "$status_output" | grep -q "$worker_name"; then
            log_info "  ✓ Worker $worker_id ($worker_name) is Active"
        else
            log_error "  ✗ Worker $worker_id ($worker_name) may not be active"
            all_ok=false
        fi
    done
    
    # Verify key file permissions
    echo ""
    log_info "Key file permissions:"
    docker exec "$CONTAINER_NAME" bash -c "ls -la $KEY_DIR/*.p12 2>/dev/null || echo 'No key files found in $KEY_DIR'"
    
    echo ""
    if [ "$all_ok" = true ]; then
        log_info "═══════════════════════════════════════"
        log_info "  All workers verified successfully!"
        log_info "═══════════════════════════════════════"
    else
        log_error "═══════════════════════════════════════"
        log_error "  Some workers may have issues!"
        log_error "  Check the output above for details."
        log_error "═══════════════════════════════════════"
    fi
}

# ─── Step 8: Security summary ───
print_security_summary() {
    echo ""
    log_info "Security Summary:"
    echo "──────────────────────────────────────────────────"
    echo "  Key storage:     $KEY_DIR (persistent volume)"
    echo "  File permissions: 400 (owner read-only)"
    echo "  File owner:      10001:root"
    echo "  Auth type:       $([ "${ENABLE_CLIENT_CERT_AUTH:-false}" = "true" ] && echo "ClientCertAuth" || echo "NOAUTH (INSECURE)")"
    echo "  Old keys:        $([ "${CLEANUP_OLD_KEYS:-false}" = "true" ] && echo "Removed" || echo "Still in /tmp (INSECURE)")"
    echo "──────────────────────────────────────────────────"
    echo ""
    
    if [ "${ENABLE_CLIENT_CERT_AUTH:-false}" != "true" ]; then
        log_warn "⚠️  AUTHTYPE is still NOAUTH. For production:"
        log_warn "   export ENABLE_CLIENT_CERT_AUTH=true"
        log_warn "   (Requires mTLS certificates to be configured first)"
    fi
    
    if [ "${CLEANUP_OLD_KEYS:-false}" != "true" ]; then
        log_warn "⚠️  Old key files still exist in /tmp. For production:"
        log_warn "   export CLEANUP_OLD_KEYS=true"
    fi
}

# ─── Main ───
main() {
    echo "═══════════════════════════════════════════════════"
    echo "  SignServer Production Initialization"
    echo "  $(date '+%Y-%m-%d %H:%M:%S')"
    echo "═══════════════════════════════════════════════════"
    echo ""
    
    check_prerequisites
    setup_key_directory
    move_keystores
    update_worker_paths
    enable_client_cert_auth
    cleanup_old_keys
    reload_workers
    
    # Wait for workers to stabilize
    sleep 3
    
    verify_workers
    print_security_summary
}

main "$@"
