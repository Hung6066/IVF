#!/bin/bash
EJBCA_DB_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db")
echo "DB container: $EJBCA_DB_C"

echo ""
echo "=== CertificateProfileData columns ==="
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "\d certificateprofiledata" 2>&1 | head -20

echo ""
echo "=== List cert profiles ==="
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT * FROM certificateprofiledata LIMIT 10;" 2>&1

echo ""
echo "=== List EE profiles ==="
docker exec "${EJBCA_DB_C}" psql -U ejbca -d ejbca -c "SELECT * FROM endentityprofiledata LIMIT 10;" 2>&1

echo ""
echo "=== Clear EJBCA cache ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh clearcache 2>&1 | tail -3
