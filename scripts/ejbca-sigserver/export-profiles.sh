#!/bin/bash
echo "=== Export existing profiles ==="
mkdir -p /tmp/exported-profiles
/opt/keyfactor/bin/ejbca.sh ca exportprofiles -d /tmp/exported-profiles 2>&1 | grep -v "^2026"

echo ""
echo "=== Files exported ==="
ls -la /tmp/exported-profiles/ 2>&1

echo ""
echo "=== Check if ENDUSER fixedprofile with editcertificateprofile ==="
/opt/keyfactor/bin/ejbca.sh ca editcertificateprofile ENDUSER -listFields 2>&1 | grep -v "^2026" | head -30
