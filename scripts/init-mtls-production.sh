#!/bin/bash
# =====================================================
# SignServer mTLS Initialization — PRODUCTION
# =====================================================
# Configures WildFly/SignServer with strict mTLS:
#   1. Imports Internal CA into a JKS truststore
#   2. Configures WildFly Elytron trust-manager and SSL context
#   3. Sets need-client-auth=true on HTTPS listener
#   4. Adds internal HTTP health listener (localhost:8081)
#      for container health checks without client cert
#
# DIFFERENCE from dev init-mtls.sh:
#   - dev:  want-client-auth=true  (allows no-cert connections)
#   - prod: need-client-auth=true  (REQUIRES client cert)
#   - prod: separate HTTP health endpoint on port 8081
#
# Run from host after SignServer container is healthy:
#   bash scripts/init-mtls-production.sh
#
# Or run inside the container:
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls.sh
#
# The script is idempotent — safe to re-run.
#
# Prerequisites:
#   - CA cert mounted at /opt/keyfactor/persistent/keys/ivf-ca.pem
#   - SignServer container must be healthy
# =====================================================

set -euo pipefail

# ── Detect execution context (host vs. container) ──
CONTAINER_NAME="${SIGNSERVER_CONTAINER:-ivf-signserver}"

if [ -f "/opt/keyfactor/wildfly-35.0.1.Final/bin/jboss-cli.sh" ]; then
    EXEC_PREFIX=""
    EXEC_PREFIX_ROOT=""
else
    EXEC_PREFIX="docker exec ${CONTAINER_NAME}"
    EXEC_PREFIX_ROOT="docker exec --user root ${CONTAINER_NAME}"
fi

WILDFLY_HOME="/opt/keyfactor/wildfly-35.0.1.Final"
KEYS_DIR="/opt/keyfactor/persistent/keys"
TRUSTSTORE_PATH="/opt/keyfactor/persistent/truststore.jks"
TRUSTSTORE_PASS="changeit"
CA_CERT="${KEYS_DIR}/ivf-ca.pem"
CLI="${WILDFLY_HOME}/bin/jboss-cli.sh"

# Elytron resource names (must match dev init-mtls.sh)
KS_NAME="trustKS"
TM_NAME="httpsTM"
SSC_NAME="httpsSSC"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[PROD]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[PROD]${NC} $1"; }
log_error() { echo -e "${RED}[PROD]${NC} $1"; }

run_cmd() { $EXEC_PREFIX sh -c "$1"; }
run_cmd_root() {
    if [ -n "$EXEC_PREFIX_ROOT" ]; then
        $EXEC_PREFIX_ROOT sh -c "$1"
    else
        sh -c "$1"
    fi
}
run_cli() { $EXEC_PREFIX "$CLI" --connect "--command=$1" 2>&1; }

wait_for_wildfly() {
    log_info "  Waiting for WildFly to restart..."
    sleep 15
    for i in $(seq 1 12); do
        if run_cli ":read-attribute(name=server-state)" 2>&1 | grep -q "running"; then
            return 0
        fi
        sleep 5
    done
    log_error "  WildFly did not come back in time"
    return 1
}

# ─── Pre-flight ───
log_info "Checking prerequisites..."
if [ -n "$EXEC_PREFIX" ]; then
    if ! docker inspect "$CONTAINER_NAME" --format='{{.State.Status}}' 2>/dev/null | grep -q running; then
        log_error "Container '$CONTAINER_NAME' is not running"
        exit 1
    fi
fi

if ! run_cmd "test -f $CA_CERT && echo ok" 2>/dev/null | grep -q ok; then
    log_error "CA certificate not found at $CA_CERT"
    exit 1
fi
log_info "Prerequisites OK"

# ─── Step 1: Create/update JKS truststore ───
log_info "Step 1: Truststore setup"
if run_cmd "test -f $TRUSTSTORE_PATH && echo ok" 2>/dev/null | grep -q ok; then
    log_info "  Truststore already exists, verifying CA entry..."
    if run_cmd "keytool -list -keystore $TRUSTSTORE_PATH -storepass $TRUSTSTORE_PASS -alias ivf-root-ca" >/dev/null 2>&1; then
        log_info "  ✓ CA entry 'ivf-root-ca' found"
    else
        log_info "  Adding CA to existing truststore..."
        run_cmd "keytool -import -trustcacerts -alias ivf-root-ca -file $CA_CERT -keystore $TRUSTSTORE_PATH -storepass $TRUSTSTORE_PASS -noprompt"
    fi
else
    log_info "  Creating truststore with Internal CA..."
    run_cmd_root "keytool -import -trustcacerts -alias ivf-root-ca -file $CA_CERT -keystore $TRUSTSTORE_PATH -storepass $TRUSTSTORE_PASS -noprompt && chown 10001:0 $TRUSTSTORE_PATH && chmod 644 $TRUSTSTORE_PATH"
    log_info "  ✓ Truststore created: $TRUSTSTORE_PATH"
fi

# ─── Step 2: Configure WildFly Elytron (strict mTLS) ───
log_info "Step 2: WildFly Elytron strict mTLS configuration"

NEEDS_RELOAD=false

# 2a. Key-store
KS_CHECK=$(run_cli "/subsystem=elytron/key-store=${KS_NAME}:read-resource" 2>&1 || true)
if echo "$KS_CHECK" | grep -q '"outcome" => "success"'; then
    log_info "  ✓ Key-store '${KS_NAME}' already exists"
else
    log_info "  Creating key-store '${KS_NAME}'..."
    run_cli "/subsystem=elytron/key-store=${KS_NAME}:add(path=${TRUSTSTORE_PATH},type=JKS,credential-reference={clear-text=${TRUSTSTORE_PASS}})"
    NEEDS_RELOAD=true
fi

# 2b. Trust-manager
TM_CHECK=$(run_cli "/subsystem=elytron/trust-manager=${TM_NAME}:read-resource" 2>&1 || true)
if echo "$TM_CHECK" | grep -q '"outcome" => "success"'; then
    log_info "  ✓ Trust-manager '${TM_NAME}' already exists"
else
    log_info "  Creating trust-manager '${TM_NAME}'..."
    run_cli "/subsystem=elytron/trust-manager=${TM_NAME}:add(key-store=${KS_NAME})"
    NEEDS_RELOAD=true
fi

# 2c. Set trust-manager on SSL context
CURRENT_TM=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=trust-manager)" 2>&1 || true)
if echo "$CURRENT_TM" | grep -q "\"${TM_NAME}\""; then
    log_info "  ✓ SSL context already uses trust-manager '${TM_NAME}'"
else
    log_info "  Setting trust-manager on SSL context..."
    run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:write-attribute(name=trust-manager,value=${TM_NAME})"
    NEEDS_RELOAD=true
fi

# 2d. Enable need-client-auth (STRICT — requires cert)
NEED_CLIENT=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=need-client-auth)" 2>&1 || true)
if echo "$NEED_CLIENT" | grep -q '"result" => true'; then
    log_info "  ✓ need-client-auth already enabled"
else
    log_info "  Enabling need-client-auth (strict mTLS)..."
    run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:write-attribute(name=need-client-auth,value=true)"
    # Also disable want-client-auth (need supersedes want)
    run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:write-attribute(name=want-client-auth,value=false)"
    NEEDS_RELOAD=true
fi

# 2e. Reload if needed
if [ "$NEEDS_RELOAD" = true ]; then
    log_info "  Reloading WildFly to apply changes..."
    run_cli ":reload"
    wait_for_wildfly
    log_info "  ✓ WildFly reloaded"
else
    log_info "  No reload needed"
fi

# ─── Step 3: Add internal HTTP health listener on port 8081 ───
log_info "Step 3: Health check listener"
HEALTH_LISTENER=$(run_cli "/subsystem=undertow/server=default-server/http-listener=health-check:read-resource" 2>&1 || true)
if echo "$HEALTH_LISTENER" | grep -q '"outcome" => "success"'; then
    log_info "  ✓ Health check listener already configured on port 8081"
else
    log_info "  Adding HTTP health check listener on 127.0.0.1:8081..."
    run_cli "/socket-binding-group=standard-sockets/socket-binding=health-check:add(port=8081,interface=private)"
    run_cli "/subsystem=undertow/server=default-server/http-listener=health-check:add(socket-binding=health-check)"
    log_info "  Reloading WildFly..."
    run_cli ":reload"
    wait_for_wildfly
    log_info "  ✓ Health check listener added"
fi

# ─── Step 4: Verify ───
log_info "Step 4: Verification"

VERIFY_TM=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=trust-manager)" 2>&1 || true)
VERIFY_NCA=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=need-client-auth)" 2>&1 || true)

if echo "$VERIFY_TM" | grep -q "\"${TM_NAME}\"" && echo "$VERIFY_NCA" | grep -q '"result" => true'; then
    log_info "  ✓ trust-manager = ${TM_NAME}"
    log_info "  ✓ need-client-auth = true"
else
    log_error "  Verification failed!"
    echo "$VERIFY_TM"
    echo "$VERIFY_NCA"
    exit 1
fi

# Test health endpoint
HEALTH=$(run_cmd "curl -sf http://localhost:8081/signserver/healthcheck/signserverhealth 2>&1 || curl -sf http://localhost:8080/signserver/healthcheck/signserverhealth 2>&1 || echo HEALTH_CHECK_FAILED")
if echo "$HEALTH" | grep -q "ALLOK"; then
    log_info "  ✓ Health check accessible: ALLOK"
else
    log_warn "  Health check: $HEALTH"
fi

echo ""
log_info "═══════════════════════════════════════"
log_info "  PRODUCTION mTLS configuration complete!"
log_info "  HTTPS (8443): need-client-auth=true"
log_info "  HTTP  (8081): health checks only"
log_info "═══════════════════════════════════════"
