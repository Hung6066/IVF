#!/bin/bash
# Two-phase import: cert profiles first, then EE profiles
# Avoids EJBCA EJB cache issue where EE profile import can't see freshly-imported cert profiles

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Step 1: Delete TSA cert profile and EE profile from DB ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM certificateprofiledata WHERE certificateprofilename = 'IVF-TSA-Profile'" 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-TSA-EEProfile'" 2>&1

echo "=== Step 2: Phase 1 - Import cert profiles only ==="
mkdir -p /tmp/certprofiles-only
docker cp "${EJBCA_CONT}:/tmp/profiles/." /tmp/certprofiles-only/
# Only keep cert profiles (not entity profiles)
rm -f /tmp/certprofiles-only/entityprofile_*

echo "Files to import (cert profiles only):"
ls /tmp/certprofiles-only/

docker cp /tmp/certprofiles-only/. "${EJBCA_CONT}:/tmp/certprofiles-only/"
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/certprofiles-only 2>&1 | grep -v '^.*INFO'

echo ""
echo "=== Step 3: Wait 5 seconds for EJB session cache refresh ==="
sleep 5

echo "=== Step 4: Phase 2 - Import EE profiles only (from ee-only dir) ==="
# Use the fixed EE profiles we already prepared
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-TSA-EEProfile'" 2>&1

docker exec "$EJBCA_CONT" mkdir -p /tmp/ee-import
docker cp /tmp/ee-only/entityprofile_IVF-TSA-EEProfile-6002.xml "${EJBCA_CONT}:/tmp/ee-import/"

docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/ee-import 2>&1 | grep -v '^.*INFO'

echo ""
echo "=== Step 5: Final verification ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT certificateprofilename, id FROM certificateprofiledata ORDER BY certificateprofilename' 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Testing addendentity with IVF-TSA-Profile ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ra addendentity \
  --username timestampsigner-test \
  --password signserver123 \
  --dn 'CN=TimeStampSigner IVF,OU=TSA,O=IVF Healthcare,C=VN' \
  --caname IVF-Signing-SubCA \
  --type 1 \
  --token USERGENERATED \
  --certprofile IVF-TSA-Profile \
  --eeprofile IVF-TSA-EEProfile \
  2>&1 | grep -v '^.*INFO'

echo "=== Done ==="
