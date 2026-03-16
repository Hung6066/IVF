#!/bin/bash
echo "=== Find configdump binary ==="
ls /opt/keyfactor/bin/
ls /opt/keyfactor/bin/internal/ 2>/dev/null

echo ""
echo "=== Look for configdump anywhere ==="
ls /opt/keyfactor/ejbca/ 2>/dev/null | head -30

echo ""
echo "=== Check ejbca webapp structure (WAR) ==="
find /opt/keyfactor/appserver/standalone/deployments/ -name "*.war" 2>/dev/null | head -5
find /opt/keyfactor/appserver/standalone/deployments/ -name "*.deploy" 2>/dev/null | head -10

echo ""
echo "=== Check configdump directory ==="
ls -la /opt/keyfactor/configdump/ 2>/dev/null
ls -la /opt/keyfactor/configdump/stage.d/ 2>/dev/null
ls -la /opt/keyfactor/configdump/initialize.d/ 2>/dev/null
