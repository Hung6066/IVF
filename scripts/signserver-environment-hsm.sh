#!/bin/bash
# Persistent environment-hsm hook - runs on every SignServer container start

# 1. Copy truststore.jks from persistent volume to WildFly config dir
CONF_DIR=/opt/keyfactor/appserver/standalone/configuration
if [ -f /opt/keyfactor/persistent/truststore.jks ]; then
    cp /opt/keyfactor/persistent/truststore.jks "${CONF_DIR}/truststore.jks"
    echo "[hook] truststore.jks copied to config dir"
fi

# 2. Fix TLS protocols
python3 /opt/keyfactor/persistent/fix-tls.py

# 3. Register SoftHSM library (stored in persistent volume) in SignServer deploy properties
# Note: library lives directly in persistent volume (/opt/keyfactor/persistent/libsofthsm2.so)
# so no copy needed — just register the persistent path.
SOFTHSM_LIB=/opt/keyfactor/persistent/libsofthsm2.so
DEPLOY_PROPS=/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties
if [ -f "${SOFTHSM_LIB}" ]; then
    if ! grep -q "cryptotoken.p11.lib.90.name" "${DEPLOY_PROPS}"; then
        echo "cryptotoken.p11.lib.90.name = SoftHSM" >> "${DEPLOY_PROPS}"
        echo "cryptotoken.p11.lib.90.file = ${SOFTHSM_LIB}" >> "${DEPLOY_PROPS}"
        echo "[hook] SoftHSM library registered in deploy.properties: ${SOFTHSM_LIB}"
    else
        # Fix any stale wrong path
        sed -i "s|cryptotoken.p11.lib.90.file = .*|cryptotoken.p11.lib.90.file = ${SOFTHSM_LIB}|" "${DEPLOY_PROPS}"
        echo "[hook] SoftHSM library path ensured: ${SOFTHSM_LIB}"
    fi
else
    echo "[hook] WARNING: ${SOFTHSM_LIB} not found — workers will remain offline"
fi

# 5. Ensure SoftHSM2 config points to the persistent token directory
SOFTHSM_CONF_FILE=/opt/keyfactor/persistent/softhsm/softhsm2.conf
SOFTHSM_TOKEN_DIR=/opt/keyfactor/persistent/softhsm-tokens
mkdir -p "${SOFTHSM_TOKEN_DIR}"
cat > "${SOFTHSM_CONF_FILE}" << EOF
directories.tokendir = ${SOFTHSM_TOKEN_DIR}
log.level = INFO
EOF
export SOFTHSM2_CONF="${SOFTHSM_CONF_FILE}"
echo "[hook] SOFTHSM2_CONF=${SOFTHSM2_CONF}"

# 6. Initialize SoftHSM token if softhsm2-util is available and token missing
TOKEN_LABEL="${SOFTHSM_TOKEN_LABEL:-SignServerToken}"
HSM_PIN="$(cat "${SOFTHSM_USER_PIN_FILE}" 2>/dev/null || echo '3ac33a807af6b22fe9f22e4ba2c56a3b')"
HSM_SO_PIN="$(cat "${SOFTHSM_SO_PIN_FILE}" 2>/dev/null || echo "${HSM_PIN}")"

if command -v softhsm2-util &>/dev/null; then
    if softhsm2-util --show-slots 2>/dev/null | grep -q "Label.*${TOKEN_LABEL}"; then
        echo "[hook] SoftHSM token '${TOKEN_LABEL}' already initialized"
    else
        echo "[hook] Initializing SoftHSM token '${TOKEN_LABEL}'"
        softhsm2-util --init-token --free \
            --label "${TOKEN_LABEL}" \
            --pin "${HSM_PIN}" \
            --so-pin "${HSM_SO_PIN}"
        echo "[hook] SoftHSM token initialized"
    fi
else
    echo "[hook] softhsm2-util not available, skipping token init check"
fi

# 8. Ensure WildFly mTLS config (trust-manager + want-client-auth) is present
# WildFly can overwrite standalone.xml on startup; re-apply via jboss-cli if missing
(
    JBOSS_CLI=/opt/keyfactor/wildfly-35.0.1.Final/bin/jboss-cli.sh
    STANDALONE_XML=/opt/keyfactor/wildfly-35.0.1.Final/standalone/configuration/standalone.xml
    # Wait for WildFly management interface to be ready (up to 5 min)
    for i in $(seq 1 30); do
        if ${JBOSS_CLI} --connect --command="ls /subsystem=elytron" > /dev/null 2>&1; then
            break
        fi
        sleep 10
    done
    if ! grep -q "httpsTM" "${STANDALONE_XML}" 2>/dev/null; then
        echo "[hook] Applying WildFly mTLS config (trust-manager + want-client-auth)..."
        ${JBOSS_CLI} --connect --commands="
/subsystem=elytron/key-store=httpsTS:add(path=signserver-truststore.jks,relative-to=jboss.server.config.dir,credential-reference={clear-text=changeit},type=JKS),
/subsystem=elytron/key-store=httpsTS:load(),
/subsystem=elytron/trust-manager=httpsTM:add(key-store=httpsTS),
/subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=trust-manager,value=httpsTM),
/subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(name=want-client-auth,value=true),
reload" 2>&1 && echo "[hook] mTLS config applied" || echo "[hook] mTLS config FAILED"
    else
        echo "[hook] WildFly mTLS config already present"
    fi
) &
echo "[hook] WildFly mTLS idempotency check scheduled"

# 7. Schedule auto-activation of crypto tokens after SignServer deploys
# Runs in background after a delay to allow full startup
(
    sleep 120
    SIGNCLI=/opt/keyfactor/signserver/bin/signserver
    if [ -f "${SIGNCLI}" ]; then
        PIN="$(cat "${SOFTHSM_USER_PIN_FILE}" 2>/dev/null || echo '3ac33a807af6b22fe9f22e4ba2c56a3b')"
        for WID in 1 100 272 444 597 907; do
            ${SIGNCLI} activatecryptotoken ${WID} "${PIN}" > /dev/null 2>&1 && echo "[hook-autoactivate] Worker ${WID} activated"
        done
    fi
) &
echo "[hook] Auto-activation scheduled in 120s"
