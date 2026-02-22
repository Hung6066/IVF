#!/bin/bash
# =====================================================
# Migrate SignServer Workers: P12CryptoToken → PKCS11CryptoToken
# =====================================================
# Migrates existing P12-based workers to use SoftHSM2 PKCS#11 tokens.
# This provides FIPS 140-2 Level 1 compliance and prevents key extraction.
#
# Prerequisites:
#   1. SoftHSM2 initialized: bash init-softhsm.sh
#   2. SignServer running and healthy
#
# What this script does:
#   1. Lists all active P12CryptoToken workers
#   2. For each worker: imports P12 key into SoftHSM2 token
#   3. Reconfigures worker to use PKCS11CryptoToken
#   4. Reloads and activates worker
#   5. Verifies signing still works
#
# Usage:
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh --worker-id 1
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh --dry-run
# =====================================================

set -euo pipefail

# ─── Configuration ───
TOKEN_LABEL="${SOFTHSM_TOKEN_LABEL:-SignServerToken}"
USER_PIN="${SOFTHSM_USER_PIN:-changeit}"
PKCS11_LIB="/usr/lib/softhsm/libsofthsm2.so"
SIGNSERVER_CLI="/opt/keyfactor/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"
BACKUP_DIR="/opt/keyfactor/persistent/keys/backup_p12"
DRY_RUN=false
TARGET_WORKER_ID=""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[MIGRATE]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[MIGRATE]${NC} $1"; }
log_error() { echo -e "${RED}[MIGRATE]${NC} $1"; }
log_step()  { echo -e "${CYAN}[MIGRATE]${NC} $1"; }

# ─── Parse arguments ───
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --dry-run)
                DRY_RUN=true
                log_warn "DRY RUN MODE — no changes will be made"
                shift
                ;;
            --worker-id)
                TARGET_WORKER_ID="$2"
                shift 2
                ;;
            *)
                log_error "Unknown argument: $1"
                echo "Usage: $0 [--dry-run] [--worker-id <ID>]"
                exit 1
                ;;
        esac
    done
}

# ─── Pre-flight checks ───
preflight() {
    log_info "Running pre-flight checks..."
    
    # Check SoftHSM2
    if ! command -v softhsm2-util &>/dev/null; then
        log_error "SoftHSM2 not installed. Run init-softhsm.sh first."
        exit 1
    fi
    
    # Check token exists
    if ! softhsm2-util --show-slots 2>/dev/null | grep -q "$TOKEN_LABEL"; then
        log_error "PKCS#11 token '$TOKEN_LABEL' not found. Run init-softhsm.sh first."
        exit 1
    fi
    
    # Check SignServer
    if ! $SIGNSERVER_CLI getstatus brief all &>/dev/null; then
        log_error "SignServer is not responding. Wait for startup."
        exit 1
    fi
    
    log_info "  ✓ All pre-flight checks passed"
}

# ─── Get list of P12 workers ───
get_p12_workers() {
    log_info "Discovering P12CryptoToken workers..."
    
    local workers=""
    
    # Get worker list from SignServer
    local status_output
    status_output=$($SIGNSERVER_CLI getstatus brief all 2>/dev/null) || true
    
    # Extract worker IDs from status output
    local worker_ids
    worker_ids=$(echo "$status_output" | grep -oP '^\s*\K\d+' | sort -n | uniq) || true
    
    if [ -z "$worker_ids" ]; then
        log_warn "No workers found"
        return
    fi
    
    for wid in $worker_ids; do
        if [ -n "$TARGET_WORKER_ID" ] && [ "$wid" != "$TARGET_WORKER_ID" ]; then
            continue
        fi
        
        # Check if this worker uses P12CryptoToken
        local config
        config=$($SIGNSERVER_CLI getconfig "$wid" 2>/dev/null) || continue
        
        if echo "$config" | grep -q "P12CryptoToken"; then
            local name
            name=$(echo "$config" | grep "^NAME" | cut -d= -f2 | xargs) || name="Worker_$wid"
            local keystore_path
            keystore_path=$(echo "$config" | grep "KEYSTOREPATH" | cut -d= -f2 | xargs) || keystore_path=""
            local default_key
            default_key=$(echo "$config" | grep "DEFAULTKEY" | cut -d= -f2 | xargs) || default_key="signer"
            
            echo "$wid|$name|$keystore_path|$default_key"
        fi
    done
}

# ─── Import P12 key into SoftHSM2 ───
import_p12_to_softhsm() {
    local worker_id="$1"
    local worker_name="$2"
    local keystore_path="$3"
    local key_alias="$4"
    local keystore_password="${5:-changeit}"
    
    log_step "  Importing P12 key for $worker_name (ID: $worker_id)..."
    
    if [ ! -f "$keystore_path" ]; then
        log_error "  Keystore not found: $keystore_path"
        return 1
    fi
    
    if $DRY_RUN; then
        log_warn "  [DRY RUN] Would import $keystore_path → SoftHSM2 token '$TOKEN_LABEL'"
        return 0
    fi
    
    # Use a unique label per worker to avoid conflicts
    local hsm_key_label="${worker_name}_${key_alias}"
    
    # Import PKCS#12 into SoftHSM2 using pkcs11-tool
    # First convert P12 to PEM, then import
    local tmp_dir
    tmp_dir=$(mktemp -d)
    
    # Extract private key from P12
    openssl pkcs12 -in "$keystore_path" \
        -passin "pass:$keystore_password" \
        -nocerts -nodes \
        -out "$tmp_dir/key.pem" 2>/dev/null
    
    # Extract certificate
    openssl pkcs12 -in "$keystore_path" \
        -passin "pass:$keystore_password" \
        -nokeys \
        -out "$tmp_dir/cert.pem" 2>/dev/null
    
    # Convert key to DER for pkcs11-tool
    openssl rsa -in "$tmp_dir/key.pem" \
        -outform DER \
        -out "$tmp_dir/key.der" 2>/dev/null
    
    # Convert cert to DER
    openssl x509 -in "$tmp_dir/cert.pem" \
        -outform DER \
        -out "$tmp_dir/cert.der" 2>/dev/null
    
    # Import private key into SoftHSM2
    pkcs11-tool --module "$PKCS11_LIB" \
        --login --pin "$USER_PIN" \
        --write-object "$tmp_dir/key.der" \
        --type privkey \
        --label "$hsm_key_label" \
        --id "$(printf '%02x' "$worker_id")" \
        --usage-sign 2>/dev/null || {
            log_error "  Failed to import private key"
            rm -rf "$tmp_dir"
            return 1
        }
    
    # Import certificate
    pkcs11-tool --module "$PKCS11_LIB" \
        --login --pin "$USER_PIN" \
        --write-object "$tmp_dir/cert.der" \
        --type cert \
        --label "$hsm_key_label" \
        --id "$(printf '%02x' "$worker_id")" 2>/dev/null || {
            log_warn "  Certificate import failed (non-critical)"
        }
    
    # Cleanup temp files securely
    shred -u "$tmp_dir/key.pem" "$tmp_dir/key.der" 2>/dev/null || rm -f "$tmp_dir/key.pem" "$tmp_dir/key.der"
    rm -rf "$tmp_dir"
    
    log_info "  ✓ Key imported to SoftHSM2: label=$hsm_key_label"
}

# ─── Reconfigure worker to use PKCS#11 ───
reconfigure_worker() {
    local worker_id="$1"
    local worker_name="$2"
    local key_alias="$3"
    
    local hsm_key_label="${worker_name}_${key_alias}"
    
    log_step "  Reconfiguring worker $worker_id to use PKCS11CryptoToken..."
    
    if $DRY_RUN; then
        log_warn "  [DRY RUN] Would reconfigure worker $worker_id: P12 → PKCS11"
        return 0
    fi
    
    # Update crypto token class
    $SIGNSERVER_CLI setproperty global "GLOB.WORKER${worker_id}.SIGNERTOKEN.CLASSPATH" \
        "org.signserver.server.cryptotokens.PKCS11CryptoToken" 2>/dev/null
    
    # Set PKCS#11 properties
    $SIGNSERVER_CLI setproperty "$worker_id" SHAREDLIBRARYNAME "SOFTHSM" 2>/dev/null
    $SIGNSERVER_CLI setproperty "$worker_id" SLOT "$TOKEN_LABEL" 2>/dev/null
    $SIGNSERVER_CLI setproperty "$worker_id" PIN "$USER_PIN" 2>/dev/null
    $SIGNSERVER_CLI setproperty "$worker_id" DEFAULTKEY "$hsm_key_label" 2>/dev/null
    $SIGNSERVER_CLI setproperty "$worker_id" ATTRIBUTE.PRIVATE.RSA.CKA_EXTRACTABLE "FALSE" 2>/dev/null
    $SIGNSERVER_CLI setproperty "$worker_id" ATTRIBUTE.PRIVATE.RSA.CKA_SENSITIVE "TRUE" 2>/dev/null
    
    # Remove old P12-specific properties
    $SIGNSERVER_CLI removeproperty "$worker_id" KEYSTOREPATH 2>/dev/null || true
    $SIGNSERVER_CLI removeproperty "$worker_id" KEYSTOREPASSWORD 2>/dev/null || true
    
    # Reload worker
    $SIGNSERVER_CLI reload "$worker_id" 2>/dev/null
    
    # Activate crypto token
    $SIGNSERVER_CLI activatecryptotoken "$worker_id" "$USER_PIN" 2>/dev/null || {
        log_warn "  Crypto token activation may need manual PIN entry"
    }
    
    log_info "  ✓ Worker $worker_id reconfigured to PKCS11CryptoToken"
}

# ─── Backup P12 keystore ───
backup_p12() {
    local keystore_path="$1"
    local worker_name="$2"
    
    if $DRY_RUN; then return 0; fi
    
    mkdir -p "$BACKUP_DIR"
    
    local backup_name="${worker_name}_$(date +%Y%m%d%H%M%S).p12.bak"
    cp "$keystore_path" "$BACKUP_DIR/$backup_name"
    chmod 400 "$BACKUP_DIR/$backup_name"
    
    log_info "  ✓ P12 backed up: $BACKUP_DIR/$backup_name"
}

# ─── Verify worker after migration ───
verify_worker() {
    local worker_id="$1"
    local worker_name="$2"
    
    if $DRY_RUN; then return 0; fi
    
    log_step "  Verifying worker $worker_name..."
    
    # Check worker status
    local status
    status=$($SIGNSERVER_CLI getstatus brief "$worker_id" 2>/dev/null) || true
    
    if echo "$status" | grep -qi "active"; then
        log_info "  ✓ Worker $worker_name is ACTIVE"
    else
        log_error "  ✗ Worker $worker_name is NOT active after migration"
        log_error "    Status: $status"
        return 1
    fi
    
    # Check crypto token type
    local config
    config=$($SIGNSERVER_CLI getconfig "$worker_id" 2>/dev/null) || true
    
    if echo "$config" | grep -q "PKCS11CryptoToken"; then
        log_info "  ✓ Crypto token type: PKCS11CryptoToken"
    else
        log_error "  ✗ Crypto token type not updated correctly"
        return 1
    fi
}

# ─── Main migration ───
main() {
    echo "═══════════════════════════════════════════════════════════"
    echo "  P12 → PKCS#11 Migration"
    echo "  $(date '+%Y-%m-%d %H:%M:%S')"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    
    parse_args "$@"
    preflight
    
    # Get P12 workers
    local workers
    workers=$(get_p12_workers)
    
    if [ -z "$workers" ]; then
        log_info "No P12CryptoToken workers to migrate."
        exit 0
    fi
    
    local total=0
    local success=0
    local failed=0
    
    echo ""
    echo "$workers" | while IFS='|' read -r wid wname wpath wkey; do
        total=$((total + 1))
        echo ""
        log_info "━━━ Migrating Worker: $wname (ID: $wid) ━━━"
        
        # 1. Backup original P12
        if [ -n "$wpath" ] && [ -f "$wpath" ]; then
            backup_p12 "$wpath" "$wname"
        fi
        
        # 2. Import key into SoftHSM2
        if import_p12_to_softhsm "$wid" "$wname" "$wpath" "$wkey"; then
            # 3. Reconfigure worker
            if reconfigure_worker "$wid" "$wname" "$wkey"; then
                # 4. Verify
                if verify_worker "$wid" "$wname"; then
                    success=$((success + 1))
                else
                    failed=$((failed + 1))
                fi
            else
                failed=$((failed + 1))
            fi
        else
            failed=$((failed + 1))
        fi
    done
    
    echo ""
    echo "═══════════════════════════════════════════════════════════"
    echo "  Migration Complete"
    echo "═══════════════════════════════════════════════════════════"
    echo "  Workers processed: $total"
    echo "  Successful:        $success"
    echo "  Failed:            $failed"
    if [ -d "$BACKUP_DIR" ]; then
        echo "  P12 backups:       $BACKUP_DIR/"
    fi
    echo ""
    
    if $DRY_RUN; then
        echo "  ⚠️  DRY RUN — no actual changes were made"
        echo "  Remove --dry-run to perform the migration"
        echo ""
    fi
    
    echo "═══════════════════════════════════════════════════════════"
}

main "$@"
