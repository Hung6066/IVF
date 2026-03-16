#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "=== Check more REST paths ==="
for path in "v1/ca/1031502430" "v1/cainfo" "v1/endentity/query" "v1/endentities" "v1/endentityprofile"; do
  code=$(curl -sk $CERT -o /tmp/rest-test.json -w "%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/${path}")
  echo -n "  ${path} -> HTTP ${code} "
  if [ "$code" != "404" ]; then
    cat /tmp/rest-test.json | head -c 100
  fi
  echo ""
done

echo ""
echo "=== Check EJBCA version (from REST) ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/certificate/status"
echo ""

echo ""
echo "=== Get ejbca.sh ca subcommands ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca 2>&1 | grep -v "^2026" | grep -v "^----" | head -50

echo ""
echo "=== Test importprofiles with dummy dir ==="
mkdir -p /tmp/test-profiles
# Create minimal XML test
cat > /tmp/test-profiles/certprofile_TEST-5001.xml << 'XMLEOF'
<?xml version="1.0" encoding="UTF-8"?>
<java version="1.8.0" class="java.beans.XMLDecoder">
<object class="java.util.LinkedHashMap">
<void method="put">
<string>version</string>
<float>43.0</float>
</void>
<void method="put">
<string>type</string>
<int>1</int>
</void>
</object>
</java>
XMLEOF

# Copy to container and test import
docker cp /tmp/test-profiles/certprofile_TEST-5001.xml ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl:/tmp/certprofile_TEST-5001.xml
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl mkdir -p /tmp/test-profiles
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl cp /tmp/certprofile_TEST-5001.xml /tmp/test-profiles/
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/test-profiles 2>&1 | grep -v "^2026"
