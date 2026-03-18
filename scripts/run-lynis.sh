#!/bin/bash
# Run Lynis audit and ship JSON report to MinIO
set -euo pipefail

TODAY=$(date +%Y-%m-%d)
RFILE="/var/log/lynis/reports/lynis-${TODAY}.dat"

echo "=== Running Lynis audit for $TODAY ==="
mkdir -p /var/log/lynis/reports

# Fix profile: remove invalid 'report=default' option if present
if [ -f /etc/lynis/custom.prf ]; then
    sed -i '/^report=/d' /etc/lynis/custom.prf
fi

lynis audit system \
    --profile /etc/lynis/custom.prf \
    --report-file "$RFILE" \
    --logfile /var/log/lynis/lynis.log \
    --no-colors \
    --quiet 2>/dev/null || true

echo "Lynis RC: $?"

if [ -f "$RFILE" ] && [ $(stat -c%s "$RFILE") -gt 1000 ]; then
    echo "Report file: $(ls -lh $RFILE)"
    echo "=== Running lynis-ship.sh ==="
    bash /usr/local/bin/lynis-ship.sh
    echo "SHIP_DONE"
else
    echo "ERROR: Report file not created or too small: $RFILE"
    ls -la /var/log/lynis/reports/ 2>/dev/null || true
    exit 1
fi
