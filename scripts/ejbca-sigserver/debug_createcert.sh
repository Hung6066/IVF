#!/bin/bash
# Debug: test createcert with IVF-PDFSigner-Profile (5001) to isolate key length issue

EJBCA_CONT=ivf_ejbca.1.ccb2ewdjenp7ah17d62y73vvh
EJBCA_DB=ivf_ejbca-db.1.1g7nulwd6yvd7biyqbezkx5cx

# Step 1: Try PDFSigner profile (5001) which worked for workers 1-907
echo "=== Test 1: Try IVF-PDFSigner-Profile (5001) ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5001, endentityprofileid = 1, status = 10 WHERE username = 'timestampsigner'" 2>&1

docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr -f /tmp/test-pdfsigner.pem \
  2>&1 | grep -v INFO

if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/test-pdfsigner.pem'; then
    echo "Test 1 SUCCESS with IVF-PDFSigner-Profile"
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/test-pdfsigner.pem -noout -subject 2>/dev/null
else
    echo "Test 1 FAILED with IVF-PDFSigner-Profile"
fi

# Step 2: Try with profile 5002 but EE profile 1 (ENDUSER)  
echo ""
echo "=== Test 2: IVF-TSA-Profile (5002) + ENDUSER EE profile ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5002, endentityprofileid = 1, status = 10 WHERE username = 'timestampsigner'" 2>&1

docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr -f /tmp/test-tsa-ee1.pem \
  2>&1 | grep -v INFO

if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/test-tsa-ee1.pem'; then
    echo "Test 2 SUCCESS"
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/test-tsa-ee1.pem -noout -subject 2>/dev/null
else
    echo "Test 2 FAILED with IVF-TSA-Profile + ENDUSER EE"
fi

# Step 3: Try with profile 5002 and EE profile 6002
echo ""
echo "=== Test 3: IVF-TSA-Profile (5002) + IVF-TSA-EEProfile (6002) ==="
docker exec "$EJBCA_DB" psql -U ejbca -d ejbca -c \
  "UPDATE userdata SET certificateprofileid = 5002, endentityprofileid = 6002, status = 10 WHERE username = 'timestampsigner'" 2>&1

docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh createcert \
  --username timestampsigner --password signserver123 \
  -c /tmp/ivf-csrs/worker100.csr -f /tmp/test-tsa-ee6002.pem \
  2>&1 | grep -v INFO

if docker exec "$EJBCA_CONT" bash -c 'test -f /tmp/test-tsa-ee6002.pem'; then
    echo "Test 3 SUCCESS"
    docker exec "$EJBCA_CONT" openssl x509 -in /tmp/test-tsa-ee6002.pem -noout -subject 2>/dev/null
else
    echo "Test 3 FAILED"
fi
