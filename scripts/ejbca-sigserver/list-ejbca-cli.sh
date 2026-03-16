#!/bin/bash
EJBCA_C=$(docker ps --format "{{.Names}}" | grep "ivf_ejbca\." | grep -v db | head -1)

echo "=== CA CLI commands with 'profile' ==="
/opt/keyfactor/bin/ejbca.sh ca --help 2>&1 | grep -i profile

echo ""
echo "=== RA CLI commands (may have profile commands) ==="
/opt/keyfactor/bin/ejbca.sh ra --help 2>&1

echo ""
echo "=== All available top-level commands ==="
/opt/keyfactor/bin/ejbca.sh --help 2>&1
