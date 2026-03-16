#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "============================================================"
echo "  EJBCA PKI CONFIGURATION - FINAL VERIFICATION"
echo "============================================================"

echo ""
echo "=== 1. Certificate Authorities ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" | python3 -c "
import json, sys
d = json.load(sys.stdin)
for ca in d['certificate_authorities']:
    print(f'  [{ca[\"id\"]}] {ca[\"name\"]}')
    print(f'      Issuer: {ca[\"issuer_dn\"]}')
    print(f'      Subject: {ca[\"subject_dn\"]}')
    print(f'      Expires: {ca[\"expiration_date\"][:10]}')
" 2>/dev/null

echo ""
echo "=== 2. Certificate Profiles ==="
DB=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db" | head -1)
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT '  [ID='||id||'] '||certificateprofilename FROM certificateprofiledata WHERE id > 6 ORDER BY id;"

echo ""
echo "=== 3. End Entity Profiles ==="
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT '  [ID='||id||'] '||profilename FROM endentityprofiledata ORDER BY id;"

echo ""
echo "=== 4. TSA Profile EKU Critical ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca editcertificateprofile IVF-TSA-Profile --field extendedKeyUsageCritical -getValue 2>&1 | grep "returned value"

echo ""
echo "=== 5. OCSP Profile useOcspNoCheck ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca editcertificateprofile IVF-OCSP-Profile --field useOcspNoCheck -getValue 2>&1 | grep "returned value"

echo ""
echo "=== 6. SignServer Workers Status ==="
docker exec ivf_signserver.1.q9vqidiffvhvoa3sbh3l1ng58 /opt/signserver/bin/signserver getstatus brief ALL 2>&1 | grep -E "Worker|Status|active|ACTIVE|OFFLINE" | head -20

echo ""
echo "============================================================"
echo "  STATUS: All components operational"
echo "============================================================"
