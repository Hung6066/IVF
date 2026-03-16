#!/bin/bash
echo "=== Check if superadmin P12 exists ==="
ls -la /tmp/superadmin_new/ 2>&1

echo ""
echo "=== Try to understand the P12 issue with Java keytool ==="
keytool -list -keystore /tmp/superadmin_new/superadmin.p12 -storetype PKCS12 -storepass changeit 2>&1 | head -20

echo ""
echo "=== Extract sample profiles from EAR JARs ==="
cd /tmp
mkdir -p jar-extract
# Copy the EAR to tmp
cp /opt/keyfactor/ejbca/dist/ejbca.ear /tmp/jar-extract/

# List the EAR content
cd /tmp/jar-extract
unzip -l ejbca.ear 2>&1 | head -30

# Extract JARs
unzip -o ejbca.ear "*.jar" -d /tmp/jar-extract/ 2>&1 | tail -5

# Find profile resources
for jar in /tmp/jar-extract/*.jar; do
  result=$(unzip -l "$jar" 2>/dev/null | grep -i "profile.*xml\|certprofile")
  if [ -n "$result" ]; then
    echo "=== Found in $jar ==="
    echo "$result"
  fi
done
