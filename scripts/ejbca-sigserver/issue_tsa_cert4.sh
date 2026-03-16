#!/bin/bash
# Fix timestampsigner to use IVF-TSA-EEProfile (6002) and IVF-TSA-Profile (5002)

EJBCA_CONT=ivf_ejbca.1.ccb2ewdjenp7ah17d62y73vvh
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Update timestampsigner to use correct profiles ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5002, endentityprofileid = 6002, status = 10 WHERE username = 'timestampsigner'" 2>&1

echo "=== Verify update ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, endentityprofileid, status FROM userdata WHERE username = 'timestampsigner'" 2>&1

echo "=== Copy CSR to container ==="
docker exec "$EJBCA_CONT" mkdir -p /tmp/ivf-csrs /tmp/ivf-certs 2>/dev/null
docker cp /tmp/ivf-certs/worker100.csr "${EJBCA_CONT}:/tmp/ivf-csrs/worker100.csr" 2>&1

echo "=== Issue TSA certificate ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner \
  --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr \
  -f /tmp/ivf-certs/worker100-tsa.pem \
  2>&1 | grep -v INFO

echo "=== Check certificate ==="
docker exec "$EJBCA_CONT" bash -c '
if test -f /tmp/ivf-certs/worker100-tsa.pem; then
  echo "Certificate issued!"
  openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | grep -A5 "Extended Key Usage"
  openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | grep "Subject: \|Not After"
else
  echo "FAILED: No certificate found"
fi
' 2>&1

echo ""
echo "=== Copy cert to host for SignServer ==="
docker cp "${EJBCA_CONT}:/tmp/ivf-certs/worker100-tsa.pem" /tmp/ivf-certs/worker100-tsa.pem 2>&1 && echo "Cert copied to host at /tmp/ivf-certs/worker100-tsa.pem"
