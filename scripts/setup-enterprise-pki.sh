#!/bin/bash
# ============================================================
# IVF Enterprise PKI Setup Script
# ============================================================
# Complete EJBCA + SignServer PKI hierarchy setup with PKCS#11:
#   Phase 1: Root CA + Subordinate Signing CA (EJBCA)
#   Phase 2: Certificate Profile Configuration
#   Phase 3: SoftHSM2 Token Setup (SignServer)
#   Phase 4: End Entity Enrollment (EJBCA -> SoftHSM / P12 fallback)
#   Phase 5: SignServer Worker Configuration (PKCS#11 primary, P12 fallback)
#   Phase 6: OCSP Responder Verification
#   Phase 7: Multi-Tenant Sub-CA (optional --tenant)
#   Phase 8: Final Verification & Signing Test
#
# Usage:
#   bash scripts/setup-enterprise-pki.sh
#   bash scripts/setup-enterprise-pki.sh --dry-run
#   bash scripts/setup-enterprise-pki.sh --skip-ca
#   bash scripts/setup-enterprise-pki.sh --skip-workers
#   bash scripts/setup-enterprise-pki.sh --force
#   bash scripts/setup-enterprise-pki.sh --tenant clinic-01
#
# Prerequisites:
#   - Docker running with ivf-ejbca and ivf-signserver containers
#   - EJBCA and SignServer healthy
#   - SoftHSM2 installed in SignServer container
#
# Idempotent -- safe to re-run.
# ============================================================

set -euo pipefail

# ── Disable MSYS/Git Bash path conversion (Windows) ────────
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"

# ── Colors ──────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
log_ok()      { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()    { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error()   { echo -e "${RED}[ERROR]${NC} $1"; }
log_step()    { echo -e "\n${MAGENTA}══════════════════════════════════════════════════${NC}"; \
                echo -e "${MAGENTA}  $1${NC}"; \
                echo -e "${MAGENTA}══════════════════════════════════════════════════${NC}"; }
log_substep() { echo -e "${CYAN}  ── $1${NC}"; }

# ── Configuration ───────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
CERTS_DIR="$PROJECT_DIR/certs"

EJBCA_CONTAINER="ivf-ejbca"
SIGNSERVER_CONTAINER="ivf-signserver"
EJBCA_CLI="/opt/keyfactor/bin/ejbca.sh"
SIGNSERVER_CLI="/opt/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"
EJBCA_CERT_DIR="/tmp/ejbca-certs"
KEYSTORE_PASSWORD="changeit"

# SoftHSM2 configuration
SOFTHSM_LIB="/usr/lib64/pkcs11/libsofthsm2.so"
SOFTHSM_TOKEN_DIR="/opt/keyfactor/persistent/softhsm/tokens"
SOFTHSM_CONF="/etc/softhsm2.conf"
SOFTHSM_TOKEN_LABEL="SignServerToken"
SOFTHSM_PIN="changeit"
SOFTHSM_SO_PIN="changeit"

# CA Configuration
ROOT_CA_NAME="IVF-Root-CA"
ROOT_CA_DN="CN=IVF Root Certificate Authority,O=IVF Healthcare,OU=PKI,C=VN"
ROOT_CA_VALIDITY="7305"   # 20 years in days
ROOT_CA_SIGNALG="SHA256WithRSA"

SUB_CA_NAME="IVF-Signing-SubCA"
SUB_CA_DN="CN=IVF Document Signing CA,O=IVF Healthcare,OU=Digital Signing,C=VN"
SUB_CA_VALIDITY="3652"   # 10 years in days
SUB_CA_SIGNALG="SHA256WithRSA"

# WireGuard VPN endpoint (10.200.0.1 = server via WireGuard tunnel)
VPN_HOST="${VPN_HOST:-10.200.0.1}"

# URLs for certificate extensions
EJBCA_URL="https://${VPN_HOST}:8443"
EJBCA_PUBLIC_URL="http://${VPN_HOST}:8442"
SIGNSERVER_URL="https://${VPN_HOST}:9443"
SIGNSERVER_HTTP_URL="http://${VPN_HOST}:9080"
CRL_DP_URL="http://${VPN_HOST}:8442/ejbca/publicweb/webdist/certdist?cmd=crl"
AIA_CA_ISSUERS_URL="http://${VPN_HOST}:8442/ejbca/publicweb/webdist/certdist?cmd=cacert"
OCSP_URL="http://${VPN_HOST}:8442/ejbca/publicweb/status/ocsp"

# Worker definitions: ID|Name|KeyAlias|Purpose
PDF_WORKERS=(
    "1|PDFSigner|signer|Main PDF signer"
    "272|PDFSigner_technical|pdfsigner_technical|Technical staff"
    "444|PDFSigner_head_department|pdfsigner_head_department|Department head"
    "597|PDFSigner_doctor1|pdfsigner_doctor1|Doctor"
    "907|PDFSigner_admin|pdfsigner_admin|Admin"
)

TSA_WORKER="100|TimeStampSigner|tsa|TSA"

# Crypto token mode tracking (set during Phase 3)
USE_PKCS11=false

# ── Parse Arguments ─────────────────────────────────────────
DRY_RUN=false
SKIP_CA=false
SKIP_WORKERS=false
FORCE=false
TENANT_ID=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)      DRY_RUN=true; shift ;;
        --skip-ca)      SKIP_CA=true; shift ;;
        --skip-workers) SKIP_WORKERS=true; shift ;;
        --force)        FORCE=true; shift ;;
        --tenant)
            if [[ -z "${2:-}" ]]; then
                log_error "--tenant requires a tenant ID argument"
                exit 1
            fi
            TENANT_ID="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --dry-run          Preview actions without making changes"
            echo "  --skip-ca          Skip CA creation (if CAs already exist)"
            echo "  --skip-workers     Skip SignServer worker configuration"
            echo "  --force            Recreate everything (destructive)"
            echo "  --tenant <id>      Create tenant-specific Sub-CA and signer certificates"
            echo "  -h, --help         Show this help"
            echo ""
            echo "Environment variables:"
            echo "  VPN_HOST           EJBCA/SignServer host (default: 10.200.0.1)"
            exit 0
            ;;
        *) log_error "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Helper Functions ────────────────────────────────────────
run_ejbca() {
    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] docker exec $EJBCA_CONTAINER $EJBCA_CLI $*"
        return 0
    fi
    docker exec "$EJBCA_CONTAINER" "$EJBCA_CLI" "$@"
}

run_signserver() {
    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] docker exec $SIGNSERVER_CONTAINER $SIGNSERVER_CLI $*"
        return 0
    fi
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" "$@"
}

exec_ejbca() {
    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] docker exec $EJBCA_CONTAINER $*"
        return 0
    fi
    docker exec "$EJBCA_CONTAINER" "$@"
}

exec_signserver() {
    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] docker exec $SIGNSERVER_CONTAINER $*"
        return 0
    fi
    docker exec "$SIGNSERVER_CONTAINER" "$@"
}

exec_signserver_root() {
    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] docker exec --user root $SIGNSERVER_CONTAINER $*"
        return 0
    fi
    docker exec --user root "$SIGNSERVER_CONTAINER" "$@"
}

ca_exists() {
    local ca_name="$1"
    docker exec "$EJBCA_CONTAINER" "$EJBCA_CLI" ca listcas 2>/dev/null | grep -q "$ca_name" 2>/dev/null
}

get_ca_id() {
    # Parse CA numeric ID from 'ca listcas' output.
    # Actual EJBCA output format (ID is on the line AFTER the CA name):
    #   CA Name: IVF-Root-CA
    #    Id: 1031502430
    local ca_name="$1"
    local ca_id
    ca_id=$(docker exec "$EJBCA_CONTAINER" "$EJBCA_CLI" ca listcas 2>/dev/null \
        | grep -A1 "CA Name: ${ca_name}$" \
        | grep "Id:" \
        | sed 's/.*Id: *\(-\{0,1\}[0-9][0-9]*\).*/\1/' \
        | head -1)
    if [ -z "$ca_id" ]; then
        # Broader match if exact match fails
        ca_id=$(docker exec "$EJBCA_CONTAINER" "$EJBCA_CLI" ca listcas 2>/dev/null \
            | grep -A1 "$ca_name" \
            | grep "Id:" \
            | sed 's/.*Id: *\(-\{0,1\}[0-9][0-9]*\).*/\1/' \
            | head -1)
    fi
    echo "$ca_id"
}

wait_for_container() {
    local container="$1"
    local health_url="$2"
    local max_retries=60

    log_info "Waiting for $container to be healthy..."
    for i in $(seq 1 $max_retries); do
        if curl -fsk "$health_url" > /dev/null 2>&1; then
            log_ok "$container is healthy!"
            return 0
        fi
        if [ "$i" -eq "$max_retries" ]; then
            log_error "$container failed to start after ${max_retries} retries"
            return 1
        fi
        echo -n "."
        sleep 5
    done
}

# ── Banner ──────────────────────────────────────────────────
echo -e "${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║       IVF Enterprise PKI Infrastructure Setup              ║${NC}"
echo -e "${CYAN}║       EJBCA + SoftHSM2 + SignServer PKCS#11 Workers        ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
    log_warn "DRY RUN MODE -- no changes will be made"
    echo ""
fi

if [ "$FORCE" = true ]; then
    log_warn "FORCE MODE -- existing configuration will be overwritten"
    echo ""
fi

if [ -n "$TENANT_ID" ]; then
    log_info "TENANT MODE -- will create tenant-specific Sub-CA for: $TENANT_ID"
    echo ""
fi

# ── Pre-flight Checks ──────────────────────────────────────
log_step "Phase 0: Pre-flight Checks"

for container in "$EJBCA_CONTAINER" "$SIGNSERVER_CONTAINER"; do
    if ! docker inspect "$container" &>/dev/null; then
        log_error "Container '$container' not found. Start with: docker compose up -d"
        exit 1
    fi
    log_ok "Container '$container' is running"
done

wait_for_container "$EJBCA_CONTAINER" "${EJBCA_URL}/ejbca/publicweb/healthcheck/ejbcahealth"
wait_for_container "$SIGNSERVER_CONTAINER" "${SIGNSERVER_URL}/signserver/healthcheck/signserverhealth"

# Create output directories
mkdir -p "$CERTS_DIR/ca" "$CERTS_DIR/signers" "$CERTS_DIR/tsa" "$CERTS_DIR/api"

# Create EJBCA cert output directory inside container
exec_ejbca mkdir -p "$EJBCA_CERT_DIR" 2>/dev/null || true

log_ok "Pre-flight checks passed"


# ════════════════════════════════════════════════════════════
# PHASE 1: EJBCA Certificate Authority Hierarchy
# ════════════════════════════════════════════════════════════

if [ "$SKIP_CA" = false ]; then

    # ── Step 1: Create Root CA ──────────────────────────────
    log_step "Phase 1a: Create Root CA -- $ROOT_CA_NAME"

    if ca_exists "$ROOT_CA_NAME" && [ "$FORCE" = false ]; then
        log_warn "Root CA '$ROOT_CA_NAME' already exists -- skipping (use --force to recreate)"
    else
        if ca_exists "$ROOT_CA_NAME" && [ "$FORCE" = true ]; then
            log_warn "Force mode: Root CA exists but will attempt re-initialization"
        fi

        log_info "Creating Root CA: $ROOT_CA_NAME"
        log_info "  Subject DN: $ROOT_CA_DN"
        log_info "  Key:        RSA 4096"
        log_info "  Signature:  $ROOT_CA_SIGNALG"
        log_info "  Validity:   $ROOT_CA_VALIDITY days (20 years)"

        # NOTE: Do NOT pass -certprofile -- EJBCA CE does not have ROOTCA/SUBCA
        # profile names. Omitting the flag uses built-in defaults.
        run_ejbca ca init \
            --caname "$ROOT_CA_NAME" \
            --dn "$ROOT_CA_DN" \
            --tokenType "soft" \
            --tokenPass "null" \
            --keytype "RSA" \
            --keyspec "4096" \
            -v "$ROOT_CA_VALIDITY" \
            -s "$ROOT_CA_SIGNALG" \
            --policy "null" 2>/dev/null && \
            log_ok "Root CA '$ROOT_CA_NAME' created successfully" || \
            log_warn "Root CA creation returned non-zero (may already exist)"

        # Verify Root CA
        if [ "$DRY_RUN" = false ]; then
            if ca_exists "$ROOT_CA_NAME"; then
                log_ok "Verified: Root CA '$ROOT_CA_NAME' exists in EJBCA"
            else
                log_error "Root CA '$ROOT_CA_NAME' not found after creation"
                exit 1
            fi
        fi
    fi

    # ── Step 2: Create Subordinate Signing CA ───────────────
    log_step "Phase 1b: Create Subordinate CA -- $SUB_CA_NAME"

    if ca_exists "$SUB_CA_NAME" && [ "$FORCE" = false ]; then
        log_warn "Sub-CA '$SUB_CA_NAME' already exists -- skipping (use --force to recreate)"
    else
        log_info "Creating Subordinate CA: $SUB_CA_NAME"
        log_info "  Subject DN: $SUB_CA_DN"
        log_info "  Signed by:  $ROOT_CA_NAME"
        log_info "  Key:        RSA 4096"
        log_info "  Signature:  $SUB_CA_SIGNALG"
        log_info "  Validity:   $SUB_CA_VALIDITY days (10 years)"

        # Get Root CA numeric ID -- --signedby requires the CAID, not the name
        ROOT_CA_ID=""
        if [ "$DRY_RUN" = false ]; then
            ROOT_CA_ID=$(get_ca_id "$ROOT_CA_NAME")
            if [ -z "$ROOT_CA_ID" ]; then
                log_error "Could not determine Root CA ID for '$ROOT_CA_NAME'"
                log_info "Listing known CAs:"
                run_ejbca ca listcas 2>/dev/null || true
                exit 1
            fi
            log_info "  Root CA ID: $ROOT_CA_ID"
        fi

        # NOTE: Do NOT pass -certprofile -- EJBCA CE does not have SUBCA profile.
        # --signedby requires the numeric CAID, not the CA name.
        run_ejbca ca init \
            --caname "$SUB_CA_NAME" \
            --dn "$SUB_CA_DN" \
            --tokenType "soft" \
            --tokenPass "null" \
            --keytype "RSA" \
            --keyspec "4096" \
            -v "$SUB_CA_VALIDITY" \
            -s "$SUB_CA_SIGNALG" \
            --signedby "$ROOT_CA_ID" \
            --policy "null" 2>/dev/null && \
            log_ok "Sub-CA '$SUB_CA_NAME' created successfully" || \
            log_warn "Sub-CA creation returned non-zero (may already exist)"

        # Verify Sub-CA
        if [ "$DRY_RUN" = false ]; then
            if ca_exists "$SUB_CA_NAME"; then
                log_ok "Verified: Sub-CA '$SUB_CA_NAME' exists in EJBCA"
            else
                log_error "Sub-CA '$SUB_CA_NAME' not found after creation"
                exit 1
            fi
        fi
    fi

    # ── Step 3: Export CA Certificates ──────────────────────
    log_step "Phase 1c: Export CA Certificates"

    if [ "$DRY_RUN" = false ]; then
        # Export Root CA certificate
        log_info "Exporting Root CA certificate..."
        run_ejbca ca getcacert \
            --caname "$ROOT_CA_NAME" \
            -f /tmp/root-ca.pem 2>/dev/null || true

        docker cp "${EJBCA_CONTAINER}:/tmp/root-ca.pem" "$CERTS_DIR/ca/root-ca.pem" 2>/dev/null && \
            log_ok "Root CA cert exported to certs/ca/root-ca.pem" || \
            log_warn "Could not export Root CA cert"

        # Also copy as ca.pem for backward compatibility
        if [ -f "$CERTS_DIR/ca/root-ca.pem" ]; then
            cp "$CERTS_DIR/ca/root-ca.pem" "$CERTS_DIR/ca/ca.pem"
            log_ok "Root CA cert copied to certs/ca/ca.pem"
        fi

        # Export Sub-CA certificate
        log_info "Exporting Sub-CA certificate..."
        run_ejbca ca getcacert \
            --caname "$SUB_CA_NAME" \
            -f /tmp/sub-ca.pem 2>/dev/null || true

        docker cp "${EJBCA_CONTAINER}:/tmp/sub-ca.pem" "$CERTS_DIR/ca/sub-ca.pem" 2>/dev/null && \
            log_ok "Sub-CA cert exported to certs/ca/sub-ca.pem" || \
            log_warn "Could not export Sub-CA cert"

        # Build CA chain (Sub-CA + Root CA)
        log_info "Building CA chain file..."
        if [ -f "$CERTS_DIR/ca/sub-ca.pem" ] && [ -f "$CERTS_DIR/ca/root-ca.pem" ]; then
            cat "$CERTS_DIR/ca/sub-ca.pem" "$CERTS_DIR/ca/root-ca.pem" > "$CERTS_DIR/ca-chain.pem"
            log_ok "CA chain exported to certs/ca-chain.pem"
        else
            log_warn "Cannot build CA chain -- missing Sub-CA or Root CA cert"
        fi

        # Deploy CA chain to SignServer container
        log_info "Deploying CA chain to SignServer container..."
        if [ -f "$CERTS_DIR/ca-chain.pem" ]; then
            docker cp "$CERTS_DIR/ca-chain.pem" \
                "${SIGNSERVER_CONTAINER}:${KEY_DIR}/ca-chain.pem" 2>/dev/null && \
                log_ok "CA chain deployed to SignServer" || \
                log_warn "Could not deploy CA chain to SignServer"
        fi

        if [ -f "$CERTS_DIR/ca/root-ca.pem" ]; then
            docker cp "$CERTS_DIR/ca/root-ca.pem" \
                "${SIGNSERVER_CONTAINER}:${KEY_DIR}/ivf-ca.pem" 2>/dev/null && \
                log_ok "Root CA cert deployed as ivf-ca.pem in SignServer" || \
                log_warn "Could not deploy Root CA cert to SignServer"
        fi
    else
        log_warn "[DRY RUN] Would export Root CA, Sub-CA, and chain certs"
    fi

else
    log_step "Phase 1: CA Creation -- SKIPPED (--skip-ca)"
    log_info "Using existing CAs. Ensure '$ROOT_CA_NAME' and '$SUB_CA_NAME' exist."
fi


# ════════════════════════════════════════════════════════════
# PHASE 2: Certificate Profile Configuration
# ════════════════════════════════════════════════════════════

log_step "Phase 2: Certificate Profile Configuration"

log_info "Customizing ENDUSER certificate profile with CRL/AIA extensions..."

if [ "$DRY_RUN" = false ]; then
    # Configure CRL Distribution Point on ENDUSER profile
    run_ejbca ca editcertificateprofile \
        --cpname "ENDUSER" \
        --field "useCRLDistributionPoint" \
        --value "true" 2>/dev/null && \
        log_ok "ENDUSER profile: useCRLDistributionPoint = true" || \
        log_info "Could not set useCRLDistributionPoint (may not be supported in CE CLI)"

    run_ejbca ca editcertificateprofile \
        --cpname "ENDUSER" \
        --field "useDefaultCRLDistributionPoint" \
        --value "true" 2>/dev/null && \
        log_ok "ENDUSER profile: useDefaultCRLDistributionPoint = true" || \
        log_info "Could not set useDefaultCRLDistributionPoint"

    # Configure Authority Information Access
    run_ejbca ca editcertificateprofile \
        --cpname "ENDUSER" \
        --field "useAuthorityInformationAccess" \
        --value "true" 2>/dev/null && \
        log_ok "ENDUSER profile: useAuthorityInformationAccess = true" || \
        log_info "Could not set useAuthorityInformationAccess"

    run_ejbca ca editcertificateprofile \
        --cpname "ENDUSER" \
        --field "CRLDistributionPointURI" \
        --value "${CRL_DP_URL}" 2>/dev/null && \
        log_ok "ENDUSER profile: CRL DP URI set" || \
        log_info "Could not set CRLDistributionPointURI"

    run_ejbca ca editcertificateprofile \
        --cpname "ENDUSER" \
        --field "caIssuers" \
        --value "${AIA_CA_ISSUERS_URL}" 2>/dev/null && \
        log_ok "ENDUSER profile: CA Issuers AIA URI set" || \
        log_info "Could not set caIssuers"

    log_ok "Certificate profile configuration attempted"
else
    log_warn "[DRY RUN] Would configure ENDUSER profile with CRL/AIA extensions"
fi

echo ""
log_info "For production, create dedicated profiles via EJBCA Admin Web:"
log_info "  ${EJBCA_URL}/ejbca/adminweb/ca/editcertificateprofiles/editcertificateprofiles.xhtml"
log_substep "IVF-PDFSigner-Profile -- PDF signing (digitalSignature, nonRepudiation, 3yr)"
log_substep "IVF-TSA-Profile -- TSA (digitalSignature, timeStamping EKU, 5yr)"
log_substep "IVF-TLS-Client-Profile -- mTLS Client (digitalSignature, clientAuth EKU, 2yr)"
log_substep "IVF-OCSP-Profile -- OCSP Responder (digitalSignature, OCSPSigning EKU, 2yr)"

# Set working profiles for enrollment (ENDUSER/EMPTY are CE defaults)
WORKING_CERT_PROFILE="ENDUSER"
WORKING_EE_PROFILE="EMPTY"


# ════════════════════════════════════════════════════════════
# PHASE 3: SoftHSM2 Token Setup in SignServer
# ════════════════════════════════════════════════════════════

log_step "Phase 3: SoftHSM2 Token Setup"

if [ "$DRY_RUN" = false ]; then
    # Verify SoftHSM2 is available
    log_info "Checking SoftHSM2 availability in SignServer container..."

    if exec_signserver test -f "$SOFTHSM_LIB" 2>/dev/null; then
        log_ok "SoftHSM2 library found: $SOFTHSM_LIB"

        # Ensure token directory exists with correct ownership (must be root to set up)
        exec_signserver_root bash -c "
            mkdir -p '$SOFTHSM_TOKEN_DIR'
            chown -R 10001:root '${SOFTHSM_TOKEN_DIR%/*}'
            chmod -R 755 '${SOFTHSM_TOKEN_DIR%/*}'
        " 2>/dev/null || true

        # Register SoftHSM library in SignServer deploy config (if not already)
        log_info "Registering SoftHSM2 library in SignServer deploy config..."
        exec_signserver_root bash -c "
            DEPLOY_FILE='/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties'
            if [ -f \"\$DEPLOY_FILE\" ] && ! grep -q 'SoftHSM' \"\$DEPLOY_FILE\" 2>/dev/null; then
                echo '' >> \"\$DEPLOY_FILE\"
                echo '# SoftHSM2 PKCS#11 provider' >> \"\$DEPLOY_FILE\"
                echo 'cryptotoken.p11.lib.83.name = SoftHSM' >> \"\$DEPLOY_FILE\"
                echo 'cryptotoken.p11.lib.83.file = $SOFTHSM_LIB' >> \"\$DEPLOY_FILE\"
            fi
            DEPLOY_FILE2='/opt/keyfactor/signserver/conf/signserver_deploy.properties'
            if [ -f \"\$DEPLOY_FILE2\" ] && ! grep -q 'SoftHSM' \"\$DEPLOY_FILE2\" 2>/dev/null; then
                echo '' >> \"\$DEPLOY_FILE2\"
                echo '# SoftHSM2 PKCS#11 provider' >> \"\$DEPLOY_FILE2\"
                echo 'cryptotoken.p11.lib.83.name = SoftHSM' >> \"\$DEPLOY_FILE2\"
                echo 'cryptotoken.p11.lib.83.file = $SOFTHSM_LIB' >> \"\$DEPLOY_FILE2\"
            fi
        " 2>/dev/null && log_ok "SoftHSM2 library registered in deploy config" || \
            log_warn "Could not register SoftHSM2 library (may already exist)"

        # Check if token already initialized (check as user 10001)
        TOKEN_EXISTS=false
        if exec_signserver softhsm2-util --show-slots 2>/dev/null | grep -q "$SOFTHSM_TOKEN_LABEL"; then
            TOKEN_EXISTS=true
            log_info "SoftHSM2 token '$SOFTHSM_TOKEN_LABEL' already exists"
        fi

        if [ "$TOKEN_EXISTS" = false ] || [ "$FORCE" = true ]; then
            if [ "$TOKEN_EXISTS" = true ] && [ "$FORCE" = true ]; then
                log_warn "Force mode: re-initializing SoftHSM2 token"
                SLOT_NUM=$(exec_signserver softhsm2-util --show-slots 2>/dev/null \
                    | grep -B2 "$SOFTHSM_TOKEN_LABEL" \
                    | grep "Slot " \
                    | head -1 \
                    | sed 's/.*Slot \([0-9]*\).*/\1/')
                if [ -n "$SLOT_NUM" ]; then
                    exec_signserver_root softhsm2-util --delete-token --serial "$SLOT_NUM" 2>/dev/null || true
                fi
            fi

            # Initialize token as ROOT (token dir is volume-mounted, needs root for initial creation)
            log_info "Initializing SoftHSM2 token: $SOFTHSM_TOKEN_LABEL"
            exec_signserver_root softhsm2-util \
                --init-token --free \
                --label "$SOFTHSM_TOKEN_LABEL" \
                --so-pin "$SOFTHSM_SO_PIN" \
                --pin "$SOFTHSM_PIN" 2>/dev/null && \
                log_ok "SoftHSM2 token '$SOFTHSM_TOKEN_LABEL' initialized" || \
                log_warn "Failed to initialize SoftHSM2 token"

            # Fix ownership so user 10001 can access the token
            exec_signserver_root bash -c "
                chown -R 10001:root '${SOFTHSM_TOKEN_DIR%/*}'
                chmod -R 755 '${SOFTHSM_TOKEN_DIR%/*}'
            " 2>/dev/null || true
            log_ok "Token directory ownership fixed for user 10001"

            # Verify token is visible to non-root user
            if exec_signserver softhsm2-util --show-slots 2>/dev/null | grep -q "$SOFTHSM_TOKEN_LABEL"; then
                log_ok "Token '$SOFTHSM_TOKEN_LABEL' visible to SignServer user"
            else
                log_warn "Token not visible to SignServer user -- PKCS#11 may not work"
            fi
        else
            log_ok "SoftHSM2 token '$SOFTHSM_TOKEN_LABEL' ready"
        fi

        # Keys will be generated via SignServer CLI during worker configuration (Phase 5)
        # pkcs11-tool generated keys are not compatible with SignServer's Java PKCS#11 provider

        USE_PKCS11=true
        log_ok "SoftHSM2 setup complete -- workers will use PKCS#11 crypto tokens"

    else
        log_warn "SoftHSM2 library not found at $SOFTHSM_LIB"
        log_warn "Falling back to P12 (KeystoreCryptoToken) mode"
        USE_PKCS11=false
    fi
else
    log_warn "[DRY RUN] Would initialize SoftHSM2 token and generate key pairs"
    USE_PKCS11=true  # Assume PKCS#11 for dry run
fi


# ════════════════════════════════════════════════════════════
# PHASE 4: End Entity Enrollment
# ════════════════════════════════════════════════════════════

log_step "Phase 4: End Entity Creation & Certificate Enrollment"

enroll_entity() {
    local ee_username="$1"
    local cn="$2"
    local ou="$3"
    local ca_name="$4"
    local cert_profile="$5"
    local ee_profile="$6"
    local key_alias="$7"

    log_substep "Enrolling: $cn (entity: $ee_username)"
    log_info "    DN: CN=$cn,O=IVF Healthcare,OU=$ou,C=VN"
    log_info "    CA: $ca_name | Profile: $cert_profile | EE Profile: $ee_profile"

    if [ "$DRY_RUN" = true ]; then
        log_warn "    [DRY RUN] Would create end entity and enroll certificate"
        return 0
    fi

    local key_file="${key_alias}.p12"

    # Create end entity (or reset if exists)
    run_ejbca ra addendentity \
        --username "$ee_username" \
        --dn "CN=$cn,O=IVF Healthcare,OU=$ou,C=VN" \
        --caname "$ca_name" \
        --type 1 \
        --token P12 \
        --password "$KEYSTORE_PASSWORD" \
        --certprofile "$cert_profile" \
        --eeprofile "$ee_profile" 2>/dev/null || {
        # Entity may already exist -- reset to NEW (status 10)
        run_ejbca ra setendentitystatus \
            "$ee_username" 10 2>/dev/null || true
        log_info "    End entity already exists -- reset for re-enrollment"
    }

    # Set clear password for batch enrollment
    run_ejbca ra setclearpwd \
        "$ee_username" "$KEYSTORE_PASSWORD" 2>/dev/null || true

    # Batch generate PKCS#12 keystore
    log_info "    Generating PKCS#12 keystore via EJBCA batch..."
    run_ejbca batch \
        --username "$ee_username" \
        -dir "$EJBCA_CERT_DIR" 2>/dev/null || {
        log_warn "    Batch enrollment failed -- generating self-signed P12 fallback"
        generate_selfsigned_p12 "$cn" "$ou" "$key_alias"
        return 0
    }

    local p12_path="${EJBCA_CERT_DIR}/${ee_username}.p12"

    # Verify keystore was generated
    if ! exec_ejbca test -f "$p12_path" 2>/dev/null; then
        log_warn "    PKCS#12 not found at $p12_path -- generating self-signed fallback"
        generate_selfsigned_p12 "$cn" "$ou" "$key_alias"
        return 0
    fi

    # Deploy P12 to SignServer container (used for P12 fallback and cert extraction)
    log_info "    Deploying P12 to SignServer container..."

    docker exec "$EJBCA_CONTAINER" cat "$p12_path" | \
        docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/_deploy_p12"

    docker exec "$SIGNSERVER_CONTAINER" bash -c "
        mkdir -p '$KEY_DIR'
        rm -f '${KEY_DIR}/${key_file}'
        cp /tmp/_deploy_p12 '${KEY_DIR}/${key_file}'
        rm -f /tmp/_deploy_p12
        chmod 400 '${KEY_DIR}/${key_file}'
        chown 10001:root '${KEY_DIR}/${key_file}' 2>/dev/null || true
    "

    # Normalize key alias to match expected name
    local current_alias
    current_alias=$(docker exec "$SIGNSERVER_CONTAINER" keytool -list \
        -keystore "${KEY_DIR}/${key_file}" \
        -storepass "$KEYSTORE_PASSWORD" -storetype PKCS12 2>/dev/null \
        | grep "PrivateKeyEntry" | head -1 | cut -d',' -f1 || echo "")

    if [ -n "$current_alias" ] && [ "$current_alias" != "$key_alias" ]; then
        docker exec "$SIGNSERVER_CONTAINER" bash -c "
            chmod 600 '${KEY_DIR}/${key_file}'
            keytool -changealias -keystore '${KEY_DIR}/${key_file}' \
                -storepass '$KEYSTORE_PASSWORD' -storetype PKCS12 \
                -alias '$current_alias' -destalias '$key_alias' 2>/dev/null || true
            chmod 400 '${KEY_DIR}/${key_file}'
        "
        log_info "    Key alias normalized: '$current_alias' -> '$key_alias'"
    fi

    # Copy keystore to host for backup
    docker cp "${SIGNSERVER_CONTAINER}:${KEY_DIR}/${key_file}" \
        "$CERTS_DIR/signers/${key_file}" 2>/dev/null || true

    log_ok "    Certificate enrolled and deployed: $key_file"
}

generate_selfsigned_p12() {
    local cn="$1"
    local ou="$2"
    local key_alias="$3"
    local key_file="${key_alias}.p12"

    log_info "    Generating self-signed PKCS#12 (fallback)..."
    docker exec "$SIGNSERVER_CONTAINER" bash -c "mkdir -p '$KEY_DIR'"

    docker exec "$SIGNSERVER_CONTAINER" keytool -genkeypair \
        -alias "$key_alias" \
        -keyalg RSA -keysize 4096 -sigalg SHA256withRSA \
        -validity 1095 \
        -dname "CN=${cn},O=IVF Healthcare,OU=${ou},C=VN" \
        -keystore "${KEY_DIR}/${key_file}" \
        -storetype PKCS12 \
        -storepass "$KEYSTORE_PASSWORD" \
        -keypass "$KEYSTORE_PASSWORD" 2>/dev/null && \
        log_ok "    Self-signed keystore created: $key_file" || \
        log_error "    Failed to create self-signed keystore: $key_file"

    docker exec "$SIGNSERVER_CONTAINER" bash -c "
        chmod 400 '${KEY_DIR}/${key_file}' 2>/dev/null || true
        chown 10001:root '${KEY_DIR}/${key_file}' 2>/dev/null || true
    "

    # Copy to host
    docker cp "${SIGNSERVER_CONTAINER}:${KEY_DIR}/${key_file}" \
        "$CERTS_DIR/signers/${key_file}" 2>/dev/null || true
}

# Determine which CA to issue from
# Prefer Sub-CA for end-entity certs, fall back to Root CA or ManagementCA
ISSUING_CA="$SUB_CA_NAME"
if [ "$DRY_RUN" = false ]; then
    if ! ca_exists "$SUB_CA_NAME" 2>/dev/null; then
        if ca_exists "$ROOT_CA_NAME" 2>/dev/null; then
            ISSUING_CA="$ROOT_CA_NAME"
            log_warn "Sub-CA not found -- issuing from Root CA"
        elif ca_exists "ManagementCA" 2>/dev/null; then
            ISSUING_CA="ManagementCA"
            log_warn "Custom CAs not found -- issuing from ManagementCA"
        else
            log_warn "No known CA found -- enrollment may use self-signed fallback"
        fi
    fi
fi
log_info "Issuing CA: $ISSUING_CA"

# ── Enroll PDF Signer certificates ──
echo ""
log_info "Enrolling PDF Signer certificates (${#PDF_WORKERS[@]} workers)..."

for worker_def in "${PDF_WORKERS[@]}"; do
    IFS='|' read -r wid wname wkeyalias wpurpose <<< "$worker_def"

    # Derive CN from worker purpose/name
    case "$wname" in
        PDFSigner)                    cn="IVF PDF Signer" ;;
        PDFSigner_technical)          cn="Ky Thuat Vien IVF" ;;
        PDFSigner_head_department)    cn="Truong Khoa IVF" ;;
        PDFSigner_doctor1)            cn="Bac Si IVF" ;;
        PDFSigner_admin)              cn="Quan Tri IVF" ;;
        *)                            cn="$wname" ;;
    esac

    enroll_entity \
        "ivf-signer-${wid}" \
        "$cn" \
        "Digital Signing" \
        "$ISSUING_CA" \
        "$WORKING_CERT_PROFILE" \
        "$WORKING_EE_PROFILE" \
        "$wkeyalias"
    echo ""
done

# ── Enroll TSA certificate ──
log_info "Enrolling TSA certificate..."
IFS='|' read -r tsa_id tsa_name tsa_keyalias tsa_purpose <<< "$TSA_WORKER"
enroll_entity \
    "ivf-tsa-${tsa_id}" \
    "IVF Timestamp Authority" \
    "Timestamp Authority" \
    "$ISSUING_CA" \
    "$WORKING_CERT_PROFILE" \
    "$WORKING_EE_PROFILE" \
    "$tsa_keyalias"

# Copy TSA keystore to tsa dir on host
if [ "$DRY_RUN" = false ]; then
    docker cp "${SIGNSERVER_CONTAINER}:${KEY_DIR}/${tsa_keyalias}.p12" \
        "$CERTS_DIR/tsa/${tsa_keyalias}.p12" 2>/dev/null || true
fi
echo ""

# ── Enroll mTLS API Client certificate ──
log_info "Enrolling mTLS API Client certificate..."
enroll_entity \
    "ivf-api-client" \
    "ivf-api-client" \
    "API Client" \
    "$ISSUING_CA" \
    "$WORKING_CERT_PROFILE" \
    "$WORKING_EE_PROFILE" \
    "api-client"

# Copy API client keystore to host
if [ "$DRY_RUN" = false ]; then
    docker cp "${SIGNSERVER_CONTAINER}:${KEY_DIR}/api-client.p12" \
        "$CERTS_DIR/api/api-client.p12" 2>/dev/null || true
    log_ok "API client keystore exported to certs/api/api-client.p12"
fi
echo ""

log_ok "All certificates enrolled"


# ════════════════════════════════════════════════════════════
# PHASE 5: SignServer Worker Configuration
# ════════════════════════════════════════════════════════════

if [ "$SKIP_WORKERS" = false ]; then

    log_step "Phase 5a: SignServer Worker Configuration -- PDF Signers"

    configure_pdf_worker_pkcs11() {
        local worker_id="$1"
        local worker_name="$2"
        local key_alias="$3"

        log_substep "Configuring Worker $worker_id: $worker_name (PKCS#11)"

        if [ "$DRY_RUN" = true ]; then
            log_warn "  [DRY RUN] Would configure PDFSigner worker $worker_id with PKCS#11"
            return 0
        fi

        # Remove conflicting P12 and old PKCS#11 properties
        run_signserver removeproperty "$worker_id" KEYSTOREPATH 2>/dev/null || true
        run_signserver removeproperty "$worker_id" KEYSTOREPASSWORD 2>/dev/null || true
        run_signserver removeproperty "$worker_id" KEYSTORETYPE 2>/dev/null || true
        run_signserver removeproperty "$worker_id" SHAREDLIBRARY 2>/dev/null || true
        run_signserver removeproperty "$worker_id" SET_PERMISSIONS 2>/dev/null || true

        # Set PKCS#11 worker properties
        run_signserver setproperty "$worker_id" NAME "$worker_name" 2>/dev/null || true
        run_signserver setproperty "$worker_id" TYPE PROCESSABLE 2>/dev/null || true
        run_signserver setproperty "$worker_id" IMPLEMENTATION_CLASS org.signserver.module.pdfsigner.PDFSigner 2>/dev/null || true
        run_signserver setproperty "$worker_id" CRYPTOTOKEN_IMPLEMENTATION_CLASS org.signserver.server.cryptotokens.PKCS11CryptoToken 2>/dev/null || true
        run_signserver setproperty "$worker_id" AUTHTYPE NOAUTH 2>/dev/null || true
        run_signserver setproperty "$worker_id" SHAREDLIBRARYNAME SoftHSM 2>/dev/null || true
        run_signserver setproperty "$worker_id" SLOTLABELTYPE SLOT_LABEL 2>/dev/null || true
        run_signserver setproperty "$worker_id" SLOTLABELVALUE "$SOFTHSM_TOKEN_LABEL" 2>/dev/null || true
        run_signserver setproperty "$worker_id" PIN "$SOFTHSM_PIN" 2>/dev/null || true
        run_signserver setproperty "$worker_id" DEFAULTKEY "$key_alias" 2>/dev/null || true

        # PDF signing properties
        run_signserver setproperty "$worker_id" CERTIFICATION_LEVEL NOT_CERTIFIED 2>/dev/null || true
        run_signserver setproperty "$worker_id" ADD_VISIBLE_SIGNATURE false 2>/dev/null || true
        run_signserver setproperty "$worker_id" REFUSE_DOUBLE_INDIRECT_OBJECTS true 2>/dev/null || true
        run_signserver setproperty "$worker_id" REASON "Xac nhan bao cao y te IVF" 2>/dev/null || true
        run_signserver setproperty "$worker_id" LOCATION "IVF Clinic" 2>/dev/null || true
        run_signserver setproperty "$worker_id" TSA_WORKER "TimeStampSigner" 2>/dev/null || true
        run_signserver setproperty "$worker_id" EMBED_CRL true 2>/dev/null || true
        run_signserver setproperty "$worker_id" DIGESTALGORITHM SHA256 2>/dev/null || true

        # Reload worker to pick up PKCS#11 config
        run_signserver reload "$worker_id" 2>/dev/null || true

        # Initial activation to connect to SoftHSM token (may fail if no key yet — that's OK)
        run_signserver activatecryptotoken "$worker_id" "$SOFTHSM_PIN" 2>/dev/null || true

        # Generate RSA 4096 key inside SoftHSM via SignServer
        log_info "  Generating RSA 4096 key '$key_alias' via SignServer..."
        run_signserver generatekey "$worker_id" -keyalg RSA -keyspec 4096 -alias "$key_alias" 2>/dev/null && \
            log_ok "  Key '$key_alias' generated" || \
            log_warn "  Key generation failed (key may already exist)"

        # Reload and re-activate after key generation
        run_signserver reload "$worker_id" 2>/dev/null || true
        local activate_result
        activate_result=$(run_signserver activatecryptotoken "$worker_id" "$SOFTHSM_PIN" 2>&1) || true

        if ! echo "$activate_result" | grep -qi "successful"; then
            log_warn "Worker $worker_id activation failed after key gen: $activate_result"
            return 1
        fi

        # Derive CN from worker name for cert
        local cn
        case "$worker_name" in
            PDFSigner)                    cn="IVF PDF Signer" ;;
            PDFSigner_technical)          cn="Ky Thuat Vien IVF" ;;
            PDFSigner_head_department)    cn="Truong Khoa IVF" ;;
            PDFSigner_doctor1)            cn="Bac Si IVF" ;;
            PDFSigner_admin)              cn="Quan Tri IVF" ;;
            *)                            cn="$worker_name" ;;
        esac

        # Generate CSR from PKCS#11 key
        log_info "  Generating CSR..."
        local csr_file="/tmp/worker_${worker_id}_csr.pem"
        run_signserver generatecertreq "$worker_id" \
            "CN=${cn},O=IVF Healthcare,OU=Digital Signing,C=VN" \
            SHA256WithRSA "$csr_file" 2>/dev/null || {
            log_warn "  CSR generation failed"
            return 1
        }

        # Reuse existing EJBCA end entity from Phase 4 enrollment (reset to NEW for re-signing)
        local ee_username="ivf-signer-${worker_id}"
        log_info "  Reusing EJBCA end entity: $ee_username"
        run_ejbca ra setendentitystatus "$ee_username" 10 2>/dev/null || true
        run_ejbca ra setclearpwd "$ee_username" "$KEYSTORE_PASSWORD" 2>/dev/null || true

        # Sign CSR with EJBCA
        log_info "  Signing CSR with EJBCA ($ISSUING_CA)..."
        local signed_cert="/tmp/worker_${worker_id}_signed.pem"

        # Copy CSR from SignServer to EJBCA
        docker exec "$SIGNSERVER_CONTAINER" cat "$csr_file" | \
            docker exec -i "$EJBCA_CONTAINER" bash -c "cat > $csr_file"

        run_ejbca createcert \
            --username "$ee_username" \
            --password "$KEYSTORE_PASSWORD" \
            -c "$csr_file" \
            -f "$signed_cert" 2>/dev/null || {
            log_warn "  EJBCA cert signing failed"
            return 1
        }

        # Extract PEM cert (strip header lines before BEGIN CERTIFICATE) and copy to SignServer
        docker exec "$EJBCA_CONTAINER" bash -c \
            "sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert" | \
            docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/worker_${worker_id}_cert.pem"

        # Upload signer certificate
        log_info "  Uploading signer certificate..."
        run_signserver uploadsignercertificate "$worker_id" GLOB \
            "/tmp/worker_${worker_id}_cert.pem" 2>/dev/null || {
            log_warn "  Certificate upload failed"
            return 1
        }

        # Build and upload certificate chain (signer + Sub-CA + Root CA)
        log_info "  Uploading certificate chain..."
        docker exec "$EJBCA_CONTAINER" bash -c "
            sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert
            cat /tmp/sub-ca.pem 2>/dev/null || true
            cat /tmp/root-ca.pem 2>/dev/null || true
        " | docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/worker_${worker_id}_chain.pem"

        run_signserver uploadsignercertificatechain "$worker_id" GLOB \
            "/tmp/worker_${worker_id}_chain.pem" 2>/dev/null || \
            log_warn "  Certificate chain upload failed"

        # Final reload
        run_signserver reload "$worker_id" 2>/dev/null || true

        # Verify final status
        local final_status
        final_status=$(run_signserver getstatus brief "$worker_id" 2>&1) || true
        if echo "$final_status" | grep -q "Worker status : Active"; then
            log_ok "Worker $worker_id ($worker_name) -- PKCS#11 Active with EJBCA-signed cert"
            return 0
        else
            log_warn "Worker $worker_id not fully active after PKCS#11 setup"
            echo "$final_status" | grep "Errors:" -A5 || true
            return 1
        fi
    }

    configure_pdf_worker_p12() {
        local worker_id="$1"
        local worker_name="$2"
        local key_alias="$3"
        local key_file="${key_alias}.p12"

        log_substep "Configuring Worker $worker_id: $worker_name (P12 fallback)"

        if [ "$DRY_RUN" = true ]; then
            log_warn "  [DRY RUN] Would configure PDFSigner worker $worker_id with P12"
            return 0
        fi

        exec_signserver bash -c "cat > /tmp/worker_${worker_id}.properties << 'PROPEOF'
WORKER${worker_id}.TYPE = PROCESSABLE
WORKER${worker_id}.IMPLEMENTATION_CLASS = org.signserver.module.pdfsigner.PDFSigner
WORKER${worker_id}.CRYPTOTOKEN_IMPLEMENTATION_CLASS = org.signserver.server.cryptotokens.KeystoreCryptoToken
WORKER${worker_id}.NAME = ${worker_name}
WORKER${worker_id}.AUTHTYPE = NOAUTH
WORKER${worker_id}.KEYSTORETYPE = PKCS12
WORKER${worker_id}.KEYSTOREPATH = ${KEY_DIR}/${key_file}
WORKER${worker_id}.DEFAULTKEY = ${key_alias}
WORKER${worker_id}.KEYSTOREPASSWORD = ${KEYSTORE_PASSWORD}
PROPEOF"

        run_signserver setproperties "/tmp/worker_${worker_id}.properties" 2>/dev/null || true

        # Set additional PDF signing properties
        run_signserver setproperty "$worker_id" CERTIFICATION_LEVEL NOT_CERTIFIED 2>/dev/null || true
        run_signserver setproperty "$worker_id" ADD_VISIBLE_SIGNATURE false 2>/dev/null || true
        run_signserver setproperty "$worker_id" REFUSE_DOUBLE_INDIRECT_OBJECTS true 2>/dev/null || true
        run_signserver setproperty "$worker_id" REASON "Xac nhan bao cao y te IVF" 2>/dev/null || true
        run_signserver setproperty "$worker_id" LOCATION "IVF Clinic" 2>/dev/null || true
        run_signserver setproperty "$worker_id" TSA_WORKER "TimeStampSigner" 2>/dev/null || true
        run_signserver setproperty "$worker_id" EMBED_CRL true 2>/dev/null || true
        run_signserver setproperty "$worker_id" SET_PERMISSIONS "0xfffff3c4" 2>/dev/null || true
        run_signserver setproperty "$worker_id" DIGESTALGORITHM SHA256 2>/dev/null || true

        run_signserver reload "$worker_id" 2>/dev/null || true

        local activate_result
        activate_result=$(run_signserver activatecryptotoken "$worker_id" "$KEYSTORE_PASSWORD" 2>&1) || true

        if echo "$activate_result" | grep -qi "successful\|activated"; then
            log_ok "Worker $worker_id ($worker_name) -- P12 configured and activated"
        else
            log_warn "Worker $worker_id P12 activation result: $activate_result"
            log_info "Worker may need manual activation via Admin Web"
        fi
    }

    for worker_def in "${PDF_WORKERS[@]}"; do
        IFS='|' read -r wid wname wkeyalias wpurpose <<< "$worker_def"

        if [ "$USE_PKCS11" = true ]; then
            # Try PKCS#11 first, fall back to P12 on failure
            configure_pdf_worker_pkcs11 "$wid" "$wname" "$wkeyalias" || {
                log_warn "PKCS#11 failed for Worker $wid -- falling back to P12"
                configure_pdf_worker_p12 "$wid" "$wname" "$wkeyalias"
            }
        else
            configure_pdf_worker_p12 "$wid" "$wname" "$wkeyalias"
        fi
        echo ""
    done


    # ── Phase 5b: Configure TSA Worker ──────────────────────
    log_step "Phase 5b: SignServer Worker Configuration -- TimeStampSigner"

    IFS='|' read -r tsa_id tsa_name tsa_keyalias tsa_purpose <<< "$TSA_WORKER"

    configure_tsa_worker_pkcs11() {
        log_substep "Configuring Worker $tsa_id: $tsa_name (PKCS#11)"

        if [ "$DRY_RUN" = true ]; then
            log_warn "[DRY RUN] Would configure TSA worker $tsa_id with PKCS#11"
            return 0
        fi

        # Remove conflicting P12 and old PKCS#11 properties
        run_signserver removeproperty "$tsa_id" KEYSTOREPATH 2>/dev/null || true
        run_signserver removeproperty "$tsa_id" KEYSTOREPASSWORD 2>/dev/null || true
        run_signserver removeproperty "$tsa_id" KEYSTORETYPE 2>/dev/null || true
        run_signserver removeproperty "$tsa_id" SHAREDLIBRARY 2>/dev/null || true
        run_signserver removeproperty "$tsa_id" SET_PERMISSIONS 2>/dev/null || true

        # Set PKCS#11 worker properties
        run_signserver setproperty "$tsa_id" NAME "$tsa_name" 2>/dev/null || true
        run_signserver setproperty "$tsa_id" TYPE PROCESSABLE 2>/dev/null || true
        run_signserver setproperty "$tsa_id" IMPLEMENTATION_CLASS org.signserver.module.tsa.TimeStampSigner 2>/dev/null || true
        run_signserver setproperty "$tsa_id" CRYPTOTOKEN_IMPLEMENTATION_CLASS org.signserver.server.cryptotokens.PKCS11CryptoToken 2>/dev/null || true
        run_signserver setproperty "$tsa_id" AUTHTYPE NOAUTH 2>/dev/null || true
        run_signserver setproperty "$tsa_id" SHAREDLIBRARYNAME SoftHSM 2>/dev/null || true
        run_signserver setproperty "$tsa_id" SLOTLABELTYPE SLOT_LABEL 2>/dev/null || true
        run_signserver setproperty "$tsa_id" SLOTLABELVALUE "$SOFTHSM_TOKEN_LABEL" 2>/dev/null || true
        run_signserver setproperty "$tsa_id" PIN "$SOFTHSM_PIN" 2>/dev/null || true
        run_signserver setproperty "$tsa_id" DEFAULTKEY "$tsa_keyalias" 2>/dev/null || true

        # TSA-specific properties
        run_signserver setproperty "$tsa_id" DEFAULTTSAPOLICYOID "1.2.3.4.1" 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ACCEPTANYPOLICY true 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ACCURACYMICROS 500 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ORDERING false 2>/dev/null || true
        run_signserver setproperty "$tsa_id" INCLUDESTATUSSTRING true 2>/dev/null || true

        # Reload worker to pick up PKCS#11 config
        run_signserver reload "$tsa_id" 2>/dev/null || true

        # Initial activation to connect to SoftHSM token (may fail if no key yet)
        run_signserver activatecryptotoken "$tsa_id" "$SOFTHSM_PIN" 2>/dev/null || true

        # Generate RSA 4096 key inside SoftHSM via SignServer
        log_info "  Generating RSA 4096 key '$tsa_keyalias' via SignServer..."
        run_signserver generatekey "$tsa_id" -keyalg RSA -keyspec 4096 -alias "$tsa_keyalias" 2>/dev/null && \
            log_ok "  Key '$tsa_keyalias' generated" || \
            log_warn "  Key generation failed (key may already exist)"

        # Reload and re-activate after key generation
        run_signserver reload "$tsa_id" 2>/dev/null || true
        local tsa_activate_result
        tsa_activate_result=$(run_signserver activatecryptotoken "$tsa_id" "$SOFTHSM_PIN" 2>&1) || true

        if ! echo "$tsa_activate_result" | grep -qi "successful"; then
            log_warn "TSA Worker $tsa_id activation failed after key gen: $tsa_activate_result"
            return 1
        fi

        # Generate CSR from PKCS#11 key
        local cn="IVF Timestamp Authority"
        log_info "  Generating CSR..."
        local csr_file="/tmp/worker_${tsa_id}_csr.pem"
        run_signserver generatecertreq "$tsa_id" \
            "CN=${cn},O=IVF Healthcare,OU=Digital Signing,C=VN" \
            SHA256WithRSA "$csr_file" 2>/dev/null || {
            log_warn "  CSR generation failed"
            return 1
        }

        # Reuse existing EJBCA end entity from Phase 4 enrollment
        local ee_username="ivf-tsa-${tsa_id}"
        log_info "  Reusing EJBCA end entity: $ee_username"
        run_ejbca ra setendentitystatus "$ee_username" 10 2>/dev/null || true
        run_ejbca ra setclearpwd "$ee_username" "$KEYSTORE_PASSWORD" 2>/dev/null || true

        # Sign CSR with EJBCA
        log_info "  Signing CSR with EJBCA ($ISSUING_CA)..."
        local signed_cert="/tmp/worker_${tsa_id}_signed.pem"

        # Copy CSR from SignServer to EJBCA
        docker exec "$SIGNSERVER_CONTAINER" cat "$csr_file" | \
            docker exec -i "$EJBCA_CONTAINER" bash -c "cat > $csr_file"

        run_ejbca createcert \
            --username "$ee_username" \
            --password "$KEYSTORE_PASSWORD" \
            -c "$csr_file" \
            -f "$signed_cert" 2>/dev/null || {
            log_warn "  EJBCA cert signing failed"
            return 1
        }

        # Extract PEM cert (strip header lines before BEGIN CERTIFICATE) and copy to SignServer
        docker exec "$EJBCA_CONTAINER" bash -c \
            "sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert" | \
            docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/worker_${tsa_id}_cert.pem"

        # Upload signer certificate
        log_info "  Uploading signer certificate..."
        run_signserver uploadsignercertificate "$tsa_id" GLOB \
            "/tmp/worker_${tsa_id}_cert.pem" 2>/dev/null || {
            log_warn "  Certificate upload failed"
            return 1
        }

        # Build and upload certificate chain (signer + Sub-CA + Root CA)
        log_info "  Uploading certificate chain..."
        docker exec "$EJBCA_CONTAINER" bash -c "
            sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert
            cat /tmp/sub-ca.pem 2>/dev/null || true
            cat /tmp/root-ca.pem 2>/dev/null || true
        " | docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/worker_${tsa_id}_chain.pem"

        run_signserver uploadsignercertificatechain "$tsa_id" GLOB \
            "/tmp/worker_${tsa_id}_chain.pem" 2>/dev/null || \
            log_warn "  Certificate chain upload failed"

        # Final reload
        run_signserver reload "$tsa_id" 2>/dev/null || true

        # Verify final status
        local final_status
        final_status=$(run_signserver getstatus brief "$tsa_id" 2>&1) || true
        if echo "$final_status" | grep -q "Worker status : Active"; then
            log_ok "TSA Worker $tsa_id ($tsa_name) -- PKCS#11 Active with EJBCA-signed cert"
            return 0
        else
            log_warn "TSA Worker $tsa_id not fully active after PKCS#11 setup"
            echo "$final_status" | grep "Errors:" -A5 || true
            return 1
        fi
    }

    configure_tsa_worker_p12() {
        local tsa_keyfile="${tsa_keyalias}.p12"

        log_substep "Configuring Worker $tsa_id: $tsa_name (P12 fallback)"

        if [ "$DRY_RUN" = true ]; then
            log_warn "[DRY RUN] Would configure TSA worker $tsa_id with P12"
            return 0
        fi

        exec_signserver bash -c "cat > /tmp/worker_tsa.properties << 'PROPEOF'
WORKER${tsa_id}.TYPE = PROCESSABLE
WORKER${tsa_id}.IMPLEMENTATION_CLASS = org.signserver.module.tsa.TimeStampSigner
WORKER${tsa_id}.CRYPTOTOKEN_IMPLEMENTATION_CLASS = org.signserver.server.cryptotokens.KeystoreCryptoToken
WORKER${tsa_id}.NAME = ${tsa_name}
WORKER${tsa_id}.AUTHTYPE = NOAUTH
WORKER${tsa_id}.KEYSTORETYPE = PKCS12
WORKER${tsa_id}.KEYSTOREPATH = ${KEY_DIR}/${tsa_keyfile}
WORKER${tsa_id}.DEFAULTKEY = ${tsa_keyalias}
WORKER${tsa_id}.KEYSTOREPASSWORD = ${KEYSTORE_PASSWORD}
PROPEOF"

        run_signserver setproperties "/tmp/worker_tsa.properties" 2>/dev/null || true

        run_signserver setproperty "$tsa_id" DEFAULTTSAPOLICYOID "1.2.3.4.1" 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ACCEPTANYPOLICY true 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ACCURACYMICROS 500 2>/dev/null || true
        run_signserver setproperty "$tsa_id" ORDERING false 2>/dev/null || true
        run_signserver setproperty "$tsa_id" INCLUDESTATUSSTRING true 2>/dev/null || true

        run_signserver reload "$tsa_id" 2>/dev/null || true

        local tsa_activate_result
        tsa_activate_result=$(run_signserver activatecryptotoken "$tsa_id" "$KEYSTORE_PASSWORD" 2>&1) || true

        if echo "$tsa_activate_result" | grep -qi "successful\|activated"; then
            log_ok "TSA Worker $tsa_id ($tsa_name) -- P12 configured and activated"
        else
            log_warn "TSA Worker $tsa_id P12 activation result: $tsa_activate_result"
        fi
    }

    if [ "$USE_PKCS11" = true ]; then
        configure_tsa_worker_pkcs11 || {
            log_warn "PKCS#11 failed for TSA Worker -- falling back to P12"
            configure_tsa_worker_p12
        }
    else
        configure_tsa_worker_p12
    fi

else
    log_step "Phase 5: SignServer Workers -- SKIPPED (--skip-workers)"
fi


# ════════════════════════════════════════════════════════════
# PHASE 6: OCSP Responder Configuration
# ════════════════════════════════════════════════════════════

log_step "Phase 6: OCSP Responder Configuration"

if [ "$DRY_RUN" = true ]; then
    log_warn "[DRY RUN] Would configure OCSP responder"
else
    log_info "Configuring EJBCA OCSP responder..."

    # Enable OCSP protocol if not already enabled
    run_ejbca config protocols enable \
        --name "OCSP" 2>/dev/null && \
        log_ok "OCSP protocol enabled" || \
        log_info "OCSP protocol already enabled or not available via CLI"

    log_info "OCSP is served at: ${OCSP_URL}"
    log_info "EJBCA CE uses the CA signing key for OCSP responses by default."
    log_info "For a dedicated OCSP responder, configure via Admin Web:"
    log_info "  ${EJBCA_URL}/ejbca/adminweb/sysconfig/ocsp/ocspserviceconfiguration.xhtml"

    # Verify OCSP endpoint is reachable
    ocsp_check=$(curl -sk -o /dev/null -w "%{http_code}" \
        "${OCSP_URL}" 2>/dev/null || echo "000")
    if [ "$ocsp_check" = "200" ] || [ "$ocsp_check" = "405" ]; then
        log_ok "OCSP endpoint is reachable (HTTP $ocsp_check)"
    else
        log_warn "OCSP endpoint returned HTTP $ocsp_check -- may need manual configuration"
    fi
fi


# ════════════════════════════════════════════════════════════
# PHASE 7: Multi-Tenant Sub-CA (optional)
# ════════════════════════════════════════════════════════════

if [ -n "$TENANT_ID" ]; then
    log_step "Phase 7: Tenant-Specific Sub-CA -- $TENANT_ID"

    TENANT_CA_NAME="IVF-Tenant-${TENANT_ID}-SubCA"
    TENANT_CA_DN="CN=IVF Tenant ${TENANT_ID} Signing CA,O=IVF Healthcare,OU=Tenant ${TENANT_ID},C=VN"
    TENANT_CA_VALIDITY="1826"  # 5 years in days

    if ca_exists "$TENANT_CA_NAME" && [ "$FORCE" = false ]; then
        log_warn "Tenant CA '$TENANT_CA_NAME' already exists -- skipping (use --force to recreate)"
    else
        log_info "Creating Tenant Sub-CA: $TENANT_CA_NAME"
        log_info "  Subject DN: $TENANT_CA_DN"
        log_info "  Signed by:  $ROOT_CA_NAME"
        log_info "  Validity:   $TENANT_CA_VALIDITY days (5 years)"

        # Get Root CA ID
        TENANT_ROOT_CA_ID=""
        if [ "$DRY_RUN" = false ]; then
            TENANT_ROOT_CA_ID=$(get_ca_id "$ROOT_CA_NAME")
            if [ -z "$TENANT_ROOT_CA_ID" ]; then
                log_error "Could not determine Root CA ID for tenant Sub-CA"
                log_warn "Skipping tenant CA creation"
            else
                log_info "  Root CA ID: $TENANT_ROOT_CA_ID"

                run_ejbca ca init \
                    --caname "$TENANT_CA_NAME" \
                    --dn "$TENANT_CA_DN" \
                    --tokenType "soft" \
                    --tokenPass "null" \
                    --keytype "RSA" \
                    --keyspec "4096" \
                    -v "$TENANT_CA_VALIDITY" \
                    -s "SHA256WithRSA" \
                    --signedby "$TENANT_ROOT_CA_ID" \
                    --policy "null" 2>/dev/null && \
                    log_ok "Tenant CA '$TENANT_CA_NAME' created successfully" || \
                    log_warn "Tenant CA creation returned non-zero (may already exist)"

                if ca_exists "$TENANT_CA_NAME"; then
                    log_ok "Verified: Tenant CA '$TENANT_CA_NAME' exists in EJBCA"
                fi
            fi
        else
            log_warn "[DRY RUN] Would create tenant Sub-CA: $TENANT_CA_NAME"
        fi
    fi

    # Export tenant CA certificate
    if [ "$DRY_RUN" = false ] && ca_exists "$TENANT_CA_NAME" 2>/dev/null; then
        log_info "Exporting Tenant CA certificate..."
        mkdir -p "$CERTS_DIR/tenants/${TENANT_ID}"

        run_ejbca ca getcacert \
            --caname "$TENANT_CA_NAME" \
            -f "/tmp/tenant-${TENANT_ID}-ca.pem" 2>/dev/null || true

        docker cp "${EJBCA_CONTAINER}:/tmp/tenant-${TENANT_ID}-ca.pem" \
            "$CERTS_DIR/tenants/${TENANT_ID}/ca.pem" 2>/dev/null && \
            log_ok "Tenant CA cert exported to certs/tenants/${TENANT_ID}/ca.pem" || \
            log_warn "Could not export Tenant CA cert"

        # Build tenant CA chain
        if [ -f "$CERTS_DIR/tenants/${TENANT_ID}/ca.pem" ] && [ -f "$CERTS_DIR/ca/root-ca.pem" ]; then
            cat "$CERTS_DIR/tenants/${TENANT_ID}/ca.pem" "$CERTS_DIR/ca/root-ca.pem" \
                > "$CERTS_DIR/tenants/${TENANT_ID}/ca-chain.pem"
            log_ok "Tenant CA chain exported to certs/tenants/${TENANT_ID}/ca-chain.pem"
        fi
    fi

    # Enroll tenant-specific signer certificate
    if [ "$DRY_RUN" = false ] && ca_exists "$TENANT_CA_NAME" 2>/dev/null; then
        log_info "Enrolling tenant-specific signer certificate..."
        enroll_entity \
            "ivf-tenant-${TENANT_ID}-signer" \
            "IVF Tenant ${TENANT_ID} Signer" \
            "Tenant ${TENANT_ID}" \
            "$TENANT_CA_NAME" \
            "$WORKING_CERT_PROFILE" \
            "$WORKING_EE_PROFILE" \
            "tenant-${TENANT_ID}-signer"

        # Copy tenant keystore to tenant dir
        docker cp "${SIGNSERVER_CONTAINER}:${KEY_DIR}/tenant-${TENANT_ID}-signer.p12" \
            "$CERTS_DIR/tenants/${TENANT_ID}/signer.p12" 2>/dev/null || true
    elif [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would enroll tenant-specific signer certificate"
    fi

else
    log_step "Phase 7: Multi-Tenant Sub-CA -- SKIPPED (no --tenant flag)"
    log_info "To create a tenant Sub-CA: $0 --tenant <tenant-id>"
fi


# ════════════════════════════════════════════════════════════
# PHASE 8: Final Verification
# ════════════════════════════════════════════════════════════

log_step "Phase 8: Final Verification"

if [ "$DRY_RUN" = true ]; then
    log_warn "[DRY RUN] Would verify all components"
else
    VERIFY_PASS=0
    VERIFY_FAIL=0

    # Verify CAs exist
    log_info "Verifying CAs..."
    for ca in "$ROOT_CA_NAME" "$SUB_CA_NAME"; do
        if ca_exists "$ca"; then
            log_ok "CA exists: $ca"
            VERIFY_PASS=$((VERIFY_PASS + 1))
        else
            log_error "CA missing: $ca"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    done

    # Verify tenant CA if applicable
    if [ -n "$TENANT_ID" ]; then
        if ca_exists "IVF-Tenant-${TENANT_ID}-SubCA"; then
            log_ok "Tenant CA exists: IVF-Tenant-${TENANT_ID}-SubCA"
            VERIFY_PASS=$((VERIFY_PASS + 1))
        else
            log_error "Tenant CA missing: IVF-Tenant-${TENANT_ID}-SubCA"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    fi

    # Verify keystores exist in SignServer
    log_info "Verifying keystores in SignServer..."
    ALL_KEYSTORES=()
    for worker_def in "${PDF_WORKERS[@]}"; do
        IFS='|' read -r _ _ wkeyalias _ <<< "$worker_def"
        ALL_KEYSTORES+=("${wkeyalias}.p12")
    done
    IFS='|' read -r _ _ tsa_keyalias _ <<< "$TSA_WORKER"
    ALL_KEYSTORES+=("${tsa_keyalias}.p12" "api-client.p12")

    for ks in "${ALL_KEYSTORES[@]}"; do
        if exec_signserver test -f "${KEY_DIR}/${ks}" 2>/dev/null; then
            log_ok "Keystore present: $ks"
            VERIFY_PASS=$((VERIFY_PASS + 1))
        else
            log_error "Keystore missing: $ks"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    done

    # Verify SoftHSM2 token if PKCS#11 mode
    if [ "$USE_PKCS11" = true ]; then
        log_info "Verifying SoftHSM2 token..."
        if exec_signserver softhsm2-util --show-slots 2>/dev/null | grep -q "$SOFTHSM_TOKEN_LABEL"; then
            log_ok "SoftHSM2 token '$SOFTHSM_TOKEN_LABEL' is present"
            VERIFY_PASS=$((VERIFY_PASS + 1))

            # Count keys (use pkcs11-tool if available, otherwise just log token presence)
            if exec_signserver bash -c "command -v pkcs11-tool" >/dev/null 2>&1; then
                KEY_COUNT=$(exec_signserver pkcs11-tool \
                    --module "$SOFTHSM_LIB" --login --pin "$SOFTHSM_PIN" \
                    --token-label "$SOFTHSM_TOKEN_LABEL" \
                    --list-objects --type privkey 2>/dev/null \
                    | grep -c "Private Key Object" || echo "0")
                log_info "SoftHSM2 contains $KEY_COUNT private keys"
            else
                log_info "SoftHSM2 token present (pkcs11-tool not available for key count)"
            fi
        else
            log_warn "SoftHSM2 token '$SOFTHSM_TOKEN_LABEL' not found"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    fi

    # Verify SignServer worker status
    if [ "$SKIP_WORKERS" = false ]; then
        log_info "Verifying SignServer worker status..."
        worker_status=$(run_signserver getstatus brief all 2>&1) || true

        for worker_def in "${PDF_WORKERS[@]}"; do
            IFS='|' read -r wid wname _ _ <<< "$worker_def"
            if echo "$worker_status" | grep -q "$wname"; then
                if echo "$worker_status" | grep -A2 "$wname" | grep -qi "Active"; then
                    log_ok "Worker $wid ($wname) -- Active"
                    VERIFY_PASS=$((VERIFY_PASS + 1))
                else
                    log_warn "Worker $wid ($wname) -- present but not Active"
                    VERIFY_FAIL=$((VERIFY_FAIL + 1))
                fi
            else
                log_error "Worker $wid ($wname) -- not found in SignServer"
                VERIFY_FAIL=$((VERIFY_FAIL + 1))
            fi
        done

        # TSA worker
        IFS='|' read -r tsa_id tsa_name _ _ <<< "$TSA_WORKER"
        if echo "$worker_status" | grep -q "$tsa_name"; then
            if echo "$worker_status" | grep -A2 "$tsa_name" | grep -qi "Active"; then
                log_ok "Worker $tsa_id ($tsa_name) -- Active"
                VERIFY_PASS=$((VERIFY_PASS + 1))
            else
                log_warn "Worker $tsa_id ($tsa_name) -- present but not Active"
                VERIFY_FAIL=$((VERIFY_FAIL + 1))
            fi
        else
            log_error "Worker $tsa_id ($tsa_name) -- not found in SignServer"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    fi

    # Test PDF signing
    log_info "Testing PDF signing..."
    TEST_PDF="/tmp/test-enterprise-pki.pdf"
    SIGNED_PDF="/tmp/test-enterprise-pki-signed.pdf"

    cat > "$TEST_PDF" << 'PDFEOF'
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Test PDF) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000266 00000 n
trailer
<< /Size 5 /Root 1 0 R >>
startxref
362
%%EOF
PDFEOF

    HTTP_CODE=$(curl -sk -o "$SIGNED_PDF" -w "%{http_code}" \
        -X POST "${SIGNSERVER_HTTP_URL}/signserver/process" \
        -F "workerName=PDFSigner" \
        -F "data=@${TEST_PDF}" 2>/dev/null || echo "000")

    if [ "$HTTP_CODE" = "200" ]; then
        SIGNED_SIZE=$(stat -c%s "$SIGNED_PDF" 2>/dev/null || stat -f%z "$SIGNED_PDF" 2>/dev/null || echo "0")
        log_ok "PDF signing test passed (${SIGNED_SIZE} bytes signed output)"
        VERIFY_PASS=$((VERIFY_PASS + 1))
    else
        log_warn "PDF signing test returned HTTP ${HTTP_CODE}"
        VERIFY_FAIL=$((VERIFY_FAIL + 1))
    fi

    # Cleanup test files
    rm -f "$TEST_PDF" "$SIGNED_PDF"

    # Verify CA chain on host
    log_info "Verifying exported certificates on host..."
    for cert_file in "ca-chain.pem" "ca/root-ca.pem" "ca/sub-ca.pem" "ca/ca.pem"; do
        if [ -f "$CERTS_DIR/$cert_file" ]; then
            log_ok "Exported: certs/$cert_file"
            VERIFY_PASS=$((VERIFY_PASS + 1))
        else
            log_warn "Missing: certs/$cert_file"
            VERIFY_FAIL=$((VERIFY_FAIL + 1))
        fi
    done

    echo ""
    if [ "$VERIFY_FAIL" -eq 0 ]; then
        log_ok "All verification checks passed ($VERIFY_PASS/$VERIFY_PASS)"
    else
        VERIFY_TOTAL=$((VERIFY_PASS + VERIFY_FAIL))
        log_warn "Verification: $VERIFY_PASS passed, $VERIFY_FAIL failed (of $VERIFY_TOTAL)"
    fi
fi


# ════════════════════════════════════════════════════════════
# Summary
# ════════════════════════════════════════════════════════════

echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║                 Enterprise PKI Setup Complete               ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "  ${GREEN}Certificate Authorities:${NC}"
echo "    Root CA:    $ROOT_CA_NAME"
echo "    Sub-CA:     $SUB_CA_NAME"
if [ -n "$TENANT_ID" ]; then
echo "    Tenant CA:  IVF-Tenant-${TENANT_ID}-SubCA"
fi
echo ""
echo -e "  ${GREEN}Crypto Token Mode:${NC}"
if [ "$USE_PKCS11" = true ]; then
echo "    Primary:    PKCS#11 (SoftHSM2, token: $SOFTHSM_TOKEN_LABEL)"
echo "    Fallback:   P12 (KeystoreCryptoToken)"
echo "    Library:    $SOFTHSM_LIB"
else
echo "    Mode:       P12 (KeystoreCryptoToken) -- SoftHSM2 not available"
fi
echo ""
echo -e "  ${GREEN}CA Certificates:${NC}"
echo "    Root CA:    certs/ca/root-ca.pem (also: certs/ca/ca.pem)"
echo "    Sub-CA:     certs/ca/sub-ca.pem"
echo "    CA Chain:   certs/ca-chain.pem"
echo ""
echo -e "  ${GREEN}PDF Signer Workers:${NC}"
for worker_def in "${PDF_WORKERS[@]}"; do
    IFS='|' read -r wid wname wkeyalias wpurpose <<< "$worker_def"
    printf "    Worker %-4s %-30s key: %-28s %s\n" "$wid" "$wname" "$wkeyalias" "($wpurpose)"
done
echo ""
echo -e "  ${GREEN}TSA Worker:${NC}"
IFS='|' read -r tsa_id tsa_name tsa_keyalias tsa_purpose <<< "$TSA_WORKER"
echo "    Worker $tsa_id   $tsa_name                key: $tsa_keyalias"
echo ""
echo -e "  ${GREEN}API Client (mTLS):${NC}"
echo "    Keystore:   certs/api/api-client.p12"
echo ""
if [ -n "$TENANT_ID" ]; then
echo -e "  ${GREEN}Tenant Certificates:${NC}"
echo "    CA cert:    certs/tenants/${TENANT_ID}/ca.pem"
echo "    CA chain:   certs/tenants/${TENANT_ID}/ca-chain.pem"
echo "    Signer:     certs/tenants/${TENANT_ID}/signer.p12"
echo ""
fi
echo -e "  ${GREEN}Keystore Password:${NC}"
echo "    $KEYSTORE_PASSWORD"
echo ""
echo -e "  ${GREEN}URLs:${NC}"
echo "    EJBCA Admin:       ${EJBCA_URL}/ejbca/adminweb/"
echo "    EJBCA Public:      ${EJBCA_PUBLIC_URL}/ejbca/publicweb/"
echo "    SignServer Admin:  ${SIGNSERVER_URL}/signserver/adminweb/"
echo "    SignServer REST:   ${SIGNSERVER_HTTP_URL}/signserver/process"
echo "    OCSP Responder:    ${OCSP_URL}"
echo "    CRL Distribution:  ${CRL_DP_URL}"
echo ""
echo -e "  ${GREEN}Certificate Profiles (configure via Admin Web for production):${NC}"
echo "    IVF-PDFSigner-Profile     -- PDF signing (3yr, digitalSignature + nonRepudiation)"
echo "    IVF-TSA-Profile           -- TSA (5yr, timeStamping EKU)"
echo "    IVF-TLS-Client-Profile    -- mTLS client (2yr, clientAuth EKU)"
echo "    IVF-OCSP-Profile          -- OCSP responder (2yr, OCSPSigning EKU)"
echo ""
echo -e "  ${YELLOW}Next Steps:${NC}"
echo "    1. Configure certificate profiles in EJBCA Admin Web for production use"
echo "    2. Configure end entity profiles in EJBCA Admin Web"
echo "    3. Set up CRL generation schedule in EJBCA"
echo "    4. Configure OCSP responder if needed"
echo "    5. Run 'bash scripts/init-mtls.sh' to configure mTLS with the new CA"
echo "    6. Update appsettings.json DigitalSigning section if CA paths changed"
if [ -n "$TENANT_ID" ]; then
echo "    7. Configure tenant-specific SignServer workers if needed"
fi
echo ""
log_ok "Enterprise PKI setup complete!"
