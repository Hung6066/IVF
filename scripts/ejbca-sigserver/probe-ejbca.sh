#!/bin/bash
echo "=== Probe REST API v1 endpoints ==="
for path in \
  "v1/certificate/status" \
  "v1/certificateprofile" \
  "v1/certificateprofiles" \
  "v1/certificate/profiles" \
  "v1/ca" \
  "v1/endentity" \
  "v1/endentityprofiles" \
  "v2/certificate" \
  "v2/ca" \
  "v2/endentity" \
  "v2/certificateprofile"; do
  code=$(curl -sk -o /dev/null -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/$path")
  echo "  $path -> HTTP $code"
done

echo ""
echo "=== Try v1/ca list ==="
curl -sk "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" | head -c 200
echo ""

echo ""
echo "=== CA ejbca.sh ca list certprofile ==="
/opt/keyfactor/bin/ejbca.sh ca listcertificateprofiles 2>&1 | grep -v "^2026"

echo ""
echo "=== CA ejbca.sh ra listprofiles ==="
/opt/keyfactor/bin/ejbca.sh ra --help 2>&1 | grep -iv "^2026" | head -40
