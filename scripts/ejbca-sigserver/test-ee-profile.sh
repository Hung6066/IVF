#!/bin/bash
# Create a minimal EndEntityProfile XML and test import to see what fields are required

# CA IDs from our installation
SUBCA_ID=1728368285

mkdir -p /tmp/ee-profiles

# Minimal EE profile to test
cat > /tmp/ee-profiles/entityprofile_TEST-EE-6001.xml << 'XMLEOF'
<?xml version="1.0" encoding="UTF-8"?>
<java version="17.0.16" class="java.beans.XMLDecoder">
 <object class="java.util.LinkedHashMap">
  <void method="put">
   <string>version</string>
   <float>20.0</float>
  </void>
  <void method="put">
   <string>AVAILABLECAS</string>
   <object class="java.util.ArrayList">
    <void method="add">
     <int>-1</int>
    </void>
   </object>
  </void>
  <void method="put">
   <string>AVAILABLECERTPROFILES</string>
   <object class="java.util.ArrayList">
    <void method="add">
     <int>5001</int>
    </void>
   </object>
  </void>
 </object>
</java>
XMLEOF

echo "=== Testing minimal EE profile import ==="
docker cp /tmp/ee-profiles/entityprofile_TEST-EE-6001.xml ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl:/tmp/ee-profiles/entityprofile_TEST-EE-6001.xml 2>/dev/null

docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c 'mkdir -p /tmp/ee-profiles ; cp /tmp/entityprofile_TEST-EE-6001.xml /tmp/ee-profiles/ 2>/dev/null; true'
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/ee-profiles 2>&1 | grep -v "^2026-03" | head -30

echo ""
echo "Exit: $?"
