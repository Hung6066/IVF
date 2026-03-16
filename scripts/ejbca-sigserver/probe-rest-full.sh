#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "=== Try more REST paths with auth ==="
for path in \
  "v1/endentity/search" \
  "v1/endentitiesprofile" \
  "v1/endentityprofile" \
  "v1/endentity/p10enroll" \
  "v1/ra/endentity" \
  "v1/certificate/search" \
  "v1/crypto" \
  "v1/cryptotoken/status"; do
  code=$(curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/${path}")
  echo "  ${path} -> HTTP ${code}"
done

echo ""
echo "=== Try to get OpenAPI spec from alternative paths ==="
for path in \
  "ejbca/openapi.json" \
  "ejbca-rest-api/openapi.json" \
  "ejbca/ejbca-rest-api" \
  "ejbca/doc/openapi.json"; do
  code=$(curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/${path}")
  echo "  /${path} -> HTTP ${code}"
done

echo ""
echo "=== Check REST endpoint list via v1/certificate (get methods info) ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/certificate" | head -c 400
echo ""

echo ""
echo "=== Try EJBCA Legacy REST ==="
for path in \
  "ejbca/certificates" \
  "ejbca/ca" \
  "ejbca/ejbca-ws-cli/ejbcaws"; do
  code=$(curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/${path}")
  echo "  /${path} -> HTTP ${code}"
done
