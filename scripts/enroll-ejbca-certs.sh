#!/bin/bash
# =====================================================
# EJBCA Certificate Enrollment Script
# =====================================================
# Enrolls certificates from EJBCA CA instead of self-signed.
# Uses EJBCA REST API (v1) to:
#   1. Create End Entity for each signer worker
#   2. Enroll and download PKCS#12 keystores
#   3. Deploy to SignServer workers
#
# Prerequisites:
#   - EJBCA running with REST API enabled
#     (run: bash scripts/init-ejbca-rest.sh)
#   - Certificate Profile: IVF-PDFSigner-Profile
#   - End Entity Profile: IVF-Signer-EEProfile
#   - CA: ManagementCA (or configured CA name)
#
# Usage:
#   bash scripts/enroll-ejbca-certs.sh           # all workers
#   bash scripts/enroll-ejbca-certs.sh --worker 1  # specific worker
#   bash scripts/enroll-ejbca-certs.sh --tsa       # TSA only
#   bash scripts/enroll-ejbca-certs.sh --dry-run   # preview only
# =====================================================

set -euo pipefail

# ── Colors ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
log_ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ── Configuration ──
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
CERTS_DIR="$PROJECT_DIR/certs"

EJBCA_CONTAINER="ivf-ejbca"
EJBCA_URL="https://localhost:8443/ejbca/ejbca-rest-api/v1"
SIGNSERVER_CONTAINER="ivf-signserver"
SIGNSERVER_CLI="/opt/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"

# EJBCA entity config
CA_NAME="${EJBCA_CA_NAME:-ManagementCA}"
CERT_PROFILE="${EJBCA_CERT_PROFILE:-IVF-PDFSigner-Profile}"
EE_PROFILE="${EJBCA_EE_PROFILE:-IVF-Signer-EEProfile}"
KEYSTORE_PASSWORD="changeit"

# Worker definitions: ID|Name|CN|KeyFile
WORKERS=(
    "1|PDFSigner|IVF PDF Signer|signer.p12"
    "272|PDFSigner_techinical|Ky Thuat Vien IVF|pdfsigner_techinical.p12"
    "444|PDFSigner_head_department|Truong Khoa IVF|pdfsigner_head_department.p12"
    "597|PDFSigner_doctor1|Bac Si IVF|pdfsigner_doctor1.p12"
    "907|PDFSigner_admin|Quan Tri IVF|pdfsigner_admin.p12"
)

TSA_WORKER="100|TimeStampSigner|IVF Timestamp Authority|tsa-signer.p12"

# ── Parse args ──
DRY_RUN=false
TARGET_WORKER=""
TSA_ONLY=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --worker) TARGET_WORKER="$2"; shift 2 ;;
        --tsa) TSA_ONLY=true; shift ;;
        --dry-run) DRY_RUN=true; shift ;;
        --ca) CA_NAME="$2"; shift 2 ;;
        --cert-profile) CERT_PROFILE="$2"; shift 2 ;;
        --ee-profile) EE_PROFILE="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: $0 [--worker ID] [--tsa] [--dry-run] [--ca NAME] [--cert-profile NAME] [--ee-profile NAME]"
            exit 0
            ;;
        *) log_error "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Pre-flight ──
log_info "═══ EJBCA Certificate Enrollment ═══"
log_info "CA: $CA_NAME | Cert Profile: $CERT_PROFILE | EE Profile: $EE_PROFILE"

if [ "$DRY_RUN" = true ]; then
    log_warn "DRY RUN — no changes will be made"
fi

for container in "$EJBCA_CONTAINER" "$SIGNSERVER_CONTAINER"; do
    if ! docker inspect "$container" &>/dev/null; then
        log_error "Container '$container' not found"
        exit 1
    fi
done

# ── Enroll function ──
enroll_certificate() {
    local worker_id="$1"
    local worker_name="$2"
    local cn="$3"
    local key_file="$4"
    local ee_username="ivf-signer-${worker_id}"

    log_info "Enrolling certificate for Worker $worker_id ($worker_name)..."
    log_info "  CN=$cn, O=IVF Clinic, C=VN"
    log_info "  End Entity: $ee_username"

    if [ "$DRY_RUN" = true ]; then
        log_warn "  [DRY RUN] Would enroll $cn via EJBCA"
        return 0
    fi

    # Step 1: Create/update End Entity via EJBCA CLI (more reliable than REST for CE)
    log_info "  Creating End Entity..."
    docker exec "$EJBCA_CONTAINER" /opt/keyfactor/bin/ejbca.sh ra addendentity \
        --username "$ee_username" \
        --dn "CN=$cn,O=IVF Clinic,OU=Digital Signing,C=VN" \
        --caname "$CA_NAME" \
        --type 1 \
        --token P12 \
        --password "$KEYSTORE_PASSWORD" \
        --certprofile "$CERT_PROFILE" \
        --eeprofile "$EE_PROFILE" 2>/dev/null || {
        # Entity may already exist — reset status to NEW (10) for re-enrollment
        docker exec "$EJBCA_CONTAINER" /opt/keyfactor/bin/ejbca.sh ra setendentitystatus \
            "$ee_username" 10 2>/dev/null || true
        log_info "  End Entity already exists — reset for re-enrollment"
    }

    # Set clear-text password (required by batch command for PKCS#12 generation)
    docker exec "$EJBCA_CONTAINER" /opt/keyfactor/bin/ejbca.sh ra setclearpwd \
        "$ee_username" "$KEYSTORE_PASSWORD" 2>/dev/null || true

    # Step 2: Enroll (batch generate) PKCS#12
    log_info "  Generating PKCS#12 keystore..."
    docker exec "$EJBCA_CONTAINER" /opt/keyfactor/bin/ejbca.sh batch \
        --username "$ee_username" \
        -dir /tmp/ejbca-certs 2>/dev/null

    local p12_path="/tmp/ejbca-certs/${ee_username}.p12"

    if ! docker exec "$EJBCA_CONTAINER" test -f "$p12_path" 2>/dev/null; then
        log_error "  PKCS#12 not generated at $p12_path"
        return 1
    fi

    # Step 3: Copy PKCS#12 from EJBCA → SignServer (use docker pipe to avoid host path issues)
    log_info "  Deploying to SignServer..."

    docker exec "$EJBCA_CONTAINER" cat "$p12_path" | \
        docker exec -i "$SIGNSERVER_CONTAINER" bash -c "cat > /tmp/_deploy_p12"

    docker exec "$SIGNSERVER_CONTAINER" bash -c "
        rm -f '${KEY_DIR}/${key_file}'
        cp /tmp/_deploy_p12 '${KEY_DIR}/${key_file}'
        rm -f /tmp/_deploy_p12
        chmod 400 '${KEY_DIR}/${key_file}'
        chown 10001:root '${KEY_DIR}/${key_file}' 2>/dev/null || true
    "

    # Step 4: Normalize key alias (EJBCA uses CN as alias which may contain spaces)
    local desired_alias="${key_file%.p12}"
    local current_alias
    current_alias=$(docker exec "$SIGNSERVER_CONTAINER" keytool -list \
        -keystore "${KEY_DIR}/${key_file}" \
        -storepass "$KEYSTORE_PASSWORD" -storetype PKCS12 2>/dev/null \
        | grep "PrivateKeyEntry" | head -1 | cut -d',' -f1 || echo "")

    if [ -n "$current_alias" ] && [ "$current_alias" != "$desired_alias" ]; then
        docker exec "$SIGNSERVER_CONTAINER" bash -c "
            chmod 600 '${KEY_DIR}/${key_file}'
            keytool -changealias -keystore '${KEY_DIR}/${key_file}' \
                -storepass '$KEYSTORE_PASSWORD' -storetype PKCS12 \
                -alias '$current_alias' -destalias '$desired_alias' 2>/dev/null || true
            chmod 400 '${KEY_DIR}/${key_file}'
        "
        log_info "  Key alias: $current_alias → $desired_alias"
    else
        desired_alias="$current_alias"
        log_info "  Key alias: $desired_alias"
    fi

    # Step 5: Update SignServer worker keystore config
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
        setproperty "$worker_id" KEYSTOREPATH "${KEY_DIR}/${key_file}"
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
        setproperty "$worker_id" KEYSTOREPASSWORD "$KEYSTORE_PASSWORD"

    if [ -n "$desired_alias" ]; then
        docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
            setproperty "$worker_id" DEFAULTKEY "$desired_alias"
    fi

    # Step 6: Activate and reload
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
        activatecryptotoken "$worker_id" "$KEYSTORE_PASSWORD" 2>/dev/null || true
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
        reload "$worker_id"

    # Cleanup EJBCA temp
    docker exec "$EJBCA_CONTAINER" rm -f "$p12_path" 2>/dev/null || true

    log_ok "  Worker $worker_id enrolled with EJBCA-issued certificate"
}

# ── Main ──
ENROLLED=0
FAILED=0

# Process workers
if [ "$TSA_ONLY" = true ]; then
    WORK_LIST=("$TSA_WORKER")
elif [ -n "$TARGET_WORKER" ]; then
    WORK_LIST=()
    for w in "${WORKERS[@]}" "$TSA_WORKER"; do
        IFS='|' read -r wid _ _ _ <<< "$w"
        if [ "$wid" = "$TARGET_WORKER" ]; then
            WORK_LIST+=("$w")
        fi
    done
    if [ ${#WORK_LIST[@]} -eq 0 ]; then
        log_error "Worker $TARGET_WORKER not found"
        exit 1
    fi
else
    WORK_LIST=("${WORKERS[@]}" "$TSA_WORKER")
fi

# Ensure EJBCA temp dir exists
docker exec "$EJBCA_CONTAINER" mkdir -p /tmp/ejbca-certs 2>/dev/null || true

for worker_def in "${WORK_LIST[@]}"; do
    IFS='|' read -r worker_id worker_name cn key_file <<< "$worker_def"

    if enroll_certificate "$worker_id" "$worker_name" "$cn" "$key_file"; then
        ENROLLED=$((ENROLLED + 1))
    else
        FAILED=$((FAILED + 1))
    fi
    echo ""
done

# ── Verify all workers ──
if [ "$DRY_RUN" = false ]; then
    log_info "─── Verification ───"
    docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" getstatus brief all 2>/dev/null || true
fi

# ── Summary ──
echo ""
log_info "═══ Enrollment Summary ═══"
log_ok "Enrolled: $ENROLLED"
[ "$FAILED" -gt 0 ] && log_error "Failed: $FAILED"
echo ""
log_info "Certificates are now issued by EJBCA CA ($CA_NAME)."
log_info "They include proper certificate chain for PAdES-LTV validation."
log_ok "Done"
