#!/bin/bash
# =====================================================
# Create Dedicated Admin User & Disable Root SSH Login
# =====================================================
# Creates a non-root admin user with:
#   - sudo access (passwordless for Docker commands)
#   - SSH key authentication (copies root's authorized_keys)
#   - Docker group membership (direct docker commands)
#
# IMPORTANT: Run this WHILE still logged in as root via SSH.
# After running, test SSH with the new user BEFORE closing
# your current root session!
#
# Usage:
#   ssh root@45.134.226.56 'bash -s' < scripts/setup-admin-user.sh
#
# Or interactively:
#   ssh root@45.134.226.56
#   bash /root/setup-admin-user.sh
#
# After setup, test connection:
#   ssh ivfadmin@45.134.226.56 "whoami && sudo docker ps --format 'table {{.Names}}\t{{.Status}}' | head 5"
#
# Then update scripts/admin-tunnel.ps1 and SSH config.
# =====================================================

set -euo pipefail

ADMIN_USER="${1:-ivfadmin}"
SSH_PORT="${2:-22}"

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

log_info "Creating admin user: ${ADMIN_USER}"

# ─── Step 1: Create user ───
if id "$ADMIN_USER" &>/dev/null; then
    log_info "  User '${ADMIN_USER}' already exists"
else
    log_info "  Creating user '${ADMIN_USER}'..."
    useradd -m -s /bin/bash -G sudo "$ADMIN_USER"
    log_info "  ✓ User created"
fi

# ─── Step 2: Add to Docker group ───
if groups "$ADMIN_USER" | grep -q docker; then
    log_info "  ✓ Already in docker group"
else
    log_info "  Adding to docker group..."
    usermod -aG docker "$ADMIN_USER"
    log_info "  ✓ Added to docker group"
fi

# ─── Step 3: Configure sudo ───
SUDOERS_FILE="/etc/sudoers.d/${ADMIN_USER}"
if [ -f "$SUDOERS_FILE" ]; then
    log_info "  ✓ Sudoers file already exists"
else
    log_info "  Creating sudoers file..."
    cat > "$SUDOERS_FILE" << EOF
# IVF Admin user — passwordless sudo for Docker operations
${ADMIN_USER} ALL=(ALL) NOPASSWD: /usr/bin/docker, /usr/bin/docker-compose, /usr/local/bin/docker-compose
${ADMIN_USER} ALL=(ALL) NOPASSWD: /usr/sbin/ufw, /usr/sbin/iptables
${ADMIN_USER} ALL=(ALL) NOPASSWD: /bin/systemctl restart sshd, /bin/systemctl reload sshd
# For general admin (requires password)
${ADMIN_USER} ALL=(ALL:ALL) ALL
EOF
    chmod 440 "$SUDOERS_FILE"
    # Validate sudoers syntax
    if visudo -cf "$SUDOERS_FILE" > /dev/null 2>&1; then
        log_info "  ✓ Sudoers file created and validated"
    else
        log_error "  Sudoers syntax error! Removing..."
        rm -f "$SUDOERS_FILE"
        exit 1
    fi
fi

# ─── Step 4: Copy SSH authorized_keys from root ───
ADMIN_SSH_DIR="/home/${ADMIN_USER}/.ssh"
if [ -f "${ADMIN_SSH_DIR}/authorized_keys" ] && [ -s "${ADMIN_SSH_DIR}/authorized_keys" ]; then
    log_info "  ✓ SSH keys already configured"
else
    log_info "  Copying SSH keys from root..."
    mkdir -p "$ADMIN_SSH_DIR"
    if [ -f /root/.ssh/authorized_keys ]; then
        cp /root/.ssh/authorized_keys "${ADMIN_SSH_DIR}/authorized_keys"
        chown -R "${ADMIN_USER}:${ADMIN_USER}" "$ADMIN_SSH_DIR"
        chmod 700 "$ADMIN_SSH_DIR"
        chmod 600 "${ADMIN_SSH_DIR}/authorized_keys"
        log_info "  ✓ SSH keys copied"
    else
        log_error "  No root SSH keys found at /root/.ssh/authorized_keys"
        log_warn "  You must manually add SSH keys for ${ADMIN_USER}"
    fi
fi

# ─── Step 5: Set a strong random password (for sudo escalation) ───
PASS_FILE="/root/.${ADMIN_USER}_password"
if [ -f "$PASS_FILE" ]; then
    log_info "  ✓ Password already set (saved in ${PASS_FILE})"
else
    RANDOM_PASS=$(openssl rand -base64 24)
    echo "${ADMIN_USER}:${RANDOM_PASS}" | chpasswd
    echo "$RANDOM_PASS" > "$PASS_FILE"
    chmod 600 "$PASS_FILE"
    log_info "  ✓ Random password set (saved in ${PASS_FILE})"
fi

# ─── Step 6: Create IVF work directory ───
IVF_DIR="/home/${ADMIN_USER}/ivf"
if [ -d "$IVF_DIR" ]; then
    log_info "  ✓ IVF directory exists"
else
    log_info "  Creating symlink to /root/ivf..."
    if [ -d /root/ivf ]; then
        ln -s /root/ivf "$IVF_DIR"
        chown -h "${ADMIN_USER}:${ADMIN_USER}" "$IVF_DIR"
        log_info "  ✓ Symlink created: ${IVF_DIR} → /root/ivf"
    else
        mkdir -p "$IVF_DIR"
        chown "${ADMIN_USER}:${ADMIN_USER}" "$IVF_DIR"
        log_info "  ✓ Directory created: ${IVF_DIR}"
    fi
fi

echo ""
log_info "═══════════════════════════════════════════════════"
log_info "  Admin user '${ADMIN_USER}' is ready!"
log_info ""
log_info "  NEXT STEPS (do NOT close this session yet!):"
log_info ""
log_info "  1. TEST SSH login in a NEW terminal:"
log_info "     ssh ${ADMIN_USER}@45.134.226.56"
log_info ""
log_info "  2. TEST sudo access:"
log_info "     sudo docker ps"
log_info ""
log_info "  3. If SSH works, DISABLE root login:"
log_info "     bash scripts/disable-root-ssh.sh"
log_info "     (Or run manually below)"
log_info ""
log_info "  4. Update local scripts:"
log_info "     admin-tunnel.ps1 → change root → ${ADMIN_USER}"
log_info "═══════════════════════════════════════════════════"

# ─── Optional: Disable root SSH (only if --disable-root flag) ───
if [ "${1:-}" = "--disable-root" ] || [ "${2:-}" = "--disable-root" ]; then
    log_warn "═══════════════════════════════════════════════════"
    log_warn "  DISABLING ROOT SSH LOGIN"
    log_warn "  Make sure you tested ${ADMIN_USER} login first!"
    log_warn "═══════════════════════════════════════════════════"

    # Backup sshd_config
    cp /etc/ssh/sshd_config /etc/ssh/sshd_config.bak.$(date +%Y%m%d)

    # Disable root login
    sed -i 's/^#*PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config

    # Ensure key auth is enabled
    sed -i 's/^#*PubkeyAuthentication.*/PubkeyAuthentication yes/' /etc/ssh/sshd_config

    # Validate config
    if sshd -t 2>/dev/null; then
        systemctl reload sshd
        log_info "  ✓ Root SSH login disabled, sshd reloaded"
        log_info "  ✓ Backup: /etc/ssh/sshd_config.bak.$(date +%Y%m%d)"
    else
        log_error "  sshd config validation failed! Restoring backup..."
        cp /etc/ssh/sshd_config.bak.$(date +%Y%m%d) /etc/ssh/sshd_config
        exit 1
    fi
fi
