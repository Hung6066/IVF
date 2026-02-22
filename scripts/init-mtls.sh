#!/bin/bash
# =====================================================
# SignServer mTLS Initialization Script
# =====================================================
# Configures WildFly/SignServer for mutual TLS:
#   1. Imports Internal CA into a JKS truststore
#   2. Configures WildFly trust-manager and SSL context
#   3. Sets want-client-auth=true on HTTPS listener
#
# Run this script after SignServer container starts:
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/keys/init-mtls.sh
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

log() { echo "[$(date '+%H:%M:%S')] $1"; }

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

# ─── Step 2: Configure WildFly mTLS via CLI ───
log "Checking WildFly mTLS configuration..."

# Check if trust-store already configured
TRUST_STORE_EXISTS=$($CLI --connect --command="/subsystem=elytron/key-store=httpsTrustStore:read-resource" 2>&1 || true)

if echo "$TRUST_STORE_EXISTS" | grep -q "success"; then
    log "WildFly trust-store 'httpsTrustStore' already configured"
else
    log "Configuring WildFly trust-store and trust-manager..."
    $CLI --connect --commands="
        /subsystem=elytron/key-store=httpsTrustStore:add(path=${TRUSTSTORE_PATH},type=JKS,credential-reference={clear-text=${TRUSTSTORE_PASS}}),
        /subsystem=elytron/trust-manager=httpsTM:add(key-store=httpsTrustStore),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=trust-manager,value=httpsTM),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=want-client-auth,value=true)"
    log "WildFly mTLS configured — reloading..."
    $CLI --connect --command=":reload"
    sleep 10
fi

# ─── Step 3: Verify ───
log "Verifying SSL context..."
WANT_CLIENT=$($CLI --connect --command="/subsystem=elytron/server-ssl-context=httpsSSC:read-attribute(name=want-client-auth)" 2>&1 || true)
if echo "$WANT_CLIENT" | grep -q "true"; then
    log "✓ want-client-auth = true"
else
    log "WARNING: want-client-auth may not be set correctly"
    echo "$WANT_CLIENT"
fi

log "mTLS initialization complete"
