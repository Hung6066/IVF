#!/bin/bash
echo "=== Find sample profile XML files ==="
find / -name "certprofile_*.xml" 2>/dev/null | head -20
echo ""

find / -name "*profile*.xml" 2>/dev/null | head -20
echo ""

echo "=== Look in common EJBCA dirs for templates ==="
ls /opt/keyfactor/ 2>/dev/null
ls /opt/keyfactor/conf/ 2>/dev/null
ls /opt/keyfactor/dist/ 2>/dev/null

echo ""
echo "=== Check for any XML profile examples ==="
find /opt/keyfactor -name "*.xml" 2>/dev/null | head -30
