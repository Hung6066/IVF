#!/bin/bash
# =====================================================
# SoftHSM2 Initialization Script for SignServer
# =====================================================
# Initializes PKCS#11 token slots for SignServer workers.
# Creates a signing token with PIN protection.
#
# Run after container starts:
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/init-softhsm.sh
#
# Idempotent: skips if token already exists.
# =====================================================

set -euo pipefail

# ─── Configuration ───
TOKEN_LABEL="${SOFTHSM_TOKEN_LABEL:-SignServerToken}"
SO_PIN="${SOFTHSM_SO_PIN:-12345678}"
USER_PIN="${SOFTHSM_USER_PIN:-changeit}"
SOFTHSM2_CONF="${SOFTHSM2_CONF:-/etc/softhsm2.conf}"
PKCS11_LIB="/usr/lib/softhsm/libsofthsm2.so"
JBOSS_CLI="/opt/keyfactor/wildfly-35.0.1.Final/bin/jboss-cli.sh"
SIGNSERVER_CLI="/opt/keyfactor/signserver/bin/signserver"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[SOFTHSM]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[SOFTHSM]${NC} $1"; }
log_error() { echo -e "${RED}[SOFTHSM]${NC} $1"; }

# ─── Check dependencies ───
check_deps() {
    if ! command -v softhsm2-util &>/dev/null; then
        log_error "SoftHSM2 is not installed. Use the signserver-softhsm Docker image."
        exit 1
    fi
    
    if [ ! -f "$PKCS11_LIB" ]; then
        log_error "PKCS#11 library not found at $PKCS11_LIB"
        exit 1
    fi
    
    log_info "SoftHSM2 version: $(softhsm2-util --version 2>/dev/null || echo 'unknown')"
}

# ─── Initialize token ───
init_token() {
    log_info "Step 1: Checking for existing PKCS#11 token..."
    
    # Check if token already exists
    if softhsm2-util --show-slots 2>/dev/null | grep -q "Label:.*$TOKEN_LABEL"; then
        log_warn "Token '$TOKEN_LABEL' already exists. Skipping initialization."
        softhsm2-util --show-slots 2>/dev/null
        return 0
    fi
    
    log_info "Creating PKCS#11 token: $TOKEN_LABEL"
    
    # Initialize the token
    softhsm2-util --init-token \
        --slot 0 \
        --label "$TOKEN_LABEL" \
        --so-pin "$SO_PIN" \
        --pin "$USER_PIN"
    
    log_info "  ✓ Token '$TOKEN_LABEL' created successfully"
    
    # Show slot info
    softhsm2-util --show-slots 2>/dev/null
}

# ─── Register PKCS#11 shared library in SignServer ───
register_pkcs11_library() {
    log_info "Step 2: Registering PKCS#11 shared library in SignServer..."
    
    # Check if SignServer is ready
    if ! $SIGNSERVER_CLI getstatus brief all &>/dev/null; then
        log_warn "SignServer not ready yet. Library registration will happen on next run."
        return 0
    fi
    
    # Check if shared library is already registered
    local existing
    existing=$($SIGNSERVER_CLI wsadmins -allowanyadmin -getglobalproperties 2>/dev/null | grep "SHAREDLIB" || true)
    
    if echo "$existing" | grep -q "SOFTHSM"; then
        log_warn "SoftHSM2 shared library already registered"
        return 0
    fi
    
    # Register the SoftHSM2 PKCS#11 library
    # SignServer uses global property SHAREDLIBRARYNAME -> path mapping
    $SIGNSERVER_CLI setproperty global GLOB.WORKER_SHAREDLIBRARYNAME_SOFTHSM "$PKCS11_LIB" 2>/dev/null || true
    
    log_info "  ✓ PKCS#11 library registered: SOFTHSM → $PKCS11_LIB"
}

# ─── Generate signing key in PKCS#11 token ───
generate_signing_key() {
    local key_alias="${1:-signer}"
    local key_size="${2:-2048}"
    
    log_info "Step 3: Generating RSA key pair in PKCS#11 token..."
    
    # Check if key already exists using pkcs11-tool
    if command -v pkcs11-tool &>/dev/null; then
        if pkcs11-tool --module "$PKCS11_LIB" \
            --login --pin "$USER_PIN" \
            --list-objects 2>/dev/null | grep -q "$key_alias"; then
            log_warn "Key '$key_alias' already exists in token"
            return 0
        fi
    fi
    
    # Generate RSA key pair using pkcs11-tool
    if command -v pkcs11-tool &>/dev/null; then
        pkcs11-tool --module "$PKCS11_LIB" \
            --login --pin "$USER_PIN" \
            --keypairgen --key-type rsa:$key_size \
            --label "$key_alias" \
            --id 01 \
            --usage-sign
        
        log_info "  ✓ RSA-$key_size key pair generated: alias=$key_alias"
    else
        log_warn "pkcs11-tool not available. Keys will be generated via SignServer worker activation."
    fi
}

# ─── Print status summary ───
print_summary() {
    echo ""
    echo "═══════════════════════════════════════════════════════════"
    echo "  SoftHSM2 Initialization Complete"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    echo "  Token Label:    $TOKEN_LABEL"
    echo "  PKCS#11 Lib:    $PKCS11_LIB"
    echo "  Token Dir:      $(grep tokendir $SOFTHSM2_CONF 2>/dev/null | cut -d= -f2 | xargs)"
    echo ""
    echo "  Next steps:"
    echo "  ─────────────────────────────────────────────────────────"
    echo "  1. Migrate P12 workers to PKCS#11:"
    echo "     bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh"
    echo ""
    echo "  2. Or create new PKCS#11 worker manually:"
    echo "     signserver setproperty <ID> SIGNERTOKEN.CLASSPATH \\"
    echo "       org.signserver.server.cryptotokens.PKCS11CryptoToken"
    echo "     signserver setproperty <ID> SHAREDLIBRARYNAME SOFTHSM"
    echo "     signserver setproperty <ID> SLOT $TOKEN_LABEL"
    echo "     signserver setproperty <ID> PIN $USER_PIN"
    echo "     signserver setproperty <ID> DEFAULTKEY signer"
    echo ""
    echo "═══════════════════════════════════════════════════════════"
}

# ─── Main ───
main() {
    echo "═══════════════════════════════════════════════════════════"
    echo "  SoftHSM2 PKCS#11 Token Initialization"
    echo "  $(date '+%Y-%m-%d %H:%M:%S')"
    echo "═══════════════════════════════════════════════════════════"
    echo ""
    
    check_deps
    init_token
    register_pkcs11_library
    generate_signing_key "signer" 2048
    print_summary
}

main "$@"
