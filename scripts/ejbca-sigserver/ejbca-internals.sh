#!/bin/bash
echo "=== Check ejbca dist/upgrade/profiles ==="
find /opt/keyfactor/ejbca/ -name "*.xml" 2>/dev/null | head -30
ls /opt/keyfactor/ejbca/dist/ 2>/dev/null
ls /opt/keyfactor/ejbca/dist/profiles/ 2>/dev/null

echo ""
echo "=== Check for sample profiles in WAR or dist ==="
find /opt/keyfactor/ejbca/dist -name "*.xml" 2>/dev/null | head -10
find /opt/keyfactor/ejbca/doc -name "*profile*" 2>/dev/null | head -10

echo ""
echo "=== after-deployed script ==="
cat /opt/keyfactor/bin/internal/after-deployed.sh 2>/dev/null | head -60

echo ""
echo "=== functions-ejbca (profile-related excerpts) ==="
cat /opt/keyfactor/bin/internal/functions-ejbca 2>/dev/null | head -100
