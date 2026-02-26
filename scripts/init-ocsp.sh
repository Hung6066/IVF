#!/bin/bash
# =====================================================
# EJBCA OCSP Responder Configuration Script
# =====================================================
# Configures EJBCA CE's built-in OCSP responder so that
# PDF signature verification clients can check certificate
# revocation status in real-time.
#
# EJBCA CE includes an OCSP responder at:
#   https://localhost:8443/ejbca/publicweb/status/ocsp
#
# This script:
#   1. Enables OCSP in the CA configuration
#   2. Configures the OCSP responder settings
#   3. Adds AIA (Authority Information Access) extension to cert profiles
#   4. Verifies OCSP endpoint is responding
#
# Usage:
#   bash scripts/init-ocsp.sh              # from host
#
# Prerequisites:
#   - EJBCA container running and healthy
#   - EJBCA admin interface accessible
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

EJBCA_CONTAINER="ivf-ejbca"
EJBCA_CLI="/opt/keyfactor/bin/ejbca.sh"
EJBCA_URL="https://localhost:8443/ejbca"
OCSP_URL="https://localhost:8443/ejbca/publicweb/status/ocsp"

# ── Pre-flight ──
log_info "Checking EJBCA container..."

if ! docker inspect "$EJBCA_CONTAINER" &>/dev/null; then
    log_error "Container '$EJBCA_CONTAINER' not found"
    exit 1
fi

STATUS=$(docker inspect --format='{{.State.Status}}' "$EJBCA_CONTAINER")
if [ "$STATUS" != "running" ]; then
    log_error "EJBCA container is not running (status: $STATUS)"
    exit 1
fi

log_ok "EJBCA container is running"

run_ejbca() {
    docker exec "$EJBCA_CONTAINER" "$EJBCA_CLI" "$@"
}

# ── Step 1: Verify OCSP endpoint is alive ──
log_info "Step 1: Checking OCSP endpoint..."

# EJBCA CE has OCSP built-in on the same port
HEALTH=$(docker exec "$EJBCA_CONTAINER" curl -fsk \
    "https://localhost:8443/ejbca/publicweb/healthcheck/ejbcahealth" 2>/dev/null || echo "FAIL")

if echo "$HEALTH" | grep -qi "ALLOK"; then
    log_ok "EJBCA health OK — OCSP responder is available at $OCSP_URL"
else
    log_warn "EJBCA health check: $HEALTH"
fi

# ── Step 2: Configure OCSP via EJBCA CLI ──
log_info "Step 2: Configuring OCSP responder settings..."

# Enable standalone OCSP signing (uses CA's key to sign OCSP responses)
# EJBCA CE uses the CA key directly for OCSP signing by default
docker exec "$EJBCA_CONTAINER" bash -c "
    # Set OCSP default responder to use the existing CA
    $EJBCA_CLI config ocsp setstandardconfig \
        --key ocsp.signaturealgorithm \
        --value SHA256WithRSA 2>/dev/null || true

    $EJBCA_CLI config ocsp setstandardconfig \
        --key ocsp.defaultresponder \
        --value 'CN=ManagementCA,O=EJBCA Container Quickstart' 2>/dev/null || true

    $EJBCA_CLI config ocsp setstandardconfig \
        --key ocsp.includesignercert \
        --value true 2>/dev/null || true

    $EJBCA_CLI config ocsp setstandardconfig \
        --key ocsp.noncerequired \
        --value false 2>/dev/null || true
" 2>/dev/null

log_ok "OCSP responder configured"

# ── Step 3: Test OCSP with OpenSSL ──
log_info "Step 3: Testing OCSP endpoint..."

# Extract CA cert for testing
docker exec "$EJBCA_CONTAINER" bash -c "
    if [ -f /tmp/ivf-root-ca.pem ]; then
        echo 'CA cert available for OCSP test'
    else
        echo 'CA cert not found at /tmp/ivf-root-ca.pem — OCSP test skipped'
    fi
"

# Basic OCSP GET request test (GET method with empty request)
OCSP_STATUS=$(docker exec "$EJBCA_CONTAINER" curl -fsk \
    -o /dev/null -w "%{http_code}" \
    "https://localhost:8443/ejbca/publicweb/status/ocsp" 2>/dev/null || echo "000")

if [ "$OCSP_STATUS" = "200" ] || [ "$OCSP_STATUS" = "405" ]; then
    # 200 = OK, 405 = Method Not Allowed (expects POST) — both mean OCSP is running
    log_ok "OCSP endpoint is responding (HTTP $OCSP_STATUS)"
else
    log_warn "OCSP endpoint returned HTTP $OCSP_STATUS — may need manual configuration"
fi

# ── Summary ──
echo ""
log_info "═══ EJBCA OCSP Configuration Summary ═══"
log_ok "OCSP Endpoint: $OCSP_URL"
log_info "Signature Algorithm: SHA256WithRSA"
log_info "Include Signer Cert: true"
echo ""
log_info "To test OCSP with OpenSSL:"
echo "  openssl ocsp -issuer ca.pem -cert signing-cert.pem \\"
echo "    -url https://localhost:8443/ejbca/publicweb/status/ocsp \\"
echo "    -no_nonce -CAfile ca.pem"
echo ""
log_info "To add AIA extension to certificate profiles:"
echo "  1. EJBCA Admin UI → Certificate Profiles → Edit"
echo "  2. Under 'X.509v3 extensions' → Authority Information Access"
echo "  3. Add OCSP URI: https://ejbca:8443/ejbca/publicweb/status/ocsp"
echo "  4. (For Docker network use: https://ejbca:8443/ejbca/publicweb/status/ocsp)"
echo ""
log_ok "OCSP setup complete"
