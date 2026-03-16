#!/bin/bash
# Update end entity cert profile and issue TSA cert

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Current state of tsa-cert-new ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, endentityprofileid, status FROM userdata WHERE username = 'tsa-cert-new'" 2>&1

echo "=== Updating cert profile to 5002 (IVF-TSA-Profile) ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5002 WHERE username = 'tsa-cert-new'" 2>&1

echo "=== Verify update ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, endentityprofileid, status FROM userdata WHERE username = 'tsa-cert-new'" 2>&1

echo "=== Issue TSA certificate ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username tsa-cert-new \
  --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr \
  -f /tmp/ivf-certs/worker100-tsa.pem \
  2>&1 | grep -v INFO

echo "=== Check if cert was issued ==="
if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/ivf-certs/worker100-tsa.pem'; then
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | grep -E 'Subject:|Issuer:|Time Stamping|Not After|ExtendedKeyUsage'
    echo "SUCCESS: Certificate issued!"
else
    echo "FAILED: Certificate not found at /tmp/ivf-certs/worker100-tsa.pem"
fi
