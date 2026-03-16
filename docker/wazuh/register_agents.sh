#!/bin/bash
# Register Wazuh agents via API and configure keys

set -e

API_USER="wazuh-wui"
API_PASS="0TLUTyAWNN5Xk0Gb9aeXdktR2Pp4Ww"
API_URL="https://127.0.0.1:55000"

echo "=== Authenticating with Wazuh API ==="
TOKEN=$(curl -su "$API_USER:$API_PASS" -k -X POST "$API_URL/security/user/authenticate?raw=true")
if [ -z "$TOKEN" ]; then
  echo "ERROR: Failed to get API token"
  exit 1
fi
echo "Token obtained"

echo ""
echo "=== Registering VPS2 agent ==="
RESP1=$(curl -sk -X POST "$API_URL/agents" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"vps2-vmi3129111","ip":"any"}')
echo "$RESP1"
KEY1=$(echo "$RESP1" | python3 -c "import sys,json; print(json.load(sys.stdin)['data']['key'])" 2>/dev/null || true)

echo ""
echo "=== Registering VPS1 agent ==="
RESP2=$(curl -sk -X POST "$API_URL/agents" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"vps1-vmi3129107","ip":"any"}')
echo "$RESP2"
KEY2=$(echo "$RESP2" | python3 -c "import sys,json; print(json.load(sys.stdin)['data']['key'])" 2>/dev/null || true)

echo ""
echo "=== Importing VPS2 key ==="
if [ -n "$KEY1" ]; then
  /var/ossec/bin/manage_agents -i "$KEY1" <<< "y"
  echo "VPS2 key imported"
else
  echo "ERROR: No key for VPS2"
fi

echo ""
echo "=== Restarting VPS2 agent ==="
systemctl restart wazuh-agent
sleep 3

echo ""
echo "=== VPS2 agent status ==="
systemctl status wazuh-agent --no-pager | head -10
grep -E "Connected|ERROR" /var/ossec/logs/ossec.log | tail -5

echo ""
echo "=== VPS1 key (import on vps1) ==="
echo "KEY2=$KEY2"
echo ""
echo "DONE"
