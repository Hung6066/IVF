#!/bin/bash
EJBCA_DB_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db")
echo "DB: $EJBCA_DB_C"
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT id, certificateprofilename FROM certificateprofiledata ORDER BY id;"
echo "---"
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT id, profilename FROM endentityprofiledata ORDER BY id;"
