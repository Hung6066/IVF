#!/bin/bash
# Create End Entity Profile XMLs for EJBCA
# SubCA ID: 1728368285 (IVF-Signing-SubCA)
# Cert Profile IDs: 5001 (PDFSigner), 5002 (TSA), 5003 (TLS-Client), 5004 (OCSP)

SUBCA_ID=1728368285
mkdir -p /tmp/ivf-ee-profiles

# Clear EJBCA cache to ensure cert profiles are recognized
echo "=== Clearing EJBCA cache ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh clearcache 2>&1 | tail -3

# Helper function to create an EE profile XML
# Args: $1=name, $2=id, $3=certprofile_ids (space-separated), $4=default_certprofile_id
create_ee_profile() {
  local name="$1"
  local id="$2"
  local certprofile_ids="$3"
  local default_cp="$4"

  local filepath="/tmp/ivf-ee-profiles/entityprofile_${name}-${id}.xml"

  cat > "$filepath" << XMLHDR
<?xml version="1.0" encoding="UTF-8"?>
<java version="17.0.16" class="java.beans.XMLDecoder">
 <object class="java.util.LinkedHashMap">
  <void method="put">
   <string>version</string>
   <float>20.0</float>
  </void>
  <void method="put">
   <string>CN0</string>
   <string></string>
  </void>
  <void method="put">
   <string>CN0use</string>
   <boolean>true</boolean>
  </void>
  <void method="put">
   <string>CN0required</string>
   <boolean>true</boolean>
  </void>
  <void method="put">
   <string>CN0modifiable</string>
   <boolean>true</boolean>
  </void>
  <void method="put">
   <string>CN0type</string>
   <int>4</int>
  </void>
  <void method="put">
   <string>O0</string>
   <string>IVF Healthcare</string>
  </void>
  <void method="put">
   <string>O0use</string>
   <boolean>true</boolean>
  </void>
  <void method="put">
   <string>O0required</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>O0modifiable</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>O0type</string>
   <int>0</int>
  </void>
  <void method="put">
   <string>C0</string>
   <string>VN</string>
  </void>
  <void method="put">
   <string>C0use</string>
   <boolean>true</boolean>
  </void>
  <void method="put">
   <string>C0required</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>C0modifiable</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>C0type</string>
   <int>0</int>
  </void>
  <void method="put">
   <string>DEFAULTCERTPROFILE</string>
   <int>${default_cp}</int>
  </void>
  <void method="put">
   <string>AVAILABLECERTPROFILES</string>
   <object class="java.util.ArrayList">
XMLHDR

  for cpid in $certprofile_ids; do
    echo "    <void method=\"add\"><int>${cpid}</int></void>" >> "$filepath"
  done

  cat >> "$filepath" << XMLFTR1
   </object>
  </void>
  <void method="put">
   <string>DEFAULTCA</string>
   <int>${SUBCA_ID}</int>
  </void>
  <void method="put">
   <string>AVAILABLECAS</string>
   <object class="java.util.ArrayList">
    <void method="add">
     <int>${SUBCA_ID}</int>
    </void>
   </object>
  </void>
  <void method="put">
   <string>NUMBEROFPARAMETERS</string>
   <int>0</int>
  </void>
  <void method="put">
   <string>PRINTINGUSE</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>PRINTINGDEFAULT</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>SENDNOTIFICATION0</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>USENAMECONSTRAINTS</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>USEEXTENDEDINFORMATION</string>
   <boolean>false</boolean>
  </void>
  <void method="put">
   <string>USEKEYRECOVERABLE</string>
   <boolean>false</boolean>
  </void>
 </object>
</java>
XMLFTR1

  echo "Created: $filepath"
}

# Create 3 EE profiles
# 1. IVF-PDFSigner-EEProfile: uses cert profile 5001, CA=SubCA
create_ee_profile "IVF-PDFSigner-EEProfile" "6001" "5001" "5001"

# 2. IVF-TSA-EEProfile: uses cert profile 5002, CA=SubCA
create_ee_profile "IVF-TSA-EEProfile" "6002" "5002" "5002"

# 3. IVF-TLS-Client-EEProfile: uses cert profile 5003, CA=SubCA
create_ee_profile "IVF-TLS-Client-EEProfile" "6003" "5003" "5003"

echo ""
echo "=== Files created ==="
ls -la /tmp/ivf-ee-profiles/

echo ""
echo "=== Import EE profiles ==="
# Copy to container
for f in /tmp/ivf-ee-profiles/*.xml; do
  docker cp "$f" ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl:/tmp/$(basename "$f")
done
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c 'mkdir -p /tmp/ivf-ee-profiles && for f in /tmp/entityprofile_IVF*; do cp "$f" /tmp/ivf-ee-profiles/ 2>/dev/null; done; ls /tmp/ivf-ee-profiles/'
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl /opt/keyfactor/bin/ejbca.sh ca importprofiles -d /tmp/ivf-ee-profiles 2>&1 | grep -v "^2026-03" | head -30
