#!/bin/bash
CERT="--cert /tmp/superadmin.crt --key /tmp/superadmin.key"

echo "=== List all profiles in database ==="
DB=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca-db" | head -1)
echo "-- Cert Profiles --"
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' | '||certificateprofilename||' (ID='||id||')' FROM certificateprofiledata WHERE id > 6 ORDER BY id;"

echo ""
echo "-- End Entity Profiles --"
docker exec "$DB" psql -U ejbca -d ejbca -t -A -c "SELECT id||' | '||profilename||' (ID='||id||')' FROM endentityprofiledata ORDER BY id;"

echo ""
echo "=== Export all profiles to verify ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c "mkdir -p /tmp/final-export && /opt/keyfactor/bin/ejbca.sh ca exportprofiles -d /tmp/final-export 2>&1 | grep -E 'Exporting|Filename|Added' | head -30"

echo ""
echo "=== Verify TSA profile has TSA EKU OID ==="
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c "grep -l '1.3.6.1.5.5.7.3.8' /tmp/final-export/*.xml 2>/dev/null || echo 'TSA OID not found in exported files'"
docker exec ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl sh -c "grep '1.3.6.1.5.5.7.3.8' /tmp/final-export/certprofile_IVF-TSA-Profile-5002.xml 2>/dev/null && echo 'TSA OID found' || echo 'TSA OID not in exported TSA profile'"

echo ""
echo "=== CA list from REST API ==="
curl -sk $CERT "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/ca" | python3 -c "import json,sys; d=json.load(sys.stdin); [print(f'  CA: {c[\"name\"]} ID={c[\"id\"]} expires={c[\"expiration_date\"][:10]}') for c in d['certificate_authorities']]" 2>/dev/null

echo ""
echo "=== Summary ==="
echo "Certificate Profiles created:"
echo "  IVF-PDFSigner-Profile (5001) - digitalSignature + nonRepudiation, 3y"
echo "  IVF-TSA-Profile (5002) - digitalSignature + EKU:timeStamping, 5y"
echo "  IVF-TLS-Client-Profile (5003) - digitalSignature + EKU:clientAuth, 2y"
echo "  IVF-OCSP-Profile (5004) - digitalSignature + EKU:OCSPSigning, 2y"
echo ""
echo "End Entity Profiles created:"
echo "  IVF-PDFSigner-EEProfile (6001) - CA: IVF-Signing-SubCA, CertProfile: IVF-PDFSigner-Profile"
echo "  IVF-TSA-EEProfile (6002) - CA: IVF-Signing-SubCA, CertProfile: IVF-TSA-Profile"
echo "  IVF-TLS-Client-EEProfile (6003) - CA: IVF-Signing-SubCA, CertProfile: IVF-TLS-Client-Profile"
