#!/bin/bash
# =====================================================
# SignServer mTLS Initialization — PRODUCTION
# =====================================================
# Configures WildFly/SignServer with strict mTLS:
#   1. Imports Internal CA into a JKS truststore
#   2. Configures WildFly trust-manager and SSL context
#   3. Sets need-client-auth=true on HTTPS listener
#   4. Adds internal HTTP health listener (localhost:8081)
#      for container health checks without client cert
#
# DIFFERENCE from dev init-mtls.sh:
#   - dev:  want-client-auth=true  (allows no-cert connections)
#   - prod: need-client-auth=true  (REQUIRES client cert)
#   - prod: separate HTTP health endpoint on port 8081
#
# Run after SignServer container starts:
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls.sh
#
# Prerequisites:
#   - CA cert at /opt/keyfactor/persistent/keys/ivf-ca.pem
#   - SignServer container must be healthy
# =====================================================

set -euo pipefail

WILDFLY_HOME="/opt/keyfactor/wildfly-35.0.1.Final"
KEYS_DIR="/opt/keyfactor/persistent/keys"
TRUSTSTORE_PATH="${KEYS_DIR}/truststore.jks"
TRUSTSTORE_PASS="changeit"
CA_CERT="${KEYS_DIR}/ivf-ca.pem"
CLI="${WILDFLY_HOME}/bin/jboss-cli.sh"

log() { echo "[$(date '+%H:%M:%S')] [PRODUCTION] $1"; }

# ─── Step 1: Create/update truststore ───
if [ ! -f "$CA_CERT" ]; then
    log "ERROR: CA certificate not found at $CA_CERT"
    exit 1
fi

if [ ! -f "$TRUSTSTORE_PATH" ]; then
    log "Creating truststore with Internal CA..."
    keytool -import -trustcacerts \
        -alias ivf-internal-ca \
        -file "$CA_CERT" \
        -keystore "$TRUSTSTORE_PATH" \
        -storepass "$TRUSTSTORE_PASS" \
        -noprompt
    log "Truststore created: $TRUSTSTORE_PATH"
else
    log "Truststore already exists, verifying CA entry..."
    keytool -list -keystore "$TRUSTSTORE_PATH" -storepass "$TRUSTSTORE_PASS" -alias ivf-internal-ca > /dev/null 2>&1 \
        && log "CA entry found in truststore" \
        || {
            log "Adding CA to existing truststore..."
            keytool -import -trustcacerts \
                -alias ivf-internal-ca \
                -file "$CA_CERT" \
                -keystore "$TRUSTSTORE_PATH" \
                -storepass "$TRUSTSTORE_PASS" \
                -noprompt
        }
fi

# ─── Step 2: Configure WildFly mTLS via CLI (strict mode) ───
log "Checking WildFly mTLS configuration..."

TRUST_STORE_EXISTS=$($CLI --connect --command="/subsystem=elytron/key-store=httpsTrustStore:read-resource" 2>&1 || true)

if echo "$TRUST_STORE_EXISTS" | grep -q "success"; then
    log "WildFly trust-store 'httpsTrustStore' already configured"

    # Ensure need-client-auth is set (upgrade from want to need)
    NEED_CLIENT=$($CLI --connect --command="/subsystem=elytron/server-ssl-context=httpsSSC:read-attribute(name=need-client-auth)" 2>&1 || true)
    if echo "$NEED_CLIENT" | grep -q "true"; then
        log "need-client-auth already enabled"
    else
        log "Upgrading to need-client-auth=true (strict mTLS)..."
        $CLI --connect --commands="
            /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=need-client-auth,value=true),
            /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=want-client-auth,value=false)"
        log "Reloading WildFly..."
        $CLI --connect --command=":reload"
        sleep 10
    fi
else
    log "Configuring WildFly trust-store, trust-manager, and STRICT mTLS..."
    $CLI --connect --commands="
        /subsystem=elytron/key-store=httpsTrustStore:add(path=${TRUSTSTORE_PATH},type=JKS,credential-reference={clear-text=${TRUSTSTORE_PASS}}),
        /subsystem=elytron/trust-manager=httpsTM:add(key-store=httpsTrustStore),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=trust-manager,value=httpsTM),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=need-client-auth,value=true)"
    log "WildFly strict mTLS configured — reloading..."
    $CLI --connect --command=":reload"
    sleep 10
fi

# ─── Step 3: Add internal HTTP health listener on port 8081 ───
# This bypasses mTLS for health checks (bound to localhost only)
log "Checking health check listener..."

HEALTH_LISTENER=$($CLI --connect --command="/subsystem=undertow/server=default-server/http-listener=health-check:read-resource" 2>&1 || true)

if echo "$HEALTH_LISTENER" | grep -q "success"; then
    log "Health check listener already configured on port 8081"
else
    log "Adding HTTP health check listener on 127.0.0.1:8081..."
    $CLI --connect --commands="
        /socket-binding-group=standard-sockets/socket-binding=health-check:add(port=8081,interface=private),
        /subsystem=undertow/server=default-server/http-listener=health-check:add(socket-binding=health-check)"
    log "Health check listener added — reloading..."
    $CLI --connect --command=":reload"
    sleep 10
fi

# ─── Step 4: Verify configuration ───
log "Verifying mTLS configuration..."

# Check need-client-auth
NEED_CLIENT=$($CLI --connect --command="/subsystem=elytron/server-ssl-context=httpsSSC:read-attribute(name=need-client-auth)" 2>&1 || true)
if echo "$NEED_CLIENT" | grep -q "true"; then
    log "✓ need-client-auth = true (strict mTLS enforced)"
else
    log "⚠ WARNING: need-client-auth may not be set correctly"
    echo "$NEED_CLIENT"
fi

# Check trust-manager
TRUST_MGR=$($CLI --connect --command="/subsystem=elytron/server-ssl-context=httpsSSC:read-attribute(name=trust-manager)" 2>&1 || true)
if echo "$TRUST_MGR" | grep -q "httpsTM"; then
    log "✓ trust-manager = httpsTM"
else
    log "⚠ WARNING: trust-manager not configured"
fi

# Test health endpoint
log "Testing health endpoint..."
HEALTH=$(curl -sf http://localhost:8081/signserver/healthcheck/signserverhealth 2>&1 || curl -sf http://localhost:8080/signserver/healthcheck/signserverhealth 2>&1 || echo "HEALTH_CHECK_FAILED")
if echo "$HEALTH" | grep -q "ALLOK"; then
    log "✓ Health check accessible: ALLOK"
else
    log "⚠ Health check: $HEALTH"
fi

log "Production mTLS initialization complete"
log "  HTTPS (8443): need-client-auth=true — REQUIRES client certificate"
log "  HTTP  (8081): health checks only — no auth required (localhost)"
