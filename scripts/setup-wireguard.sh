#!/bin/bash
# =====================================================
# WireGuard VPN — Multi-Admin Secure Access
# =====================================================
# Sets up a WireGuard VPN server on the VPS for secure
# admin access. Equivalent to Azure VPN Gateway / AWS
# Client VPN. Use instead of SSH tunnels when:
#   - Multiple admins need concurrent access
#   - Need persistent auto-reconnecting connections
#   - Want full network-level isolation
#
# VPN Subnet: 10.200.0.0/24
#   Server:   10.200.0.1
#   Client 1: 10.200.0.2
#   Client 2: 10.200.0.3
#   ...up to 253 clients
#
# Usage:
#   # Install server (on VPS)
#   ssh root@45.134.226.56 'bash -s' < scripts/setup-wireguard.sh
#
#   # Add a client
#   ssh root@45.134.226.56 'bash -s -- --add-client admin1' < scripts/setup-wireguard.sh
#
#   # Remove a client
#   ssh root@45.134.226.56 'bash -s -- --remove-client admin1' < scripts/setup-wireguard.sh
#
#   # Show status
#   ssh root@45.134.226.56 'bash -s -- --status' < scripts/setup-wireguard.sh
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

# ─── Configuration ───
WG_DIR="/etc/wireguard"
WG_INTERFACE="wg0"
VPN_SUBNET="10.200.0"
VPN_PORT=51820
SERVER_IP="${VPN_SUBNET}.1"
CLIENT_DIR="${WG_DIR}/clients"

# Admin service ports (accessed via VPN)
ADMIN_PORTS="8443,9443,9001,5433,6379,3000"

MODE="${1:-}"
CLIENT_NAME="${2:-}"

if [ "$(id -u)" -ne 0 ]; then
    log_error "This script must be run as root"
    exit 1
fi

# ─── Helper: Get next available client IP ───
get_next_ip() {
    local last_octet=1  # .1 is server
    if [ -d "$CLIENT_DIR" ]; then
        for conf in "$CLIENT_DIR"/*.conf; do
            [ -f "$conf" ] || continue
            local ip_line
            ip_line=$(grep "^Address" "$conf" | head -1) || true
            if [ -n "$ip_line" ]; then
                local octet
                octet=$(echo "$ip_line" | grep -oP "${VPN_SUBNET}\.\K[0-9]+")
                if [ -n "$octet" ] && [ "$octet" -gt "$last_octet" ]; then
                    last_octet="$octet"
                fi
            fi
        done
    fi
    echo $((last_octet + 1))
}

# ─── Status mode ───
if [ "$MODE" = "--status" ]; then
    if ! systemctl is-active --quiet wg-quick@${WG_INTERFACE}; then
        log_error "WireGuard is not running"
        exit 1
    fi
    wg show "$WG_INTERFACE"
    echo ""
    log_info "Configured clients:"
    if [ -d "$CLIENT_DIR" ]; then
        for conf in "$CLIENT_DIR"/*.conf; do
            [ -f "$conf" ] || continue
            name=$(basename "$conf" .conf)
            ip=$(grep "^Address" "$conf" | awk -F= '{print $2}' | tr -d ' ')
            echo "  - $name: $ip"
        done
    fi
    exit 0
fi

# ─── Add client mode ───
if [ "$MODE" = "--add-client" ]; then
    if [ -z "$CLIENT_NAME" ]; then
        log_error "Usage: $0 --add-client <name>"
        exit 1
    fi

    if [ -f "${CLIENT_DIR}/${CLIENT_NAME}.conf" ]; then
        log_warn "Client '$CLIENT_NAME' already exists:"
        cat "${CLIENT_DIR}/${CLIENT_NAME}.conf"
        exit 0
    fi

    SERVER_PUBKEY=$(cat "${WG_DIR}/server.pub")
    SERVER_ENDPOINT=$(curl -4 -sf ifconfig.me 2>/dev/null || hostname -I | awk '{print $1}')
    NEXT_IP=$(get_next_ip)
    CLIENT_IP="${VPN_SUBNET}.${NEXT_IP}"

    log_info "Adding client: $CLIENT_NAME ($CLIENT_IP)"

    # Generate client keys
    mkdir -p "$CLIENT_DIR"
    wg genkey | tee "${CLIENT_DIR}/${CLIENT_NAME}.key" | wg pubkey > "${CLIENT_DIR}/${CLIENT_NAME}.pub"
    CLIENT_PRIVKEY=$(cat "${CLIENT_DIR}/${CLIENT_NAME}.key")
    CLIENT_PUBKEY=$(cat "${CLIENT_DIR}/${CLIENT_NAME}.pub")
    chmod 600 "${CLIENT_DIR}/${CLIENT_NAME}.key"

    # Generate preshared key
    wg genpsk > "${CLIENT_DIR}/${CLIENT_NAME}.psk"
    CLIENT_PSK=$(cat "${CLIENT_DIR}/${CLIENT_NAME}.psk")
    chmod 600 "${CLIENT_DIR}/${CLIENT_NAME}.psk"

    # Create client config file
    cat > "${CLIENT_DIR}/${CLIENT_NAME}.conf" << EOF
[Interface]
# Client: ${CLIENT_NAME}
PrivateKey = ${CLIENT_PRIVKEY}
Address = ${CLIENT_IP}/32
DNS = 1.1.1.1

[Peer]
# IVF VPS Server
PublicKey = ${SERVER_PUBKEY}
PresharedKey = ${CLIENT_PSK}
# Only route VPN subnet + admin ports through VPN (split tunnel)
AllowedIPs = ${VPN_SUBNET}.0/24
Endpoint = ${SERVER_ENDPOINT}:${VPN_PORT}
PersistentKeepalive = 25
EOF

    # Add peer to server config
    cat >> "${WG_DIR}/${WG_INTERFACE}.conf" << EOF

# Client: ${CLIENT_NAME}
[Peer]
PublicKey = ${CLIENT_PUBKEY}
PresharedKey = ${CLIENT_PSK}
AllowedIPs = ${CLIENT_IP}/32
EOF

    # Hot-reload (add peer without restart)
    wg set "$WG_INTERFACE" peer "$CLIENT_PUBKEY" \
        preshared-key "${CLIENT_DIR}/${CLIENT_NAME}.psk" \
        allowed-ips "${CLIENT_IP}/32"

    echo ""
    log_info "═══════════════════════════════════════════════════"
    log_info "  Client '$CLIENT_NAME' added!"
    log_info ""
    log_info "  Client config (copy to client machine):"
    log_info "═══════════════════════════════════════════════════"
    echo ""
    cat "${CLIENT_DIR}/${CLIENT_NAME}.conf"
    echo ""
    log_info "═══════════════════════════════════════════════════"
    log_info "  On Windows: Import into WireGuard app"
    log_info "  On macOS:   Import into WireGuard app"
    log_info "  On Linux:   cp to /etc/wireguard/wg0.conf"
    log_info "              wg-quick up wg0"
    log_info ""
    log_info "  After connecting, access admin services at:"
    log_info "    EJBCA:      https://${SERVER_IP}:8443"
    log_info "    SignServer: https://${SERVER_IP}:9443"
    log_info "    MinIO:      http://${SERVER_IP}:9001"
    log_info "    PostgreSQL: ${SERVER_IP}:5433"
    log_info "    Redis:      ${SERVER_IP}:6379"
    log_info "    Grafana:    http://${SERVER_IP}:3000"
    log_info "═══════════════════════════════════════════════════"
    exit 0
fi

# ─── Remove client mode ───
if [ "$MODE" = "--remove-client" ]; then
    if [ -z "$CLIENT_NAME" ]; then
        log_error "Usage: $0 --remove-client <name>"
        exit 1
    fi

    if [ ! -f "${CLIENT_DIR}/${CLIENT_NAME}.pub" ]; then
        log_error "Client '$CLIENT_NAME' not found"
        exit 1
    fi

    CLIENT_PUBKEY=$(cat "${CLIENT_DIR}/${CLIENT_NAME}.pub")

    # Remove from live interface
    wg set "$WG_INTERFACE" peer "$CLIENT_PUBKEY" remove

    # Remove from config file (remove [Peer] block with matching key)
    # Create temp file without this peer
    python3 -c "
import re, sys
conf = open('${WG_DIR}/${WG_INTERFACE}.conf').read()
# Remove the peer block for this client
pattern = r'\n# Client: ${CLIENT_NAME}\n\[Peer\][^\[]*'
conf = re.sub(pattern, '', conf)
open('${WG_DIR}/${WG_INTERFACE}.conf', 'w').write(conf)
" 2>/dev/null || {
        # Fallback: sed-based removal
        log_warn "  python3 not available, manual config cleanup needed"
    }

    # Remove client files
    rm -f "${CLIENT_DIR}/${CLIENT_NAME}".{key,pub,psk,conf}

    log_info "  ✓ Client '$CLIENT_NAME' removed"
    exit 0
fi

# ─── Install mode (default) ───
log_info "═══ WireGuard VPN Server Setup ═══"

# ─── Step 1: Install WireGuard ───
log_info "Step 1: Installing WireGuard..."
if command -v wg &>/dev/null; then
    log_info "  ✓ Already installed"
else
    apt-get update -qq
    apt-get install -y -qq wireguard
    log_info "  ✓ Installed"
fi

# ─── Step 2: Generate server keys ───
log_info "Step 2: Generating server keys..."

mkdir -p "$WG_DIR" "$CLIENT_DIR"
chmod 700 "$WG_DIR"

if [ -f "${WG_DIR}/server.key" ]; then
    log_info "  ✓ Server keys already exist"
else
    wg genkey | tee "${WG_DIR}/server.key" | wg pubkey > "${WG_DIR}/server.pub"
    chmod 600 "${WG_DIR}/server.key"
    log_info "  ✓ Server keys generated"
fi

SERVER_PRIVKEY=$(cat "${WG_DIR}/server.key")
SERVER_PUBKEY=$(cat "${WG_DIR}/server.pub")

# ─── Step 3: Create server config ───
log_info "Step 3: Creating server config..."

if [ -f "${WG_DIR}/${WG_INTERFACE}.conf" ]; then
    log_warn "  Config already exists — preserving (may have clients)"
else
    # Detect default network interface
    DEFAULT_IF=$(ip route | grep default | awk '{print $5}' | head -1)

    cat > "${WG_DIR}/${WG_INTERFACE}.conf" << EOF
[Interface]
# IVF VPN Server
PrivateKey = ${SERVER_PRIVKEY}
Address = ${SERVER_IP}/24
ListenPort = ${VPN_PORT}

# NAT + forwarding for VPN clients
PostUp = iptables -A FORWARD -i %i -j ACCEPT; iptables -A FORWARD -o %i -j ACCEPT; iptables -t nat -A POSTROUTING -o ${DEFAULT_IF} -j MASQUERADE
PostDown = iptables -D FORWARD -i %i -j ACCEPT; iptables -D FORWARD -o %i -j ACCEPT; iptables -t nat -D POSTROUTING -o ${DEFAULT_IF} -j MASQUERADE
EOF

    chmod 600 "${WG_DIR}/${WG_INTERFACE}.conf"
    log_info "  ✓ Server config created"
fi

# ─── Step 4: Enable IP forwarding ───
log_info "Step 4: Enabling IP forwarding..."
if sysctl net.ipv4.ip_forward | grep -q "= 1"; then
    log_info "  ✓ Already enabled"
else
    sysctl -w net.ipv4.ip_forward=1
    if ! grep -q "^net.ipv4.ip_forward=1" /etc/sysctl.conf; then
        echo "net.ipv4.ip_forward=1" >> /etc/sysctl.conf
    fi
    log_info "  ✓ IP forwarding enabled"
fi

# ─── Step 5: Firewall rules ───
log_info "Step 5: Configuring firewall..."

# Allow WireGuard port
ufw allow "${VPN_PORT}/udp" comment "WireGuard VPN" 2>/dev/null || true

# Allow admin ports from VPN subnet
for port in $(echo "$ADMIN_PORTS" | tr ',' ' '); do
    ufw allow in on "$WG_INTERFACE" to any port "$port" comment "IVF admin via VPN" 2>/dev/null || true
done

ufw reload 2>/dev/null || true
log_info "  ✓ Firewall configured"

# ─── Step 6: Start WireGuard ───
log_info "Step 6: Starting WireGuard..."
systemctl enable wg-quick@${WG_INTERFACE}

if systemctl is-active --quiet wg-quick@${WG_INTERFACE}; then
    log_info "  ✓ Already running"
    # Reload config
    wg syncconf "$WG_INTERFACE" <(wg-quick strip "$WG_INTERFACE")
else
    systemctl start wg-quick@${WG_INTERFACE}
    log_info "  ✓ Started"
fi

# ─── Step 7: Verify ───
log_info "Step 7: Verification..."

if ip link show "$WG_INTERFACE" &>/dev/null; then
    log_info "  ✓ Interface $WG_INTERFACE is up"
    wg show "$WG_INTERFACE"
else
    log_error "  ✗ Interface $WG_INTERFACE not found"
    exit 1
fi

echo ""
log_info "═══════════════════════════════════════════════════"
log_info "  WireGuard VPN server is ready!"
log_info ""
log_info "  Server: ${SERVER_IP}/24 (port ${VPN_PORT})"
log_info "  Public key: ${SERVER_PUBKEY}"
log_info ""
log_info "  Add a client:"
log_info "    bash $0 --add-client admin1"
log_info ""
log_info "  Show status:"
log_info "    bash $0 --status"
log_info ""
log_info "  After VPN connect, admin services at:"
log_info "    https://${SERVER_IP}:8443  (EJBCA)"
log_info "    https://${SERVER_IP}:9443  (SignServer)"
log_info "    http://${SERVER_IP}:9001   (MinIO)"
log_info "    ${SERVER_IP}:5433          (PostgreSQL)"
log_info "    ${SERVER_IP}:6379          (Redis)"
log_info "    http://${SERVER_IP}:3000   (Grafana)"
log_info "═══════════════════════════════════════════════════"
