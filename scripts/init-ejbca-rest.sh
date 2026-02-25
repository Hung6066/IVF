#!/bin/bash
# Initialize EJBCA REST API access for IVF API client
# Run this after EJBCA container is healthy:
#   docker exec ivf-ejbca bash /scripts/init-ejbca-rest.sh
# Or mount the script and CA cert into the container via docker-compose

set -e

EJBCA_CLI="/opt/keyfactor/bin/ejbca.sh"
TRUSTSTORE="/opt/keyfactor/appserver/standalone/configuration/truststore.jks"
CA_CERT="/tmp/ivf-root-ca.pem"

# Check if CA cert file exists (must be mounted or copied first)
if [ ! -f "$CA_CERT" ]; then
    echo "ERROR: CA cert not found at $CA_CERT"
    echo "Copy it first: docker cp certs/ca/ca.pem ivf-ejbca:/tmp/ivf-root-ca.pem"
    exit 1
fi

echo "=== Enabling all EJBCA protocols ==="
$EJBCA_CLI config protocols enable --name "ACME" 2>&1 || true
$EJBCA_CLI config protocols enable --name "CMP" 2>&1 || true
$EJBCA_CLI config protocols enable --name "EST" 2>&1 || true
$EJBCA_CLI config protocols enable --name "MSAE" 2>&1 || true
$EJBCA_CLI config protocols enable --name "SCEP" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST Certificate Management" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST Certificate Management V2" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST CA Management" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST End Entity Management" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST End Entity Management V2" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST Crypto Token Management" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST Coap Management" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST SSH V1" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST System V1" 2>&1 || true
$EJBCA_CLI config protocols enable --name "REST Configdump" 2>&1 || true

echo "=== Checking if IVF Root CA already imported ==="
if $EJBCA_CLI ca listcas 2>&1 | grep -q "IVF Internal Root CA"; then
    echo "IVF Internal Root CA already exists in EJBCA"
else
    echo "Importing IVF Internal Root CA with SuperAdmin for ivf-api-client..."
    $EJBCA_CLI ca importcacert \
        --caname "IVF Internal Root CA" \
        -f "$CA_CERT" \
        -initauthorization \
        -superadmincn "ivf-api-client" 2>&1
fi

echo "=== Importing CA cert into TLS truststore ==="
# Get truststore password - it's the 2nd credential-reference in standalone.xml (keystore=first, truststore=second)
TS_PASS=$(grep -oP 'credential-reference clear-text="\K[^"]+' /opt/keyfactor/appserver/standalone/configuration/standalone.xml | sed -n '2p')

if keytool -list -keystore "$TRUSTSTORE" -storepass "$TS_PASS" -alias "ivf-internal-root-ca" > /dev/null 2>&1; then
    echo "CA cert already in truststore"
else
    keytool -importcert -alias "ivf-internal-root-ca" \
        -file "$CA_CERT" \
        -keystore "$TRUSTSTORE" \
        -storepass "$TS_PASS" \
        -noprompt 2>&1
    echo "CA cert imported into truststore (restart EJBCA if first time)"
fi

echo "=== Verifying setup ==="
$EJBCA_CLI config protocols status 2>&1 | grep "REST"
$EJBCA_CLI roles listadmins --role "Super Administrator Role" 2>&1

echo "=== Done! ==="
