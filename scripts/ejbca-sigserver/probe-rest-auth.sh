#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "=== v1 API endpoints ==="
for path in "v1/certificate/status" "v1/ca" "v1/endentity" "v1/cryptotoken" "v1/configdump" "v1/certprofile" "v1/profiles"; do
  code=$(curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/${path}")
  echo "  ${path} -> HTTP ${code}"
done

echo ""
echo "=== v2 API endpoints ==="
for path in "v2/ca" "v2/endentity" "v2/certificate" "v2/certprofile" "v2/profiles" "v2/configdump"; do
  code=$(curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/${path}")
  echo "  ${path} -> HTTP ${code}"
done

echo ""
echo "=== Try to get CA list (authenticated) ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" | python3 -c "import sys,json; d=json.load(sys.stdin); [print(f'  CA: {ca[\"name\"]} (ID={ca[\"id\"]})') for ca in d['certificate_authorities']]" 2>/dev/null

echo ""
echo "=== Check OpenAPI spec via Swagger UI (maybe different URL) ==="
curl -sk $CERT -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/" 
echo ""
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/" -w "HTTP: %{http_code}"
