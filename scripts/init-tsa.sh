#!/bin/bash
# =====================================================
# SignServer TSA (Timestamp Authority) Setup Script
# =====================================================
# Creates a TimeStampSigner worker in SignServer for PAdES-LTV.
# PDFSigner workers reference this TSA worker via TSA_WORKER property
# so signed PDFs include RFC 3161 timestamps for long-term validation.
#
# Usage:
#   bash scripts/init-tsa.sh              # from host
#   bash /opt/keyfactor/persistent/init-tsa.sh  # inside container
#
# Idempotent — safe to run multiple times.
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

# ── Detect execution context (host vs container) ──
CONTAINER_NAME="ivf-signserver"
SIGNSERVER_CLI="/opt/signserver/bin/signserver"
KEY_DIR="/opt/keyfactor/persistent/keys"

if [ -f "$SIGNSERVER_CLI" ]; then
    EXEC_PREFIX=""
    log_info "Running inside container"
else
    EXEC_PREFIX="docker exec $CONTAINER_NAME"
    log_info "Running from host (docker exec → $CONTAINER_NAME)"
    if ! docker inspect "$CONTAINER_NAME" &>/dev/null; then
        log_error "Container '$CONTAINER_NAME' not found"
        exit 1
    fi
fi

run_cmd() { $EXEC_PREFIX "$@"; }
run_ss()  { run_cmd "$SIGNSERVER_CLI" "$@"; }

# ── Configuration ──
TSA_WORKER_ID=100
TSA_WORKER_NAME="TimeStampSigner"
TSA_KEYSTORE="tsa-signer.p12"
TSA_KEYSTORE_PATH="$KEY_DIR/$TSA_KEYSTORE"
TSA_KEY_ALIAS="tsa"
TSA_STOREPASS="changeit"
TSA_DNAME="CN=IVF Timestamp Authority,O=IVF Clinic,OU=Digital Signing,C=VN"

# PDFSigner workers that should reference the TSA
PDF_WORKERS=(1 272 444 597 907)

CHANGES=0

# ── Step 1: Check if TSA worker already exists ──
log_info "Step 1: Checking if TSA worker (ID=$TSA_WORKER_ID) exists..."

if run_ss getstatus brief all 2>/dev/null | grep -q "Worker $TSA_WORKER_ID"; then
    log_ok "TSA worker already exists (ID=$TSA_WORKER_ID)"
else
    log_info "Creating TSA worker..."

    # Generate TSA keystore if not exists
    if ! run_cmd test -f "$TSA_KEYSTORE_PATH" 2>/dev/null; then
        log_info "Generating TSA keystore at $TSA_KEYSTORE_PATH"
        run_cmd keytool -genkeypair \
            -alias "$TSA_KEY_ALIAS" \
            -keyalg RSA -keysize 2048 \
            -sigalg SHA256withRSA \
            -validity 1095 \
            -dname "$TSA_DNAME" \
            -keystore "$TSA_KEYSTORE_PATH" \
            -storetype PKCS12 \
            -storepass "$TSA_STOREPASS" \
            -keypass "$TSA_STOREPASS" \
            -ext "ExtendedKeyUsage:critical=timeStamping" \
            -ext "KeyUsage=digitalSignature"
        log_ok "TSA keystore generated"
    else
        log_ok "TSA keystore already exists"
    fi

    # Set worker properties
    run_ss setproperty "$TSA_WORKER_ID" NAME "$TSA_WORKER_NAME"
    run_ss setproperty "$TSA_WORKER_ID" TYPE PROCESSABLE
    run_ss setproperty "$TSA_WORKER_ID" IMPLEMENTATION_CLASS \
        org.signserver.module.tsa.TimeStampSigner
    run_ss setproperty "$TSA_WORKER_ID" CRYPTOTOKEN_IMPLEMENTATION_CLASS \
        org.signserver.server.cryptotokens.KeystoreCryptoToken
    run_ss setproperty "$TSA_WORKER_ID" KEYSTORETYPE PKCS12
    run_ss setproperty "$TSA_WORKER_ID" KEYSTOREPATH "$TSA_KEYSTORE_PATH"
    run_ss setproperty "$TSA_WORKER_ID" KEYSTOREPASSWORD "$TSA_STOREPASS"
    run_ss setproperty "$TSA_WORKER_ID" DEFAULTKEY "$TSA_KEY_ALIAS"
    run_ss setproperty "$TSA_WORKER_ID" DEFAULTTSAPOLICYOID "1.2.3.4.1"
    run_ss setproperty "$TSA_WORKER_ID" ACCEPTANYPOLICY true
    run_ss setproperty "$TSA_WORKER_ID" ACCURACYMICROS 500
    run_ss setproperty "$TSA_WORKER_ID" ORDERING false
    run_ss setproperty "$TSA_WORKER_ID" INCLUDESTATUSSTRING true
    run_ss setproperty "$TSA_WORKER_ID" AUTHTYPE NOAUTH

    # Activate crypto token
    run_ss activatecryptotoken "$TSA_WORKER_ID" "$TSA_STOREPASS"
    run_ss reload "$TSA_WORKER_ID"

    CHANGES=$((CHANGES + 1))
    log_ok "TSA worker created (ID=$TSA_WORKER_ID, Name=$TSA_WORKER_NAME)"
fi

# ── Step 2: Set TSA_WORKER on all PDFSigner workers ──
log_info "Step 2: Configuring PDFSigner workers to use TSA..."

for wid in "${PDF_WORKERS[@]}"; do
    current_tsa=$(run_ss getconfig "$wid" 2>/dev/null | grep "TSA_WORKER" | awk -F'=' '{print $2}' | tr -d ' ' || echo "")
    if [ "$current_tsa" = "$TSA_WORKER_NAME" ]; then
        log_ok "Worker $wid already has TSA_WORKER=$TSA_WORKER_NAME"
    else
        run_ss setproperty "$wid" TSA_WORKER "$TSA_WORKER_NAME"
        run_ss reload "$wid"
        CHANGES=$((CHANGES + 1))
        log_ok "Worker $wid → TSA_WORKER=$TSA_WORKER_NAME"
    fi
done

# ── Step 3: Set ClientCertAuthorizer on TSA worker (if mTLS is configured) ──
log_info "Step 3: Checking ClientCertAuthorizer for TSA worker..."

current_auth=$(run_ss getconfig "$TSA_WORKER_ID" 2>/dev/null | grep "AUTHTYPE" | awk -F'=' '{print $2}' | tr -d ' ' || echo "")

# Check if any PDFSigner uses ClientCertAuthorizer
pdf_auth=$(run_ss getconfig 1 2>/dev/null | grep "AUTHTYPE" | awk -F'=' '{print $2}' | tr -d ' ' || echo "")
if [ "$pdf_auth" = "org.signserver.server.ClientCertAuthorizer" ]; then
    if [ "$current_auth" != "org.signserver.server.ClientCertAuthorizer" ]; then
        SERIAL=$(run_ss authorizedclients -worker 1 -list 2>/dev/null | grep "SN:" | head -1 | awk '{print $2}' | tr -d ',' || echo "")
        ISSUER=$(run_ss authorizedclients -worker 1 -list 2>/dev/null | grep "Issuer" | head -1 | sed 's/.*Issuer DN: //' || echo "")

        if [ -n "$SERIAL" ] && [ -n "$ISSUER" ]; then
            run_ss setproperty "$TSA_WORKER_ID" AUTHTYPE org.signserver.server.ClientCertAuthorizer
            run_ss addauthorizedclient "$TSA_WORKER_ID" "$SERIAL" "$ISSUER"
            run_ss reload "$TSA_WORKER_ID"
            CHANGES=$((CHANGES + 1))
            log_ok "TSA worker secured with ClientCertAuthorizer"
        else
            log_warn "Could not extract authorized client from Worker 1; TSA left with NOAUTH"
        fi
    else
        log_ok "TSA worker already uses ClientCertAuthorizer"
    fi
else
    log_info "PDFSigner uses $pdf_auth — TSA worker left with NOAUTH"
fi

# ── Verify ──
log_info "─── Verification ───"
run_ss getstatus brief all 2>/dev/null | grep -E "Worker ($TSA_WORKER_ID|$(IFS='|'; echo "${PDF_WORKERS[*]}"))" || true
echo ""

if [ "$CHANGES" -gt 0 ]; then
    log_ok "TSA setup complete ($CHANGES changes applied)"
else
    log_ok "TSA already fully configured (no changes needed)"
fi
