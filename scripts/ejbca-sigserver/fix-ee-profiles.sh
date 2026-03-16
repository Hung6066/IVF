#!/bin/bash
DB=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db" | head -1)

echo "=== Delete TEST-EE profile from DB ==="
docker exec "$DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'TEST-EE';"

echo ""
echo "=== List current EE profiles ==="
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' | '||profilename FROM endentityprofiledata ORDER BY profilename;"

echo ""
echo "=== Re-import IVF-PDFSigner-EEProfile with correct ID 6001 ==="
# Delete the auto-ID'd one
docker exec "$DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-PDFSigner-EEProfile';"

# Re-import from the original XML files which have ID 6001
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/ivf-ee-profiles 2>&1 | grep -v "^2026-03" | head -20

echo ""
echo "=== Final list of EE profiles ==="
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' | '||profilename FROM endentityprofiledata ORDER BY id;"

echo ""
echo "=== Export EE profiles to verify content ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c 'mkdir -p /tmp/ee-export && /opt/keyfactor/bin/ejbca.sh ca exportprofiles -d /tmp/ee-export 2>&1 | tail -15'
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c 'ls /tmp/ee-export/'
echo ""
echo "=== Check IVF-PDFSigner-EEProfile XML content (cert profile ref) ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c 'grep -E "AVAILABLE|DEFAULT" /tmp/ee-export/entityprofile_IVF-PDFSigner* 2>/dev/null | head -20'
