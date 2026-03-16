#!/bin/bash
echo "=== Check ejbca.sh ra subcommands ==="
/opt/keyfactor/bin/ejbca.sh ra 2>&1 | grep -v "^2026" | head -60

echo ""
echo "=== Check exported certprofile XML (first 30 lines to understand format) ==="
head -30 /tmp/exported/certprofile_IVF-PDFSigner-Profile-5001.xml

echo ""
echo "=== Check any 'addendentityprofile' or similar command ==="
/opt/keyfactor/bin/ejbca.sh ra addeeprofile --help 2>&1 | grep -v "^2026" | head -20
