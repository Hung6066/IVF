#!/bin/bash
# Nuclear option: delete TSA cert/ee profile and reimport fresh

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Step 1: Current state ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT certificateprofilename, id FROM certificateprofiledata ORDER BY certificateprofilename' 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Step 2: Delete TSA cert profile and EE profile ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM certificateprofiledata WHERE certificateprofilename = 'IVF-TSA-Profile'" 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-TSA-EEProfile'" 2>&1

echo "=== Step 3: Verify deletions ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT certificateprofilename, id FROM certificateprofiledata ORDER BY certificateprofilename' 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Step 4: Copy fixed EE profile (remove broken SAN int keys) ==="
docker cp /tmp/tsa-fixed-host.xml "${EJBCA_CONT}:/tmp/profiles/entityprofile_IVF-TSA-EEProfile-6002.xml"

echo "=== Step 5: Reimport from the full profiles directory ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/profiles 2>&1 | grep -v '^.*INFO'

echo "=== Step 6: Verify final state ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT certificateprofilename, id FROM certificateprofiledata ORDER BY certificateprofilename' 2>&1
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c 'SELECT profilename, id FROM endentityprofiledata ORDER BY profilename' 2>&1

echo "=== Done ==="
