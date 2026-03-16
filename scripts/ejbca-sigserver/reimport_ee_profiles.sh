#!/bin/bash
# Fix EE profiles: delete from DB and reimport from ee-only dir

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Step 1: Check current EE profiles ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Step 2: Delete broken EE profiles ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-TSA-EEProfile'" 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-PDFSigner-EEProfile'" 2>&1

echo "=== Step 3: Copy fixed EE profiles to EJBCA container ==="
mkdir -p /tmp/ee-import
cp /tmp/ee-only/entityprofile_IVF-TSA-EEProfile-6002.xml /tmp/ee-import/
cp /tmp/ee-only/entityprofile_IVF-PDFSigner-EEProfile-6001.xml /tmp/ee-import/

docker exec "$EJBCA_CONT" mkdir -p /tmp/ee-import
docker cp /tmp/ee-import/entityprofile_IVF-TSA-EEProfile-6002.xml "${EJBCA_CONT}:/tmp/ee-import/"
docker cp /tmp/ee-import/entityprofile_IVF-PDFSigner-EEProfile-6001.xml "${EJBCA_CONT}:/tmp/ee-import/"

echo "=== Step 4: Import EE profiles only (no cert profiles in dir) ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/ee-import 2>&1 | grep -v '^.*INFO'

echo "=== Step 5: Verify profiles in DB ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Done ==="
