#!/bin/bash
# Fix SignServer SoftHSM: copies libsofthsm2.so from persistent volume,
# registers it in deploy.properties, and ensures SOFTHSM2_CONF is correct.
# Upload to VPS and run as root.

set -e

SSCONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | head -1)
if [ -z "$SSCONT" ]; then
    echo "ERROR: SignServer container not found"
    exit 1
fi
echo "Container: $SSCONT"

# Write the updated environment-hsm hook
docker exec -i "$SSCONT" tee /opt/keyfactor/persistent/environment-hsm > /dev/null << 'ENDHOOK'
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

# 3. Install SoftHSM2 library from persistent volume into container
SOFTHSM_LIB_SRC=/opt/keyfactor/persistent/libsofthsm2.so
SOFTHSM_LIB_DEST=/usr/lib64/softhsm/libsofthsm2.so
if [ -f "${SOFTHSM_LIB_SRC}" ]; then
    mkdir -p "$(dirname ${SOFTHSM_LIB_DEST})"
    cp "${SOFTHSM_LIB_SRC}" "${SOFTHSM_LIB_DEST}"
    echo "[hook] libsofthsm2.so installed to ${SOFTHSM_LIB_DEST}"
else
    echo "[hook] WARNING: ${SOFTHSM_LIB_SRC} not found"
fi

# 4. Register SoftHSM library in SignServer deploy properties
DEPLOY_PROPS=/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties
if ! grep -q "cryptotoken.p11.lib.90.name" "${DEPLOY_PROPS}"; then
    echo "cryptotoken.p11.lib.90.name = SoftHSM" >> "${DEPLOY_PROPS}"
    echo "cryptotoken.p11.lib.90.file = ${SOFTHSM_LIB_DEST}" >> "${DEPLOY_PROPS}"
    echo "[hook] SoftHSM library registered in deploy.properties"
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
ENDHOOK

echo "Hook written successfully"
echo "Verifying hook contents:"
docker exec "$SSCONT" head -5 /opt/keyfactor/persistent/environment-hsm
