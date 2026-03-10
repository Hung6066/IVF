#!/bin/bash
# Setup auto-heal cron on VPS1

echo "=== Setup auto-heal cron on VPS1 ==="

# Get existing crontab (if any) and remove auto-heal entries
(crontab -l 2>/dev/null | grep -v "auto-heal" || true) > /tmp/crontab.txt

# Add new auto-heal cron (runs every 2 minutes)
echo "*/2 * * * * source /opt/ivf/.autoheal-env && /opt/ivf/scripts/auto-heal.sh >> /var/log/ivf-autoheal.log 2>&1" >> /tmp/crontab.txt

# Install crontab
crontab /tmp/crontab.txt

# Verify
echo "✅ Auto-heal cron installed on VPS1"
echo "Current crontab entry:"
crontab -l | tail -1
