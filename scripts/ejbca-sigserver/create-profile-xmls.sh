#!/bin/bash
# Create complete certificate profile XML files for EJBCA import
# These files follow EJBCA's Java XMLDecoder format for CertificateProfile

mkdir -p /tmp/ivf-profiles

# ======================================================
# Helper function to create a certificate profile XML
# ======================================================
create_cert_profile() {
  local name="$1"
  local id="$2"
  local validity="$3"
  local eku_oids="$4"  # space-separated OIDs or empty
  local nonrepudiation="$5"  # "true" or "false"
  local filepath="/tmp/ivf-profiles/certprofile_${name}-${id}.xml"

  cat > "$filepath" << XMLEOF
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

  <void method="put">
    <string>availablekeyalgorithms</string>
    <object class="java.util.ArrayList">
      <void method="add"><string>RSA</string></void>
      <void method="add"><string>ECDSA</string></void>
    </object>
  </void>

  <void method="put">
    <string>availableECCurvesAsString</string>
    <object class="java.util.ArrayList">
      <void method="add"><string>ANY_EC_CURVE</string></void>
    </object>
  </void>

  <void method="put">
    <string>signingalgorithm</string>
    <string>-1</string>
  </void>

  <void method="put">
    <string>encodedvalidity</string>
    <string>${validity}</string>
  </void>

  <void method="put">
    <string>allowvalidityoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowkeyusageoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowextensionoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowdnoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecertificatestorage</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>storecertificatedata</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>storesubjectaltname</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>availablebitlenghts</string>
    <object class="java.util.ArrayList">
      <void method="add"><int>2048</int></void>
      <void method="add"><int>3072</int></void>
      <void method="add"><int>4096</int></void>
    </object>
  </void>

  <void method="put">
    <string>minimumavailablebitlength</string>
    <int>0</int>
  </void>

  <void method="put">
    <string>maximumavailablebitlength</string>
    <int>0</int>
  </void>

  <void method="put">
    <string>keyusage</string>
    <object class="java.util.ArrayList">
      <void method="add"><boolean>true</boolean></void>
      <void method="add"><boolean>${nonrepudiation}</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
    </object>
  </void>

  <void method="put">
    <string>keyusagecritical</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>extendedkeyusagecritical</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usesubjectaltname</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>useissueralternativename</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecrldistributionpoint</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usedefaultcrldistributionpoint</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>useauthorityinformationaccess</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usedefaultocspservicelocator</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>ocspservicelocatoruri</string>
    <string></string>
  </void>

  <void method="put">
    <string>usesubjectdirattributes</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>useocspnocheck</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecaissuer</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usefreshestcrl</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>availablecas</string>
    <object class="java.util.ArrayList">
      <void method="add"><int>-1</int></void>
    </object>
  </void>

XMLEOF

  # Add EKU OIDs if provided
  if [ -n "$eku_oids" ]; then
    cat >> "$filepath" << EKUEOF
  <void method="put">
    <string>extendedkeyusage</string>
    <object class="java.util.ArrayList">
EKUEOF
    for oid in $eku_oids; do
      echo "      <void method=\"add\"><string>${oid}</string></void>" >> "$filepath"
    done
    cat >> "$filepath" << EKUEOF2
    </object>
  </void>
EKUEOF2
  else
    cat >> "$filepath" << EKUEOF3
  <void method="put">
    <string>extendedkeyusage</string>
    <object class="java.util.ArrayList">
    </object>
  </void>
EKUEOF3
  fi

  # Close the XML
  cat >> "$filepath" << XMLEOF2

</object>
</java>
XMLEOF2

  echo "Created: $filepath"
}

# ======================================================
# Create the 4 certificate profiles
# ======================================================

# 1. IVF-PDFSigner-Profile: digitalSignature + nonRepudiation, 3y, no EKU
create_cert_profile "IVF-PDFSigner-Profile" "5001" "3y" "" "true"

# 2. IVF-TSA-Profile: digitalSignature only, 5y, EKU: timeStamping (critical)
# Note: TSA EKU critical is set via extendedkeyusagecritical
create_cert_profile "IVF-TSA-Profile" "5002" "5y" "1.3.6.1.5.5.7.3.8" "false"
# Override extendedkeyusagecritical to true for TSA
sed -i 's|<string>extendedkeyusagecritical</string>\n    <boolean>false</boolean>|<string>extendedkeyusagecritical</string>\n    <boolean>true</boolean>|g' /tmp/ivf-profiles/certprofile_IVF-TSA-Profile-5002.xml

# 3. IVF-TLS-Client-Profile: digitalSignature, 2y, EKU: clientAuth
create_cert_profile "IVF-TLS-Client-Profile" "5003" "2y" "1.3.6.1.5.5.7.3.2" "false"

# 4. IVF-OCSP-Profile: digitalSignature, 2y, EKU: OCSPSigning, useOCSPnoCheck
create_cert_profile "IVF-OCSP-Profile" "5004" "2y" "1.3.6.1.5.5.7.3.9" "false"

echo ""
echo "=== Files created ==="
ls -la /tmp/ivf-profiles/
