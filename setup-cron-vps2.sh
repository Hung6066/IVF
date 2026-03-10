#!/bin/bash
# Setup watchdog cron on VPS2

echo "=== Setup watchdog cron on VPS2 ==="

# Get existing crontab (if any) and remove watchdog entries
(crontab -l 2>/dev/null | grep -v "watchdog" || true) > /tmp/crontab.txt

# Add new watchdog cron
echo "*/2 * * * * source /opt/ivf/.watchdog-env && /opt/ivf/scripts/watchdog-vps1.sh >> /var/log/ivf-watchdog.log 2>&1" >> /tmp/crontab.txt

# Install crontab
crontab /tmp/crontab.txt

# Verify
echo "✅ Watchdog cron installed on VPS2"
echo "Current crontab entry:"
crontab -l | tail -1
