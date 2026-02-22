#!/bin/bash
# ==============================================================================
# Certificate Rotation Script for IVF SignServer Infrastructure
# Phase 4: Automated certificate lifecycle management
# ==============================================================================
# Usage:
#   ./rotate-certs.sh --type <api-client|admin|worker> [--worker-id <ID>]
#                     [--grace-days <N>] [--dry-run] [--force]
#
# Prerequisites:
#   - EJBCA CLI available at /opt/keyfactor/bin/ejbca.sh (or EJBCA_CLI env)
#   - SignServer CLI available at /opt/keyfactor/bin/signserver-cli (or SIGNSERVER_CLI env)
#   - OpenSSL installed
#   - Certificates directory at /opt/keyfactor/persistent/certs/ (or CERT_DIR env)
# ==============================================================================

set -euo pipefail

# ── Configuration ──────────────────────────────────────────
CERT_DIR="${CERT_DIR:-/opt/keyfactor/persistent/certs}"
BACKUP_DIR="${BACKUP_DIR:-/opt/keyfactor/persistent/certs/backup_rotated}"
EJBCA_CLI="${EJBCA_CLI:-/opt/keyfactor/bin/ejbca.sh}"
SIGNSERVER_CLI="${SIGNSERVER_CLI:-/opt/keyfactor/bin/signserver-cli}"
GRACE_DAYS="${GRACE_DAYS:-30}"
LOG_FILE="${LOG_FILE:-/var/log/cert-rotation.log}"

# EJBCA configuration
EJBCA_CA_NAME="${EJBCA_CA_NAME:-InternalRootCA}"
EJBCA_CERT_PROFILE="${EJBCA_CERT_PROFILE:-tlsServerAuth}"
EJBCA_EE_PROFILE="${EJBCA_EE_PROFILE:-default}"

# Certificate type to rotate
CERT_TYPE=""
WORKER_ID=""
DRY_RUN=false
FORCE=false

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ── Functions ──────────────────────────────────────────────

log() {
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo -e "${BLUE}[$timestamp]${NC} $1"
    echo "[$timestamp] $1" >> "$LOG_FILE" 2>/dev/null || true
}

log_success() { echo -e "${GREEN}✓${NC} $1"; }
log_warn() { echo -e "${YELLOW}⚠${NC} $1"; }
log_error() { echo -e "${RED}✗${NC} $1"; }

usage() {
    cat <<EOF
Certificate Rotation Script for IVF SignServer Infrastructure

Usage: $0 --type <api-client|admin|worker> [OPTIONS]

Options:
  --type <TYPE>        Certificate type to rotate:
                         api-client  - API ↔ SignServer mTLS client certificate
                         admin       - Admin client certificate
                         worker      - SignServer worker signing certificate
  --worker-id <ID>     Worker ID (required when --type=worker)
  --grace-days <N>     Days before expiry to trigger rotation (default: 30)
  --dry-run            Show what would be done without making changes
  --force              Force rotation even if certificate is not near expiry

Environment Variables:
  CERT_DIR             Certificate directory (default: /opt/keyfactor/persistent/certs)
  EJBCA_CLI            Path to EJBCA CLI
  SIGNSERVER_CLI       Path to SignServer CLI
  EJBCA_CA_NAME        CA name in EJBCA (default: InternalRootCA)

Examples:
  $0 --type api-client --dry-run
  $0 --type worker --worker-id 444 --force
  $0 --type admin --grace-days 60
EOF
    exit 1
}

check_cert_expiry() {
    local cert_file="$1"
    local grace_days="$2"

    if [ ! -f "$cert_file" ]; then
        echo "MISSING"
        return
    fi

    local expiry_date
    expiry_date=$(openssl x509 -enddate -noout -in "$cert_file" 2>/dev/null | sed 's/notAfter=//')
    
    if [ -z "$expiry_date" ]; then
        echo "INVALID"
        return
    fi

    local expiry_epoch
    expiry_epoch=$(date -d "$expiry_date" +%s 2>/dev/null || date -jf "%b %d %H:%M:%S %Y %Z" "$expiry_date" +%s 2>/dev/null)
    local now_epoch
    now_epoch=$(date +%s)
    local grace_seconds=$((grace_days * 86400))
    local remaining=$((expiry_epoch - now_epoch))
    local remaining_days=$((remaining / 86400))

    if [ "$remaining" -le 0 ]; then
        echo "EXPIRED|$remaining_days"
    elif [ "$remaining" -le "$grace_seconds" ]; then
        echo "EXPIRING|$remaining_days"
    else
        echo "VALID|$remaining_days"
    fi
}

backup_cert() {
    local cert_file="$1"
    local cert_name="$2"

    if [ ! -f "$cert_file" ]; then
        return 0
    fi

    mkdir -p "$BACKUP_DIR"
    local timestamp
    timestamp=$(date '+%Y%m%d_%H%M%S')
    local backup_file="${BACKUP_DIR}/${cert_name}_${timestamp}.pem"
    
    cp "$cert_file" "$backup_file"
    chmod 400 "$backup_file"
    log "Backed up $cert_file → $backup_file"
}

rotate_api_client_cert() {
    local cert_file="${CERT_DIR}/api-client.pem"
    local key_file="${CERT_DIR}/api-client-key.pem"
    local p12_file="${CERT_DIR}/api-client.p12"
    local cn="ivf-api-client"

    log "=== Rotating API Client Certificate ==="

    # Check current cert status
    local status
    status=$(check_cert_expiry "$cert_file" "$GRACE_DAYS")
    local state=$(echo "$status" | cut -d'|' -f1)
    local days=$(echo "$status" | cut -d'|' -f2)

    case "$state" in
        VALID)
            if [ "$FORCE" = false ]; then
                log_success "Certificate is valid ($days days remaining). No rotation needed."
                log "  Use --force to rotate anyway."
                return 0
            fi
            log_warn "Certificate is valid ($days days remaining) but --force specified."
            ;;
        EXPIRING)
            log_warn "Certificate expires in $days days — rotation recommended."
            ;;
        EXPIRED)
            log_error "Certificate is EXPIRED ($days days ago)!"
            ;;
        MISSING)
            log_warn "Certificate not found — will generate new one."
            ;;
        INVALID)
            log_error "Certificate is invalid/corrupt — will regenerate."
            ;;
    esac

    if [ "$DRY_RUN" = true ]; then
        log "[DRY RUN] Would rotate API client certificate"
        log "[DRY RUN]   1. Backup existing cert"
        log "[DRY RUN]   2. Generate new private key (EC P-256)"
        log "[DRY RUN]   3. Create CSR with CN=$cn"
        log "[DRY RUN]   4. Submit to EJBCA CA ($EJBCA_CA_NAME)"
        log "[DRY RUN]   5. Update SignServer authorized clients"
        log "[DRY RUN]   6. Restart API service to load new cert"
        return 0
    fi

    # Step 1: Backup existing
    backup_cert "$cert_file" "api-client"
    [ -f "$key_file" ] && backup_cert "$key_file" "api-client-key"

    # Step 2: Generate new EC key
    log "Generating new EC P-256 private key..."
    openssl ecparam -genkey -name prime256v1 -noout -out "${key_file}.new" 2>/dev/null
    chmod 400 "${key_file}.new"

    # Step 3: Create CSR
    log "Creating certificate signing request..."
    openssl req -new -key "${key_file}.new" \
        -out "${CERT_DIR}/api-client.csr" \
        -subj "/CN=${cn}/O=IVF/OU=API" \
        -addext "keyUsage = digitalSignature, keyAgreement" \
        -addext "extendedKeyUsage = clientAuth" 2>/dev/null

    # Step 4: Submit to EJBCA
    log "Submitting CSR to EJBCA ($EJBCA_CA_NAME)..."
    if command -v "$EJBCA_CLI" &>/dev/null; then
        # Use EJBCA CLI for proper certificate issuance
        "$EJBCA_CLI" ra addendentity \
            --username "${cn}-$(date +%s)" \
            --dn "CN=${cn},O=IVF,OU=API" \
            --caname "$EJBCA_CA_NAME" \
            --type 1 \
            --token PEM 2>/dev/null || true

        "$EJBCA_CLI" ca signcsr \
            --caname "$EJBCA_CA_NAME" \
            --csrfile "${CERT_DIR}/api-client.csr" \
            --certfile "${cert_file}.new" 2>/dev/null
    else
        # Fallback: self-signed (development only)
        log_warn "EJBCA CLI not available. Using self-signed certificate (dev only)."
        openssl x509 -req -in "${CERT_DIR}/api-client.csr" \
            -signkey "${key_file}.new" \
            -out "${cert_file}.new" \
            -days 365 \
            -sha256 2>/dev/null
    fi

    # Step 5: Swap certificates atomically
    if [ -f "${cert_file}.new" ]; then
        mv "${key_file}.new" "$key_file"
        mv "${cert_file}.new" "$cert_file"
        chmod 400 "$key_file" "$cert_file"
        rm -f "${CERT_DIR}/api-client.csr"
        log_success "API client certificate rotated successfully."

        # Step 6: Extract serial for SignServer auth update
        local new_serial
        new_serial=$(openssl x509 -serial -noout -in "$cert_file" | sed 's/serial=//')
        local new_issuer_dn
        new_issuer_dn=$(openssl x509 -issuer -noout -in "$cert_file" | sed 's/issuer=//')
        log "New certificate serial: $new_serial"
        log "New certificate issuer: $new_issuer_dn"

        # Update SignServer authorized clients
        if command -v "$SIGNSERVER_CLI" &>/dev/null; then
            log "Updating SignServer authorized clients..."
            # Add new cert as authorized before removing old
            for worker_id in 1 272 444 597 907; do
                "$SIGNSERVER_CLI" setproperty "$worker_id" "AUTHCLIENT2" \
                    "${new_serial};${new_issuer_dn};CN=${cn}" 2>/dev/null || true
            done
            log_warn "Old authorized client (AUTHCLIENT1) preserved for grace period."
            log_warn "Run '$0 --type api-client --cleanup' after validating new cert works."
        fi
    else
        log_error "Failed to generate new certificate. Old cert preserved."
        # Restore backup
        [ -f "${key_file}.new" ] && rm -f "${key_file}.new"
        return 1
    fi

    log_success "API client certificate rotation complete."
}

rotate_admin_cert() {
    local cert_file="${CERT_DIR}/admin-client.pem"
    local key_file="${CERT_DIR}/admin-client-key.pem"
    local cn="ivf-admin"

    log "=== Rotating Admin Client Certificate ==="

    local status
    status=$(check_cert_expiry "$cert_file" "$GRACE_DAYS")
    local state=$(echo "$status" | cut -d'|' -f1)
    local days=$(echo "$status" | cut -d'|' -f2)

    case "$state" in
        VALID)
            if [ "$FORCE" = false ]; then
                log_success "Admin certificate is valid ($days days remaining). No rotation needed."
                return 0
            fi
            log_warn "Admin certificate is valid ($days days) but --force specified."
            ;;
        EXPIRING) log_warn "Admin certificate expires in $days days." ;;
        EXPIRED)  log_error "Admin certificate is EXPIRED!" ;;
        MISSING)  log_warn "Admin certificate not found — generating." ;;
    esac

    if [ "$DRY_RUN" = true ]; then
        log "[DRY RUN] Would rotate admin client certificate"
        log "[DRY RUN]   1. Backup existing cert"
        log "[DRY RUN]   2. Generate new EC P-256 key"
        log "[DRY RUN]   3. Issue from EJBCA with adminAuth profile"
        log "[DRY RUN]   4. Update admin CLI trust config"
        return 0
    fi

    # Backup existing
    backup_cert "$cert_file" "admin-client"
    [ -f "$key_file" ] && backup_cert "$key_file" "admin-client-key"

    # Generate new key
    log "Generating new EC P-256 private key for admin..."
    openssl ecparam -genkey -name prime256v1 -noout -out "${key_file}.new" 2>/dev/null
    chmod 400 "${key_file}.new"

    # Create CSR
    openssl req -new -key "${key_file}.new" \
        -out "${CERT_DIR}/admin-client.csr" \
        -subj "/CN=${cn}/O=IVF/OU=Admin" \
        -addext "keyUsage = digitalSignature" \
        -addext "extendedKeyUsage = clientAuth" 2>/dev/null

    # Issue certificate
    if command -v "$EJBCA_CLI" &>/dev/null; then
        "$EJBCA_CLI" ra addendentity \
            --username "${cn}-$(date +%s)" \
            --dn "CN=${cn},O=IVF,OU=Admin" \
            --caname "$EJBCA_CA_NAME" \
            --type 1 \
            --token PEM 2>/dev/null || true

        "$EJBCA_CLI" ca signcsr \
            --caname "$EJBCA_CA_NAME" \
            --csrfile "${CERT_DIR}/admin-client.csr" \
            --certfile "${cert_file}.new" 2>/dev/null
    else
        log_warn "EJBCA CLI not available. Using self-signed certificate (dev only)."
        openssl x509 -req -in "${CERT_DIR}/admin-client.csr" \
            -signkey "${key_file}.new" \
            -out "${cert_file}.new" \
            -days 365 -sha256 2>/dev/null
    fi

    if [ -f "${cert_file}.new" ]; then
        mv "${key_file}.new" "$key_file"
        mv "${cert_file}.new" "$cert_file"
        chmod 400 "$key_file" "$cert_file"
        rm -f "${CERT_DIR}/admin-client.csr"
        log_success "Admin client certificate rotated successfully."
    else
        log_error "Failed to generate admin certificate."
        [ -f "${key_file}.new" ] && rm -f "${key_file}.new"
        return 1
    fi
}

rotate_worker_cert() {
    local worker_id="$1"

    if [ -z "$worker_id" ]; then
        log_error "Worker ID is required for worker cert rotation."
        log "  Use: $0 --type worker --worker-id <ID>"
        exit 1
    fi

    log "=== Rotating Worker $worker_id Signing Certificate ==="

    if [ "$DRY_RUN" = true ]; then
        log "[DRY RUN] Would rotate worker $worker_id signing certificate"
        log "[DRY RUN]   1. Query current crypto token type (P12 vs PKCS11)"
        log "[DRY RUN]   2. Generate new signing key pair"
        log "[DRY RUN]   3. Create CSR and get cert from EJBCA"
        log "[DRY RUN]   4. Install certificate in worker"
        log "[DRY RUN]   5. Test worker with new key"
        return 0
    fi

    if ! command -v "$SIGNSERVER_CLI" &>/dev/null; then
        log_error "SignServer CLI not available at $SIGNSERVER_CLI"
        exit 1
    fi

    # Detect crypto token type
    local token_type
    token_type=$("$SIGNSERVER_CLI" getproperty "$worker_id" CRYPTOTOKEN_IMPLEMENTATION 2>/dev/null | grep -o 'PKCS11\|P12' || echo "P12")
    log "Worker $worker_id crypto token type: $token_type"

    # Generate new key alias with timestamp
    local key_alias="signing-key-$(date +%Y%m%d)"
    log "Generating new key pair with alias: $key_alias"

    if [ "$token_type" = "PKCS11" ]; then
        # PKCS#11: generate key directly in HSM (non-extractable)
        "$SIGNSERVER_CLI" generatekey "$worker_id" RSA 2048 "$key_alias"
    else
        # P12: generate key in keystore
        "$SIGNSERVER_CLI" generatekey "$worker_id" RSA 2048 "$key_alias"
    fi

    # Generate CSR with new key
    log "Generating CSR for worker $worker_id..."
    "$SIGNSERVER_CLI" generatecertreq "$worker_id" "$key_alias" \
        "CN=IVF-Worker-${worker_id},O=IVF" \
        > "${CERT_DIR}/worker-${worker_id}.csr" 2>/dev/null

    # Submit to EJBCA
    if command -v "$EJBCA_CLI" &>/dev/null; then
        log "Submitting CSR to EJBCA..."
        "$EJBCA_CLI" ra addendentity \
            --username "worker-${worker_id}-$(date +%s)" \
            --dn "CN=IVF-Worker-${worker_id},O=IVF" \
            --caname "$EJBCA_CA_NAME" \
            --type 1 \
            --token PEM 2>/dev/null || true

        "$EJBCA_CLI" ca signcsr \
            --caname "$EJBCA_CA_NAME" \
            --csrfile "${CERT_DIR}/worker-${worker_id}.csr" \
            --certfile "${CERT_DIR}/worker-${worker_id}-new.pem" 2>/dev/null

        # Install signed certificate
        if [ -f "${CERT_DIR}/worker-${worker_id}-new.pem" ]; then
            log "Installing certificate in worker $worker_id..."
            "$SIGNSERVER_CLI" uploadcert "$worker_id" \
                "${CERT_DIR}/worker-${worker_id}-new.pem" PEM

            # Switch to new key
            "$SIGNSERVER_CLI" setproperty "$worker_id" DEFAULTKEY "$key_alias"
            "$SIGNSERVER_CLI" reload "$worker_id"

            log_success "Worker $worker_id certificate rotated successfully."
            log "New key alias: $key_alias"
        else
            log_error "Failed to obtain certificate from EJBCA."
            return 1
        fi
    else
        log_warn "EJBCA CLI not available. Key generated but no certificate issued."
        log_warn "Manually sign the CSR at: ${CERT_DIR}/worker-${worker_id}.csr"
    fi

    # Test worker
    log "Testing worker $worker_id..."
    local test_result
    test_result=$("$SIGNSERVER_CLI" getstatus brief "$worker_id" 2>/dev/null || echo "UNKNOWN")
    if echo "$test_result" | grep -qi "active"; then
        log_success "Worker $worker_id is ACTIVE with new certificate."
    else
        log_warn "Worker $worker_id status: $test_result — verify manually."
    fi
}

check_all_certs() {
    log "=== Certificate Expiry Report ==="
    echo ""
    printf "%-30s %-12s %-15s\n" "Certificate" "Status" "Days Remaining"
    printf "%-30s %-12s %-15s\n" "------------------------------" "------------" "---------------"

    for cert_file in "${CERT_DIR}"/*.pem; do
        [ -f "$cert_file" ] || continue
        local name
        name=$(basename "$cert_file" .pem)
        local status
        status=$(check_cert_expiry "$cert_file" "$GRACE_DAYS")
        local state=$(echo "$status" | cut -d'|' -f1)
        local days=$(echo "$status" | cut -d'|' -f2)

        local color="$GREEN"
        case "$state" in
            EXPIRING) color="$YELLOW" ;;
            EXPIRED|INVALID) color="$RED" ;;
            MISSING) color="$RED" ;;
        esac

        printf "%-30s ${color}%-12s${NC} %-15s\n" "$name" "$state" "${days:-N/A}"
    done
    echo ""
}

# ── Parse Arguments ────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --type)
            CERT_TYPE="$2"
            shift 2
            ;;
        --worker-id)
            WORKER_ID="$2"
            shift 2
            ;;
        --grace-days)
            GRACE_DAYS="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --force)
            FORCE=true
            shift
            ;;
        --check)
            check_all_certs
            exit 0
            ;;
        -h|--help)
            usage
            ;;
        *)
            log_error "Unknown option: $1"
            usage
            ;;
    esac
done

# ── Validate ───────────────────────────────────────────────
if [ -z "$CERT_TYPE" ]; then
    log_error "Certificate type is required."
    usage
fi

# ── Create required directories ────────────────────────────
mkdir -p "$CERT_DIR" "$BACKUP_DIR"
touch "$LOG_FILE" 2>/dev/null || true

# ── Main ───────────────────────────────────────────────────
log "Certificate Rotation — Type: $CERT_TYPE, Grace: ${GRACE_DAYS}d, DryRun: $DRY_RUN, Force: $FORCE"

if [ "$DRY_RUN" = true ]; then
    echo ""
    echo -e "${YELLOW}═══ DRY RUN MODE — No changes will be made ═══${NC}"
    echo ""
fi

case "$CERT_TYPE" in
    api-client)
        rotate_api_client_cert
        ;;
    admin)
        rotate_admin_cert
        ;;
    worker)
        rotate_worker_cert "$WORKER_ID"
        ;;
    *)
        log_error "Unknown certificate type: $CERT_TYPE"
        log "Supported types: api-client, admin, worker"
        exit 1
        ;;
esac

log "Done."
