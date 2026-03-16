#!/bin/bash
# Delete old timestampsigner, recreate with correct profiles, issue TSA cert

EJBCA_CONT=ivf_ejbca.1.ccb2ewdjenp7ah17d62y73vvh
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

echo "=== Step 1: Revoke old timestampsigner certs and delete user from DB ==="

# Revoke old cert first (avoid "enforce unique DN" blocking)
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE certificatedata SET status = 40, revokedate = EXTRACT(EPOCH FROM now()) * 1000, revocationreason = 0 WHERE username = 'timestampsigner' AND status = 20" 2>&1

# Check cerificate table column name
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "SELECT username, status, subjectdn FROM certificatedata WHERE username = 'timestampsigner' LIMIT 3" 2>&1

# Delete user from userdata
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "DELETE FROM userdata WHERE username = 'timestampsigner'" 2>&1

echo "=== Step 2: Re-add timestampsigner with correct profiles ==="
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ra addendentity \
  --username timestampsigner \
  --password signserver123 \
  --dn 'CN=TimeStampSigner IVF,OU=TSA,O=IVF Healthcare,C=VN' \
  --caname IVF-Signing-SubCA \
  --type 1 \
  --token USERGENERATED \
  --certprofile IVF-TSA-Profile \
  --eeprofile IVF-TSA-EEProfile \
  2>&1 | tail -5

echo "=== Step 3: Issue TSA certificate ==="
docker exec "$EJBCA_CONT" bash -c 'mkdir -p /tmp/ivf-certs && rm -f /tmp/ivf-certs/worker100-tsa.pem'

docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner \
  --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr \
  -f /tmp/ivf-certs/worker100-tsa.pem \
  2>&1 | grep -v INFO

echo "=== Step 4: Verify certificate ==="
docker exec "$EJBCA_CONT" bash -c '
if test -f /tmp/ivf-certs/worker100-tsa.pem; then
  echo "TSA Certificate ISSUED!"
  openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -noout -subject 2>/dev/null
  openssl x509 -in /tmp/ivf-certs/worker100-tsa.pem -text -noout 2>/dev/null | grep -A5 "Extended Key Usage"
else
  echo "FAILED: No certificate"
fi
' 2>&1

echo "=== Step 5: Copy cert to host ==="
docker cp "${EJBCA_CONT}:/tmp/ivf-certs/worker100-tsa.pem" /tmp/ivf-certs/worker100-tsa.pem 2>&1 && echo "cert copied to host"
