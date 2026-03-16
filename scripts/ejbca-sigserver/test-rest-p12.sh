#!/bin/bash
echo "=== Try curl with P12 directly ==="
curl -sk --cert-type P12 --cert /tmp/superadmin_new/superadmin.p12:changeit \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" \
  -w "\nHTTP: %{http_code}\n" | head -c 500

echo ""
echo "=== Try v1 endentity with P12 ==="
curl -sk --cert-type P12 --cert /tmp/superadmin_new/superadmin.p12:changeit \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/endentity" \
  -w "\nHTTP: %{http_code}\n" | head -c 300

echo ""
echo "=== Try v1 certificate/profiles ==="
curl -sk --cert-type P12 --cert /tmp/superadmin_new/superadmin.p12:changeit \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/certificate/profiles" \
  -w "\nHTTP: %{http_code}\n" | head -c 300

echo ""
echo "=== Try no cert - v1/ca ==="
curl -sk "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" \
  -w "\nHTTP: %{http_code}\n" | head -c 300
