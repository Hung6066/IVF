#!/bin/bash
# Run inside EJBCA container to enroll TSA cert
EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl

docker exec -i "$EJBCA_CONT" bash << 'EOF'
# Extract base64 CSR from PEM
CSR_B64=$(grep -v '^---' /tmp/ivf-csrs/worker100.csr | tr -d '\n')

# Write JSON payload explicitly 
cat > /tmp/enroll-payload.json << JSONEOF
{
  "certificate_request": "-----BEGIN CERTIFICATE REQUEST-----\n${CSR_B64}\n-----END CERTIFICATE REQUEST-----",
  "certificate_profile_name": "IVF-TSA-Profile",
  "end_entity_profile_name": "ENDUSER",
  "certificate_authority_name": "IVF-Signing-SubCA",
  "username": "timestampsigner-tsa4",
  "password": "signserver123",
  "include_chain": false
}
JSONEOF

echo "=== Payload preview ===" 
head -5 /tmp/enroll-payload.json

echo "=== Calling REST API with superadmin cert ==="
curl -v -X POST https://localhost:8443/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll \
  -H "Content-Type: application/json" \
  --cert /tmp/superadmin.p12 \
  --cert-type P12 \
  --pass superadmin123 \
  --insecure \
  -d @/tmp/enroll-payload.json \
  2>&1 | tail -30

EOF
