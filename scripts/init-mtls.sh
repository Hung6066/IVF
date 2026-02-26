#!/bin/bash
# =====================================================
# SignServer mTLS Initialization Script
# =====================================================
# Configures WildFly/SignServer for mutual TLS:
#   1. Imports Internal CA into a JKS truststore
#   2. Configures WildFly Elytron trust-manager and SSL context
#   3. Sets want-client-auth=true on HTTPS listener
#   4. Registers api-client certificate as SignServer wsadmin
#
# Run from host after SignServer container is healthy:
#   bash scripts/init-mtls.sh
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
    # Running inside the container
    EXEC_PREFIX=""
    EXEC_PREFIX_ROOT=""
else
    # Running from host — forward commands via docker exec
    EXEC_PREFIX="docker exec ${CONTAINER_NAME}"
    EXEC_PREFIX_ROOT="docker exec --user root ${CONTAINER_NAME}"
fi

WILDFLY_HOME="/opt/keyfactor/wildfly-35.0.1.Final"
KEYS_DIR="/opt/keyfactor/persistent/keys"
TRUSTSTORE_PATH="/opt/keyfactor/persistent/truststore.jks"
TRUSTSTORE_PASS="changeit"
CA_CERT="${KEYS_DIR}/ivf-ca.pem"
CLI="${WILDFLY_HOME}/bin/jboss-cli.sh"
SIGNSERVER_CLI="/opt/signserver/bin/signserver"

# Elytron resource names
KS_NAME="trustKS"
TM_NAME="httpsTM"
SSC_NAME="httpsSSC"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

run_cmd() { $EXEC_PREFIX sh -c "$1"; }
run_cmd_root() {
    if [ -n "$EXEC_PREFIX_ROOT" ]; then
        $EXEC_PREFIX_ROOT sh -c "$1"
    else
        sh -c "$1"
    fi
}
run_cli() { $EXEC_PREFIX "$CLI" --connect "--command=$1" 2>&1; }

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

# ─── Step 2: Configure WildFly Elytron ───
log_info "Step 2: WildFly Elytron mTLS configuration"

# 2a. Key-store for truststore
KS_CHECK=$(run_cli "/subsystem=elytron/key-store=${KS_NAME}:read-resource" 2>&1 || true)
if echo "$KS_CHECK" | grep -q '"outcome" => "success"'; then
    log_info "  ✓ Key-store '${KS_NAME}' already exists"
else
    log_info "  Creating key-store '${KS_NAME}'..."
    run_cli "/subsystem=elytron/key-store=${KS_NAME}:add(path=${TRUSTSTORE_PATH},type=JKS,credential-reference={clear-text=${TRUSTSTORE_PASS}})"
    log_info "  ✓ Key-store created"
fi

# 2b. Trust-manager
TM_CHECK=$(run_cli "/subsystem=elytron/trust-manager=${TM_NAME}:read-resource" 2>&1 || true)
if echo "$TM_CHECK" | grep -q '"outcome" => "success"'; then
    log_info "  ✓ Trust-manager '${TM_NAME}' already exists"
else
    log_info "  Creating trust-manager '${TM_NAME}'..."
    run_cli "/subsystem=elytron/trust-manager=${TM_NAME}:add(key-store=${KS_NAME})"
    log_info "  ✓ Trust-manager created"
fi

# 2c. Set trust-manager on SSL context
CURRENT_TM=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=trust-manager)" 2>&1 || true)
if echo "$CURRENT_TM" | grep -q "\"${TM_NAME}\""; then
    log_info "  ✓ SSL context already uses trust-manager '${TM_NAME}'"
else
    log_info "  Setting trust-manager on SSL context..."
    run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:write-attribute(name=trust-manager,value=${TM_NAME})"
    log_info "  ✓ Trust-manager set"
fi

# 2d. Enable want-client-auth
WANT_CLIENT=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=want-client-auth)" 2>&1 || true)
if echo "$WANT_CLIENT" | grep -q '"result" => true'; then
    log_info "  ✓ want-client-auth already enabled"
else
    log_info "  Enabling want-client-auth..."
    run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:write-attribute(name=want-client-auth,value=true)"
    log_info "  ✓ want-client-auth enabled"
fi

# 2e. Check if reload is needed
PROCESS_STATE=$(run_cli ":read-attribute(name=server-state)" 2>&1 || true)
if echo "$PROCESS_STATE" | grep -q "reload-required"; then
    log_info "  Reloading WildFly to apply changes..."
    run_cli ":reload"
    log_info "  Waiting for WildFly to restart..."
    sleep 15
    # Wait until CLI reconnects
    for i in $(seq 1 12); do
        if run_cli ":read-attribute(name=server-state)" 2>&1 | grep -q "running"; then
            break
        fi
        sleep 5
    done
    log_info "  ✓ WildFly reloaded"
else
    log_info "  No reload needed"
fi

# ─── Step 3: Verify ───
log_info "Step 3: Verification"

VERIFY_TM=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=trust-manager)" 2>&1 || true)
VERIFY_WCA=$(run_cli "/subsystem=elytron/server-ssl-context=${SSC_NAME}:read-attribute(name=want-client-auth)" 2>&1 || true)

if echo "$VERIFY_TM" | grep -q "\"${TM_NAME}\"" && echo "$VERIFY_WCA" | grep -q '"result" => true'; then
    log_info "  ✓ trust-manager = ${TM_NAME}"
    log_info "  ✓ want-client-auth = true"
    echo ""
    log_info "═══════════════════════════════════════"
    log_info "  mTLS configuration complete!"
    log_info "  SignServer now accepts client certs."
    log_info "═══════════════════════════════════════"
else
    log_error "  Verification failed!"
    echo "$VERIFY_TM"
    echo "$VERIFY_WCA"
    exit 1
fi
