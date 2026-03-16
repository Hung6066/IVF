#!/bin/bash
EJBCA_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca\." | grep -v db | head -1)
echo "Container: $EJBCA_C"

# Try different OpenAPI URL patterns
for url in \
  "https://127.0.0.1:8443/ejbca/swagger-ui/openapi.json" \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/openapi.json" \
  "https://127.0.0.1:8443/ejbca/ejbca-rest-api/openapi.json" \
  "https://127.0.0.1:8443/ejbca-rest-api/swagger.json" \
  "https://127.0.0.1:8443/ejbca/swagger-ui" \
  "https://127.0.0.1:8443/ejbca/swagger/openapi.json"; do
  echo -n "Testing $url ... "
  result=$(docker exec "$EJBCA_C" curl -sk -o /dev/null -w "%{http_code}" "$url")
  echo "HTTP $result"
done

echo ""
echo "=== Check what REST endpoints are actually available ==="
docker exec "$EJBCA_C" curl -sk "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/certificate/status" | head -c 200
echo ""

# Try configdump endpoint
echo "=== Configdump download test ==="
docker exec "$EJBCA_C" curl -sk -w "\nHTTP:%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v1/configdump" | tail -5
echo ""
docker exec "$EJBCA_C" curl -sk -w "\nHTTP:%{http_code}" "https://127.0.0.1:8443/ejbca/ejbca-rest-api/v2/configdump" | tail -5
