#!/bin/bash
echo "=== Check openssl availability and cert ==="
which openssl 2>&1
openssl version 2>&1

echo ""
echo "=== Try extract superadmin cert/key ==="
openssl pkcs12 -in /tmp/superadmin_new/superadmin.p12 -passin pass:changeit -clcerts -nokeys -out /tmp/superadmin.crt 2>&1
echo "Exit: $?"
ls -la /tmp/superadmin.crt 2>&1

openssl pkcs12 -in /tmp/superadmin_new/superadmin.p12 -passin pass:changeit -nocerts -nodes -out /tmp/superadmin.key 2>&1
echo "Exit: $?"
ls -la /tmp/superadmin.key 2>&1

echo ""
echo "=== Try curl with certs - verbose TLS ==="
curl -v --cert /tmp/superadmin.crt --key /tmp/superadmin.key \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" 2>&1 | grep -E "(HTTP|SSL|TLS|cert|error|Error|connect|issued|subject)" | head -30
