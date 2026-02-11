#!/bin/bash
# ============================================================
# IVF Digital Signing Infrastructure Setup Script
# Sets up EJBCA CA + SignServer for PDF document signing
# ============================================================
# Usage: bash scripts/setup-signing.sh
# Requires: docker, docker-compose, curl
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

log_info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
log_ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ─── Configuration ───────────────────────────────────────────
EJBCA_URL="https://localhost:8443"
EJBCA_PUBLIC_URL="http://localhost:8442"
SIGNSERVER_URL="https://localhost:9443"
SIGNSERVER_HTTP_URL="http://localhost:9080"
SIGNER_CERT_CN="IVF PDF Signer"
CERT_VALIDITY_DAYS=1095  # 3 years
WORKER_NAME="PDFSigner"
WORKER_ID=1
KEYSTORE_PASSWORD="changeit"
KEY_ALIAS="signer"
CONTAINER_KEYSTORE_PATH="/tmp/signer.p12"

echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}  IVF Digital Signing Infrastructure Setup${NC}"
echo -e "${CYAN}  EJBCA (Certificate Authority) + SignServer (PDF Signer)${NC}"
echo -e "${CYAN}============================================================${NC}"
echo ""

# ─── Step 1: Start Docker services ──────────────────────────
log_info "Step 1: Starting EJBCA and SignServer Docker containers..."
cd "$PROJECT_DIR"
docker compose up -d ejbca-db signserver-db
log_info "Waiting for databases to be healthy..."
sleep 10

docker compose up -d ejbca
log_info "Waiting for EJBCA to start (this may take 2-3 minutes on first run)..."

MAX_RETRIES=60
for i in $(seq 1 $MAX_RETRIES); do
    if curl -fsk "${EJBCA_URL}/ejbca/publicweb/healthcheck/ejbcahealth" > /dev/null 2>&1; then
        log_ok "EJBCA is healthy!"
        break
    fi
    if [ $i -eq $MAX_RETRIES ]; then
        log_error "EJBCA failed to start after ${MAX_RETRIES} retries"
        docker compose logs ejbca --tail=50
        exit 1
    fi
    echo -n "."
    sleep 5
done

docker compose up -d signserver
log_info "Waiting for SignServer to start..."
for i in $(seq 1 $MAX_RETRIES); do
    if curl -fsk "${SIGNSERVER_URL}/signserver/healthcheck/signserverhealth" > /dev/null 2>&1; then
        log_ok "SignServer is healthy!"
        break
    fi
    if [ $i -eq $MAX_RETRIES ]; then
        log_error "SignServer failed to start after ${MAX_RETRIES} retries"
        docker compose logs signserver --tail=50
        exit 1
    fi
    echo -n "."
    sleep 5
done

echo ""
log_ok "All containers are running!"

# ─── Step 2: Generate Keystore (inside container) ────────────
log_info "Step 2: Generating PKCS12 keystore with Java keytool inside SignServer container..."

docker exec ivf-signserver keytool -genkeypair \
    -alias "$KEY_ALIAS" \
    -keyalg RSA -keysize 2048 -sigalg SHA256withRSA \
    -validity $CERT_VALIDITY_DAYS \
    -dname "CN=${SIGNER_CERT_CN},O=IVF Clinic,OU=Digital Signing,C=VN" \
    -keystore "$CONTAINER_KEYSTORE_PATH" \
    -storetype PKCS12 \
    -storepass "$KEYSTORE_PASSWORD" \
    -keypass "$KEYSTORE_PASSWORD" 2>/dev/null

# Verify keystore
KEYTOOL_CHECK=$(docker exec ivf-signserver keytool -list -keystore "$CONTAINER_KEYSTORE_PATH" -storepass "$KEYSTORE_PASSWORD" 2>&1)
if echo "$KEYTOOL_CHECK" | grep -q "$KEY_ALIAS"; then
    log_ok "Keystore created with alias '$KEY_ALIAS'"
else
    log_error "Failed to create keystore!"
    exit 1
fi

# ─── Step 3: Configure SignServer PDFSigner Worker ───────────
log_info "Step 3: Configuring SignServer PDFSigner worker..."

# Create properties file inside container
docker exec ivf-signserver bash -c "cat > /tmp/worker.properties << 'EOF'
GLOB.WORKER${WORKER_ID}.CLASSPATH = org.signserver.module.pdfsigner.PDFSigner
GLOB.WORKER${WORKER_ID}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.P12CryptoToken
WORKER${WORKER_ID}.NAME = ${WORKER_NAME}
WORKER${WORKER_ID}.AUTHTYPE = NOAUTH
WORKER${WORKER_ID}.DEFAULTKEY = ${KEY_ALIAS}
WORKER${WORKER_ID}.KEYSTOREPATH = ${CONTAINER_KEYSTORE_PATH}
WORKER${WORKER_ID}.KEYSTOREPASSWORD = ${KEYSTORE_PASSWORD}
EOF"

log_info "Loading worker properties..."
docker exec ivf-signserver bin/signserver setproperties /tmp/worker.properties 2>/dev/null

# Fix TYPE property (setproperties has a known bug that resets TYPE to empty)
log_info "Setting TYPE and additional worker properties..."
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID TYPE PROCESSABLE 2>/dev/null
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID CERTIFICATION_LEVEL NOT_CERTIFIED 2>/dev/null
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID ADD_VISIBLE_SIGNATURE false 2>/dev/null
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID REASON "Xac nhan bao cao y te IVF" 2>/dev/null
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID LOCATION "IVF Clinic" 2>/dev/null
docker exec ivf-signserver bin/signserver setproperty $WORKER_ID REFUSE_DOUBLE_INDIRECT_OBJECTS true 2>/dev/null

# Reload worker
docker exec ivf-signserver bin/signserver reload $WORKER_ID 2>/dev/null
log_ok "Worker configured and reloaded"

# Activate crypto token
log_info "Activating crypto token..."
ACTIVATE_RESULT=$(docker exec ivf-signserver bin/signserver activatecryptotoken $WORKER_ID $KEYSTORE_PASSWORD 2>&1)
if echo "$ACTIVATE_RESULT" | grep -q "successful"; then
    log_ok "Crypto token activated!"
else
    log_error "Crypto token activation failed: $ACTIVATE_RESULT"
    log_info "Check logs: docker logs ivf-signserver --tail=50"
    exit 1
fi

# Verify worker status
STATUS_RESULT=$(docker exec ivf-signserver bin/signserver getstatus brief all 2>&1)
if echo "$STATUS_RESULT" | grep -q "Active"; then
    log_ok "PDFSigner worker is Active!"
else
    log_warn "Worker status: $STATUS_RESULT"
fi

# ─── Step 4: Test PDF Signing ────────────────────────────────
log_info "Step 4: Testing PDF signing via REST API..."

# Create a minimal test PDF in temp
TEST_PDF="/tmp/test-signing.pdf"
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

SIGNED_PDF="/tmp/test-signed.pdf"
HTTP_CODE=$(curl -sk -o "$SIGNED_PDF" -w "%{http_code}" \
    -X POST "${SIGNSERVER_HTTP_URL}/signserver/process" \
    -F "workerName=${WORKER_NAME}" \
    -F "data=@${TEST_PDF}" 2>/dev/null || echo "000")

if [ "$HTTP_CODE" = "200" ]; then
    SIGNED_SIZE=$(stat -c%s "$SIGNED_PDF" 2>/dev/null || stat -f%z "$SIGNED_PDF" 2>/dev/null || echo "0")
    log_ok "PDF signing test passed! (signed output: ${SIGNED_SIZE} bytes)"
else
    log_warn "Test signing returned HTTP ${HTTP_CODE}. Worker may need manual configuration."
    log_info "Complete setup at: ${SIGNSERVER_URL}/signserver/adminweb/"
fi

# ─── Summary ─────────────────────────────────────────────────
echo ""
echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}  Setup Complete!${NC}"
echo -e "${CYAN}============================================================${NC}"
echo ""
echo "  Services:"
echo "    EJBCA Admin:       ${EJBCA_URL}/ejbca/adminweb/"
echo "    EJBCA Public:      ${EJBCA_PUBLIC_URL}/ejbca/publicweb/"
echo "    SignServer Admin:  ${SIGNSERVER_URL}/signserver/adminweb/"
echo "    SignServer REST:   ${SIGNSERVER_HTTP_URL}/signserver/process"
echo ""
echo "  SignServer Worker:"
echo "    Name:       ${WORKER_NAME} (ID: ${WORKER_ID})"
echo "    Key Alias:  ${KEY_ALIAS}"
echo "    Keystore:   ${CONTAINER_KEYSTORE_PATH} (inside container)"
echo "    Password:   ${KEYSTORE_PASSWORD}"
echo ""
echo "  IVF API Endpoints:"
echo "    Export with signing:  GET /api/forms/responses/{id}/export-pdf?sign=true"
echo "    Report with signing:  GET /api/forms/reports/{id}/export-pdf?sign=true"
echo "    Upload & sign:        POST /api/signing/sign-pdf"
echo "    Health check:         GET /api/signing/health"
echo ""
echo "  Next Steps:"
echo "    1. Set DigitalSigning:Enabled=true in appsettings.json (or env var)"
echo "    2. Restart IVF API"
echo "    3. Test: curl http://localhost:5000/api/signing/health"
echo ""
log_ok "Digital signing infrastructure is ready!"
