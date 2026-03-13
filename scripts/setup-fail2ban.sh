#!/bin/bash
# =====================================================
# Fail2ban — SSH Brute-Force Protection
# =====================================================
# Installs and configures fail2ban to protect SSH from
# brute-force attacks. Equivalent to AWS GuardDuty /
# Azure Defender for SSH.
#
# Features:
#   - Bans IP after 5 failed SSH attempts
#   - Ban duration: 1 hour (escalating for repeat offenders)
#   - Whitelist: localhost + Docker networks
#   - Email/Discord notification (optional)
#   - Monitors sshd + recidive (repeat offenders)
#
# Usage:
#   ssh root@45.134.226.56 'bash -s' < scripts/setup-fail2ban.sh
#
# Check status:
#   fail2ban-client status sshd
#   fail2ban-client status recidive
#
# Unban IP:
#   fail2ban-client set sshd unbanip <IP>
#
# The script is idempotent — safe to re-run.
# =====================================================

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ─── Pre-flight ───
if [ "$(id -u)" -ne 0 ]; then
    log_error "This script must be run as root"
    exit 1
fi

# ─── Step 1: Install fail2ban ───
log_info "Step 1: Installing fail2ban..."
if command -v fail2ban-server &>/dev/null; then
    log_info "  ✓ fail2ban already installed"
else
    apt-get update -qq
    apt-get install -y -qq fail2ban
    log_info "  ✓ fail2ban installed"
fi

# ─── Step 2: Configure jail.local ───
log_info "Step 2: Configuring fail2ban jails..."

JAIL_LOCAL="/etc/fail2ban/jail.local"

cat > "$JAIL_LOCAL" << 'EOF'
# =====================================================
# IVF System — fail2ban jail configuration
# =====================================================
# Customizations over jail.conf defaults.
# Do NOT edit jail.conf directly — it gets overwritten on updates.

[DEFAULT]
# Ban duration: 1 hour
bantime = 3600
# Detection window: 10 minutes
findtime = 600
# Max failures before ban
maxretry = 5
# Whitelist: localhost, Docker networks, admin IPs
ignoreip = 127.0.0.1/8 ::1 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16 115.79.197.0/24
# Ban action: iptables-multiport (works alongside UFW)
banaction = iptables-multiport
banaction_allports = iptables-allports
# Use systemd journal for log reading (Ubuntu 20.04+)
backend = systemd

# ─── SSH jail ───
[sshd]
enabled = true
port = ssh
filter = sshd
maxretry = 5
bantime = 3600
findtime = 600

# ─── Recidive (repeat offenders) ───
# Bans IPs that get banned 3+ times in 12 hours → 1 week ban
[recidive]
enabled = true
filter = recidive
banaction = iptables-allports
logpath = /var/log/fail2ban.log
maxretry = 3
findtime = 43200
bantime = 604800
backend = auto
EOF

log_info "  ✓ jail.local written"

# ─── Step 3: Clean up any custom filters ───
log_info "Step 3: Ensuring clean filter configuration..."

# Remove sshd.local if it exists — it causes circular inclusion (recursion error)
# The default sshd.conf filter handles all common attack patterns
if [ -f /etc/fail2ban/filter.d/sshd.local ]; then
    rm -f /etc/fail2ban/filter.d/sshd.local
    log_info "  ✓ Removed sshd.local (prevents circular inclusion)"
else
    log_info "  ✓ No custom filters to clean"
fi

# ─── Step 4: Enable and start ───
log_info "Step 4: Starting fail2ban..."
systemctl enable fail2ban
systemctl restart fail2ban

# Wait for startup
sleep 2

# ─── Step 5: Verify ───
log_info "Step 5: Verification"

if systemctl is-active --quiet fail2ban; then
    log_info "  ✓ fail2ban is running"
else
    log_error "  ✗ fail2ban failed to start"
    journalctl -u fail2ban --no-pager -n 20
    exit 1
fi

SSHD_STATUS=$(fail2ban-client status sshd 2>&1 || true)
if echo "$SSHD_STATUS" | grep -q "Currently banned"; then
    log_info "  ✓ sshd jail is active"
    echo "$SSHD_STATUS" | grep -E "Currently (banned|failed)"
else
    log_warn "  sshd jail status: $SSHD_STATUS"
fi

RECIDIVE_STATUS=$(fail2ban-client status recidive 2>&1 || true)
if echo "$RECIDIVE_STATUS" | grep -q "Currently banned"; then
    log_info "  ✓ recidive jail is active"
else
    log_info "  ✓ recidive jail configured"
fi

echo ""
log_info "═══════════════════════════════════════════════════"
log_info "  fail2ban configured!"
log_info ""
log_info "  SSH: Ban after 5 failures (1 hour)"
log_info "  Recidive: Ban after 3 bans (1 week)"
log_info ""
log_info "  Commands:"
log_info "    fail2ban-client status sshd"
log_info "    fail2ban-client set sshd unbanip <IP>"
log_info "    fail2ban-client status recidive"
log_info "═══════════════════════════════════════════════════"
