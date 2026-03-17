#!/bin/sh
# lynis-init-107.sh — Deploy Lynis config & run first audit on vmi3129107
# Runs inside an Alpine container with / bound to /host
set -e

echo "=== Lynis init for vmi3129107 ==="

# 1. Profile
mkdir -p /host/etc/lynis
cat > /host/etc/lynis/custom.prf << 'EOF'
#
# Lynis Custom Profile — IVF Platform vmi3129107
#
skip-test=HRDN-7222
skip-test=NETW-3200
skip-test=FIRE-4512
colors=no
quick=yes
log_tests_incorrect_os=yes
EOF
echo "[OK] Profile /etc/lynis/custom.prf"

# 2. Log dirs
mkdir -p /host/var/log/lynis/reports
chmod 700 /host/var/log/lynis/reports
chmod 700 /host/var/log/lynis
echo "[OK] Log dirs"

# 3. Ship script (already copied to /tmp/lynis-ship-107.sh → write to /host)
cp /scripts/lynis-ship-107.sh /host/usr/local/bin/lynis-ship.sh
chmod 755 /host/usr/local/bin/lynis-ship.sh
echo "[OK] Ship script /usr/local/bin/lynis-ship.sh"

# 4. Cron
cat > /host/etc/cron.d/lynis-audit << 'EOF'
# Lynis weekly security audit — vmi3129107
30 2 * * 0 root lynis audit system --profile /etc/lynis/custom.prf --report-file /var/log/lynis/reports/lynis-$(date +\%Y-\%m-\%d).dat --logfile /var/log/lynis/lynis.log --no-colors --quiet && /usr/local/bin/lynis-ship.sh
EOF
chmod 644 /host/etc/cron.d/lynis-audit
echo "[OK] Cron /etc/cron.d/lynis-audit"

# 5. Audit
TODAY=$(date +%Y-%m-%d)
echo "=== Running Lynis audit (this takes 2-3 min) ==="
chroot /host lynis audit system \
  --profile /etc/lynis/custom.prf \
  --report-file /var/log/lynis/reports/lynis-${TODAY}.dat \
  --logfile /var/log/lynis/lynis.log \
  --no-colors --quiet 2>&1 | tail -3 || true

echo "=== Report file ==="
ls -lh /host/var/log/lynis/reports/
echo "LYNIS_INIT_DONE"
