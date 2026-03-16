#!/bin/bash
EJBCA_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca\." | grep -v db | head -1)
echo "Container: $EJBCA_C"

# First check if openapi.json exists inside container
docker exec "$EJBCA_C" ls -la /tmp/ejbca-openapi.json 2>&1 || {
  echo "Downloading openapi to container..."
  docker exec "$EJBCA_C" sh -c 'curl -sk https://127.0.0.1:8443/ejbca/swagger-ui/openapi.json -o /tmp/ejbca-openapi.json && echo "Downloaded" || echo "Failed"'
}

# Copy to host
docker cp "${EJBCA_C}:/tmp/ejbca-openapi.json" /tmp/ejbca-openapi.json && echo "Copied to host" || echo "Copy failed"

# Show size
ls -la /tmp/ejbca-openapi.json

# Try to extract paths
grep -o '"\/ejbca[^"]*"' /tmp/ejbca-openapi.json | sort -u | head -60
