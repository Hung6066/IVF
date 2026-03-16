#!/bin/bash
# Fix TSA EE Profile by deleting from DB and reimporting

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

# Step 1: Find and show EE profiles in DB
echo "=== Current EE profiles in DB ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -t -c "SELECT profilename, id FROM endentityprofiledata ORDER BY profilename;" 2>/dev/null

# Step 2: Delete the broken TSA EE profile from DB
echo "=== Deleting IVF-TSA-EEProfile ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-TSA-EEProfile';" 2>/dev/null

echo "=== Deleting IVF-PDFSigner-EEProfile ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-PDFSigner-EEProfile';" 2>/dev/null

# Step 3: Copy fixed profile into EJBCA container
docker cp /tmp/tsa-fixed-host.xml "${EJBCA_CONT}:/tmp/profiles/entityprofile_IVF-TSA-EEProfile-6002.xml"
echo "Copied fixed TSA EE profile to container"

# Step 4: Also fix the PDFSigner EE profile (same bug)
# Copy existing profile, apply same fix
docker exec "$EJBCA_CONT" bash << 'INNEREOF'
python3 -c "
import re

# Use the same fix on PDFSigner EE profile
with open('/tmp/profiles/entityprofile_IVF-PDFSigner-EEProfile-6001.xml') as f:
    content = f.read()

# Remove integer-keyed SAN entries
pattern = r'\s*<void method=\"put\">\s*<int>\d+</int>\s*<string>[^<]*</string>\s*</void>'
cleaned = re.sub(pattern, '', content)

# Add empty SUBJECTALTNAME_FIELDORDER before PROFILETYPE
insert = '  <void method=\"put\">\n   <string>SUBJECTALTNAME_FIELDORDER</string>\n   <object class=\"java.util.ArrayList\"/>\n  </void>\n  '
needle = '  <void method=\"put\">\n   <string>PROFILETYPE</string>'
if needle in cleaned:
    cleaned = cleaned.replace(needle, insert + '<void method=\"put\">\n   <string>PROFILETYPE</string>', 1)
    print('PDFSigner EE profile fixed')
else:
    print('ERROR: needle not found in PDFSigner EE profile!')

with open('/tmp/profiles/entityprofile_IVF-PDFSigner-EEProfile-6001.xml', 'w') as f:
    f.write(cleaned)
" 2>&1 || echo "python3 not in container, skipping PDFSigner fix"
INNEREOF

# Step 5: Delete PDFSigner EE profile too and reimport
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c "DELETE FROM endentityprofiledata WHERE profilename = 'IVF-PDFSigner-EEProfile';" 2>/dev/null

# Step 6: Reimport all profiles
echo "=== Reimporting profiles ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/profiles 2>&1 | grep -v "^.*INFO"

echo "=== Done ==="
