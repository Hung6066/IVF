#!/bin/bash
set -e

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl

docker exec "$EJBCA_CONT" bash << 'INNEREOF'
set -e

CSR_LINE=$(awk '/BEGIN CERTIFICATE REQUEST/{p=1;next}/END CERTIFICATE REQUEST/{p=0;next}p' /tmp/ivf-csrs/worker100.csr | tr -d '\n')

cat > /tmp/tsa-payload.json << JSONEOF
{
  "certificate_request": "-----BEGIN CERTIFICATE REQUEST-----\n${CSR_LINE}\n-----END CERTIFICATE REQUEST-----",
  "certificate_profile_name": "IVF-TSA-Profile",
  "end_entity_profile_name": "ENDUSER",
  "certificate_authority_name": "IVF-Signing-SubCA",
  "username": "timestampsigner-tsa3",
  "password": "signserver123",
  "include_chain": false
}
JSONEOF

echo "Payload prepared. Enrolling..."
curl -sk -X POST https://localhost:8443/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll \
  -H "Content-Type: application/json" \
  --cert /tmp/superadmin.p12 --cert-type P12 --pass superadmin123 \
  -d @/tmp/tsa-payload.json > /tmp/tsa-response.json 2>&1

echo "Response:"
cat /tmp/tsa-response.json | head -5
INNEREOF
