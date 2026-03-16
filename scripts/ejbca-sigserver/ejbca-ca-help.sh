#!/bin/bash
echo "=== ejbca.sh ca (list ca subcommands) ==="
/opt/keyfactor/bin/ejbca.sh ca 2>&1 | grep -v "^2026" | head -80

echo ""
echo "=== Test editcertificateprofile ENDUSER ==="
/opt/keyfactor/bin/ejbca.sh ca editcertificateprofile ENDUSER --help 2>&1 | grep -v "^2026" | head -20

echo ""
echo "=== importprofiles help ==="
/opt/keyfactor/bin/ejbca.sh ca importprofiles --help 2>&1 | grep -v "^2026" | head -20

echo ""
echo "=== exportprofiles help ==="
/opt/keyfactor/bin/ejbca.sh ca exportprofiles --help 2>&1 | grep -v "^2026" | head -20
