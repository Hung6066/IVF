#!/bin/bash
# Copy CSR to new EJBCA container and issue TSA cert

EJBCA_CONT=ivf_ejbca.1.ccb2ewdjenp7ah17d62y73vvh
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Copy CSR to new container ==="
docker exec "$EJBCA_CONT" mkdir -p /tmp/ivf-csrs /tmp/ivf-certs
docker cp /tmp/signcerts/worker100.csr "${EJBCA_CONT}:/tmp/ivf-csrs/worker100.csr"
echo "CSR copied"

echo "=== Verify timestampsigner status ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, endentityprofileid, status FROM userdata WHERE username = 'timestampsigner'" 2>&1

echo "=== Issue TSA certificate ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner \
  --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr \
  -f /tmp/ivf-certs/worker100-tsa.pem \
  2>&1 | grep -v INFO

echo "=== Verify certificate ==="
if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/ivf-certs/worker100-tsa.pem'; then
    echo "Certificate file found. Checking Extended Key Usage:"
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | \
      grep -A5 "Extended Key Usage"
    echo ""
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | \
      grep -E "Subject:|Issuer:|Not After"
    echo "SUCCESS: TSA Certificate issued!"
else
    echo "FAILED: Certificate not found"
fi
