#!/bin/bash
echo "=== Check ejbca.ear for profile resources ==="
ls /opt/keyfactor/ejbca/dist/ejbca.ear 2>/dev/null | head -5

# Try to extract profile resources from the EAR
cd /tmp
mkdir -p ejbca-extract
cp /opt/keyfactor/ejbca/dist/ejbca.ear /tmp/ejbca-extract/ 2>/dev/null

echo ""
echo "=== Try to find profile XML in JARs ==="
java -cp /opt/keyfactor/ejbca/dist/ejbca.ear org.ejbca.ui.cli.ca.CaListCAsCommand 2>&1 | head -5

echo ""
echo "=== Check REST API with superadmin cert (PEM format) ==="
# Try to call REST API with cert
openssl pkcs12 -in /tmp/superadmin_new/superadmin.p12 -passin pass:changeit -clcerts -nokeys -out /tmp/superadmin.crt 2>/dev/null
openssl pkcs12 -in /tmp/superadmin_new/superadmin.p12 -passin pass:changeit -nocerts -nodes -out /tmp/superadmin.key 2>/dev/null
echo "Cert files created"
ls -la /tmp/superadmin.crt /tmp/superadmin.key 2>/dev/null

curl -sk --cert /tmp/superadmin.crt --key /tmp/superadmin.key "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" -o /tmp/ca-response.json -w "\nHTTP: %{http_code}\n"
cat /tmp/ca-response.json | head -c 300

echo ""
echo "=== Check certprofile REST get ==="
curl -sk --cert /tmp/superadmin.crt --key /tmp/superadmin.key "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/certificate/status" -w "\nHTTP: %{http_code}\n"
