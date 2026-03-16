#!/bin/bash
DB=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db" | head -1)
echo "=== Cert Profiles ==="
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' - '||certificateprofilename FROM certificateprofiledata ORDER BY id;"

echo ""
echo "=== End Entity Profiles ==="
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' - '||profilename FROM endentityprofiledata ORDER BY id;"
