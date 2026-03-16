#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "=== Clear EJBCA cache ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh clearcache 2>&1 | grep -v "^2026" | tail -5

echo ""
echo "=== Check cert profile IDs via DB (direct query) ==="
EJBCA_DB_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db")
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT id, profilename, profiletype FROM CertificateProfileData ORDER BY id;" 2>&1 | head -20

echo ""
echo "=== Check EE profile IDs via DB ==="
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT id, profilename FROM EndEntityProfileData ORDER BY id;" 2>&1 | head -20

echo ""
echo "=== Check Certificate Profiles via REST API ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" | python3 -c "import json,sys; d=json.load(sys.stdin); [print(f'CA: {c[\"name\"]} ID={c[\"id\"]}') for c in d['certificate_authorities']]" 2>/dev/null
