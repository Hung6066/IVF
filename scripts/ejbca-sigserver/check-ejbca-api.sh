#!/bin/bash
EJBCA_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca\." | grep -v db | head -1)
docker exec "$EJBCA_C" curl -sk https://127.0.0.1:8443/ejbca/ejbca-rest-api/openapi.json -o /tmp/ejbca-openapi.json
docker exec "$EJBCA_C" python3 -c "
import json
with open('/tmp/ejbca-openapi.json') as f:
    d = json.load(f)
for k in sorted(d['paths'].keys()):
    print(k)
" 2>&1 | head -60
