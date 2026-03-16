#!/bin/bash
# Use existing timestampsigner user, change cert profile to IVF-TSA-Profile, reissue

EJBCA_CONT=ivf_ejbca.1.reezcbeaxqe1jaizaxq82tyfl
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Check timestampsigner user ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, endentityprofileid, status, subjectdn FROM userdata WHERE username = 'timestampsigner'" 2>&1

echo "=== Update cert profile to ID 5002 (IVF-TSA-Profile) and status to NEW(10) ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5002, status = 10 WHERE username = 'timestampsigner'" 2>&1

echo "=== Verify update ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, certificateprofileid, status FROM userdata WHERE username = 'timestampsigner'" 2>&1

echo "=== Delete old cert file if exists ==="
docker exec "$EJBCA_CONT" bash -c "rm -f /tmp/ivf-certs/worker100-tsa.pem; mkdir -p /tmp/ivf-certs"

echo "=== Issue TSA certificate using IVF-TSA-Profile ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner \
  --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr \
  -f /tmp/ivf-certs/worker100-tsa.pem \
  2>&1 | grep -v INFO

echo "=== Verify certificate ==="
if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/ivf-certs/worker100-tsa.pem'; then
    echo "Certificate file found. Checking contents:"
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | \
      grep -E 'Subject:|Issuer:|Not After|Extended Key Usage|Time Stamp'
    echo "SUCCESS: Certificate issued!"
else
    echo "FAILED: Certificate not issued"
fi
