#!/bin/bash
# =====================================================
# SSH 2FA — Google Authenticator TOTP Setup
# =====================================================
# Adds TOTP-based two-factor authentication to SSH.
# After setup: SSH login requires SSH key + TOTP code.
# Equivalent to Azure MFA / AWS IAM MFA.
#
# IMPORTANT: Keep your current SSH session open while
# testing 2FA in a new terminal! If TOTP fails, you can
# still fix the config from the open session.
#
# Usage:
#   ssh root@45.134.226.56 'bash -s' < scripts/setup-ssh-2fa.sh
#
# After running:
#   1. Scan the QR code with Google Authenticator / Authy
#   2. Save the emergency scratch codes
#   3. Test in a NEW terminal before closing current session
#
# To disable 2FA:
#   ssh root@VPS 'bash -s -- --disable' < scripts/setup-ssh-2fa.sh
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

MODE="${1:-}"

if [ "$(id -u)" -ne 0 ]; then
    log_error "This script must be run as root"
    exit 1
fi

# ─── Disable mode ───
if [ "$MODE" = "--disable" ]; then
    log_warn "Disabling SSH 2FA..."

    # Remove PAM line
    if grep -q "pam_google_authenticator.so" /etc/pam.d/sshd; then
        sed -i '/pam_google_authenticator.so/d' /etc/pam.d/sshd
        log_info "  ✓ Removed PAM module"
    fi

    # Revert sshd_config
    sed -i 's/^ChallengeResponseAuthentication yes/ChallengeResponseAuthentication no/' /etc/ssh/sshd_config 2>/dev/null || true
    sed -i 's/^KbdInteractiveAuthentication yes/KbdInteractiveAuthentication no/' /etc/ssh/sshd_config 2>/dev/null || true
    sed -i '/^AuthenticationMethods/d' /etc/ssh/sshd_config 2>/dev/null || true

    if sshd -t 2>/dev/null; then
        # Ubuntu uses 'ssh' service, not 'sshd'
        systemctl reload ssh 2>/dev/null || systemctl reload sshd 2>/dev/null
        log_info "  ✓ SSH reloaded — 2FA disabled"
    else
        log_error "  sshd config test failed!"
    fi
    exit 0
fi

# ─── Step 1: Install Google Authenticator PAM ───
log_info "Step 1: Installing Google Authenticator PAM..."
if dpkg -l | grep -q libpam-google-authenticator; then
    log_info "  ✓ Already installed"
else
    apt-get update -qq
    apt-get install -y -qq libpam-google-authenticator
    log_info "  ✓ Installed"
fi

# ─── Step 2: Configure PAM ───
log_info "Step 2: Configuring PAM for SSH..."

PAM_SSHD="/etc/pam.d/sshd"
PAM_LINE="auth required pam_google_authenticator.so nullok"

if grep -q "pam_google_authenticator.so" "$PAM_SSHD"; then
    log_info "  ✓ PAM module already configured"
else
    # Add at the end of the file
    echo "" >> "$PAM_SSHD"
    echo "# IVF: Google Authenticator TOTP (SSH 2FA)" >> "$PAM_SSHD"
    echo "$PAM_LINE" >> "$PAM_SSHD"
    log_info "  ✓ PAM module added"
    log_info "  Note: 'nullok' allows users without 2FA to still login"
    log_info "  Remove 'nullok' after all admins have set up TOTP"
fi

# ─── Step 3: Configure sshd ───
log_info "Step 3: Configuring sshd for 2FA..."

SSHD_CONFIG="/etc/ssh/sshd_config"
SSHD_BACKUP="${SSHD_CONFIG}.bak.2fa.$(date +%Y%m%d)"

# Backup
cp "$SSHD_CONFIG" "$SSHD_BACKUP"
log_info "  Backup: $SSHD_BACKUP"

# Enable ChallengeResponseAuthentication (Ubuntu < 22.04)
if grep -q "^#*ChallengeResponseAuthentication" "$SSHD_CONFIG"; then
    sed -i 's/^#*ChallengeResponseAuthentication.*/ChallengeResponseAuthentication yes/' "$SSHD_CONFIG"
fi

# Enable KbdInteractiveAuthentication (Ubuntu >= 22.04)
if grep -q "^#*KbdInteractiveAuthentication" "$SSHD_CONFIG"; then
    sed -i 's/^#*KbdInteractiveAuthentication.*/KbdInteractiveAuthentication yes/' "$SSHD_CONFIG"
else
    echo "KbdInteractiveAuthentication yes" >> "$SSHD_CONFIG"
fi

# Set AuthenticationMethods: require publickey AND keyboard-interactive (TOTP)
if grep -q "^AuthenticationMethods" "$SSHD_CONFIG"; then
    sed -i 's/^AuthenticationMethods.*/AuthenticationMethods publickey,keyboard-interactive/' "$SSHD_CONFIG"
else
    echo "" >> "$SSHD_CONFIG"
    echo "# IVF: Require SSH key + TOTP (2FA)" >> "$SSHD_CONFIG"
    echo "AuthenticationMethods publickey,keyboard-interactive" >> "$SSHD_CONFIG"
fi

log_info "  ✓ sshd configured: publickey + keyboard-interactive"

# Validate config
if sshd -t 2>/dev/null; then
    log_info "  ✓ sshd config syntax OK"
else
    log_error "  sshd config validation failed! Restoring backup..."
    cp "$SSHD_BACKUP" "$SSHD_CONFIG"
    exit 1
fi

# ─── Step 4: Generate TOTP for current user ───
log_info "Step 4: Generating TOTP secret..."

TOTP_USER="${SUDO_USER:-root}"
TOTP_HOME=$(eval echo "~$TOTP_USER")
TOTP_FILE="$TOTP_HOME/.google_authenticator"

if [ -f "$TOTP_FILE" ]; then
    log_warn "  TOTP already configured for '$TOTP_USER'"
    log_warn "  To regenerate: rm $TOTP_FILE && re-run this script"
else
    log_info ""
    log_info "═══════════════════════════════════════════════════"
    log_info "  Generating TOTP secret..."
    log_info "═══════════════════════════════════════════════════"
    echo ""

    # Generate TOTP secret with: time-based, disallow reuse, force write,
    # rate limit 3/30s, window 3 (allows 30s clock skew), 5 emergency codes
    # NOTE: no -q flag so secret key and emergency codes are displayed
    su -c "google-authenticator -t -d -f -r 3 -R 30 -w 3 -e 5" "$TOTP_USER" <<< $'y\ny\ny\nn\ny' 2>/dev/null || \
    google-authenticator -t -d -f -r 3 -R 30 -w 3 -e 5 <<< $'y\ny\ny\nn\ny'

    echo ""
    # Also print the secret key in text form for manual entry
    TOTP_SECRET=$(head -1 "$TOTP_FILE")
    log_info "═══════════════════════════════════════════════════"
    log_info "  TOTP Secret Key: $TOTP_SECRET"
    log_info "  (Use this to manually add to your authenticator app)"
    log_info ""
    log_info "  Emergency scratch codes (save these!):"
    tail -5 "$TOTP_FILE" | grep -E '^[0-9]+$' | while read -r code; do
        log_info "    $code"
    done
    log_info "═══════════════════════════════════════════════════"
fi

# ─── Step 5: DO NOT auto-reload (safety measure) ───
log_info "Step 5: Configuration ready (NOT yet active)"
log_info ""
log_warn "═══════════════════════════════════════════════════"
log_warn "  SSH 2FA is configured but NOT YET ACTIVE!"
log_warn ""
log_warn "  Before activating, you MUST:"
log_warn "  1. Add the TOTP secret to your authenticator app"
log_warn "  2. Verify you can generate codes"
log_warn "  3. Save the emergency scratch codes"
log_warn ""
log_warn "  To ACTIVATE 2FA (apply the config):"
log_warn "    systemctl reload ssh"
log_warn ""
log_warn "  To TEST after activating:"
log_warn "    ssh -t $TOTP_USER@$(hostname -I | awk '{print $1}')"
log_warn "    → You will be prompted for Verification code"
log_warn ""
log_warn "  To REVERT if locked out (from VPS console):"
log_warn "    cp $SSHD_BACKUP /etc/ssh/sshd_config"
log_warn "    sed -i '/pam_google_authenticator.so/d' /etc/pam.d/sshd"
log_warn "    systemctl reload ssh"
log_warn "    fail2ban-client unban --all"
log_warn ""
log_warn "  To disable 2FA:"
log_warn "    ssh root@VPS 'bash -s -- --disable' < scripts/setup-ssh-2fa.sh"
log_warn "═══════════════════════════════════════════════════"
