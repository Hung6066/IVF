#!/bin/bash
# =====================================================
# Cloudflare Access — Zero Trust Admin Access
# =====================================================
# Installs cloudflared tunnel daemon and configures
# Cloudflare Access for admin service proxying.
# Equivalent to Azure Entra Application Proxy.
#
# Architecture:
#   Browser → Cloudflare Access (identity check)
#         → Cloudflare Tunnel (encrypted)
#         → cloudflared (on VPS)
#         → localhost:port (admin service)
#
# Benefits:
#   - No SSH tunnel needed
#   - Identity-based access (email OTP, Google SSO, GitHub SSO)
#   - Audit logs in Cloudflare dashboard
#   - DDoS protection
#
# Prerequisites:
#   1. Cloudflare account with domain (natra.site)
#   2. Cloudflare tunnel token (from Zero Trust dashboard)
#
# Usage:
#   # Step 1: Create tunnel in Cloudflare dashboard
#   #   → Zero Trust → Networks → Tunnels → Create
#   #   → Name: "ivf-admin"
#   #   → Copy the tunnel token
#
#   # Step 2: Install cloudflared on VPS
#   ssh root@45.134.226.56 'bash -s -- <TUNNEL_TOKEN>' < scripts/setup-cloudflare-access.sh
#
#   # Step 3: Configure Access policies in Cloudflare dashboard
#   #   → Zero Trust → Access → Applications → Add
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

TUNNEL_TOKEN="${1:-}"
MODE="${2:-}"

if [ "$(id -u)" -ne 0 ]; then
    log_error "This script must be run as root"
    exit 1
fi

# ─── Uninstall mode ───
if [ "$TUNNEL_TOKEN" = "--uninstall" ]; then
    log_warn "Uninstalling cloudflared..."
    systemctl stop cloudflared 2>/dev/null || true
    systemctl disable cloudflared 2>/dev/null || true
    apt-get remove -y cloudflared 2>/dev/null || true
    rm -f /etc/cloudflared/config.yml
    log_info "  ✓ cloudflared removed"
    exit 0
fi

if [ -z "$TUNNEL_TOKEN" ]; then
    echo ""
    log_info "═══════════════════════════════════════════════════"
    log_info "  Cloudflare Access Setup Guide"
    log_info "═══════════════════════════════════════════════════"
    echo ""
    log_info "  Step 1: Create Cloudflare Tunnel"
    log_info "    → https://one.dash.cloudflare.com/"
    log_info "    → Zero Trust → Networks → Tunnels"
    log_info "    → Create a tunnel → Name: 'ivf-admin'"
    log_info "    → Copy the tunnel token"
    echo ""
    log_info "  Step 2: Configure public hostnames (in tunnel config):"
    echo ""
    echo "    ┌───────────────────────┬──────────────────────────────┐"
    echo "    │ Public hostname       │ Service                      │"
    echo "    ├───────────────────────┼──────────────────────────────┤"
    echo "    │ ejbca.natra.site      │ https://localhost:8443       │"
    echo "    │ signserver.natra.site │ https://localhost:9443       │"
    echo "    │ minio.natra.site      │ http://localhost:9001        │"
    echo "    │ grafana.natra.site    │ http://localhost:3000        │"
    echo "    └───────────────────────┴──────────────────────────────┘"
    echo ""
    log_info "  Step 3: Create Access Applications"
    log_info "    → Zero Trust → Access → Applications → Add"
    log_info "    → Self-hosted → Domain: ejbca.natra.site"
    log_info "    → Policy: Allow emails: your-email@gmail.com"
    log_info "    → Session duration: 24 hours"
    echo ""
    log_info "  Step 4: Run this script with the token:"
    log_info "    ssh root@VPS 'bash -s -- <TUNNEL_TOKEN>' < $0"
    log_info "═══════════════════════════════════════════════════"
    exit 0
fi

# ─── Step 1: Install cloudflared ───
log_info "Step 1: Installing cloudflared..."

if command -v cloudflared &>/dev/null; then
    log_info "  ✓ Already installed: $(cloudflared --version)"
else
    # Add Cloudflare GPG key and repo
    curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg | \
        tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null

    echo "deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared $(lsb_release -cs) main" | \
        tee /etc/apt/sources.list.d/cloudflared.list

    apt-get update -qq
    apt-get install -y -qq cloudflared
    log_info "  ✓ Installed: $(cloudflared --version)"
fi

# ─── Step 2: Install as service ───
log_info "Step 2: Installing cloudflared as system service..."

if systemctl is-active --quiet cloudflared; then
    log_info "  ✓ Service already running"
else
    cloudflared service install "$TUNNEL_TOKEN"
    log_info "  ✓ Service installed and started"
fi

# ─── Step 3: Verify ───
log_info "Step 3: Verification..."

sleep 3

if systemctl is-active --quiet cloudflared; then
    log_info "  ✓ cloudflared is running"
else
    log_error "  ✗ cloudflared failed to start"
    journalctl -u cloudflared --no-pager -n 20
    exit 1
fi

echo ""
log_info "═══════════════════════════════════════════════════"
log_info "  Cloudflare Tunnel is running!"
log_info ""
log_info "  Configure Access policies in Cloudflare dashboard:"
log_info "    https://one.dash.cloudflare.com/"
log_info "    → Zero Trust → Access → Applications"
log_info ""
log_info "  Admin access (after policy setup):"
log_info "    EJBCA:      https://ejbca.natra.site"
log_info "    SignServer: https://signserver.natra.site"
log_info "    MinIO:      https://minio.natra.site"
log_info "    Grafana:    https://grafana.natra.site"
log_info ""
log_info "  To uninstall:"
log_info "    bash $0 --uninstall"
log_info "═══════════════════════════════════════════════════"
