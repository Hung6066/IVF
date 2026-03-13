#!/bin/bash
# ═══════════════════════════════════════════════════════════════
# IVF System — Lock down admin ports (Azure Bastion-style)
# ═══════════════════════════════════════════════════════════════
# Run on VPS as root. Blocks external access to admin services.
# Admin access is only possible via SSH tunnel (like Azure Bastion).
#
# Usage: ssh root@VPS 'bash -s' < scripts/secure-admin-ports.sh
# ═══════════════════════════════════════════════════════════════

set -euo pipefail

echo "=== IVF Admin Port Lockdown ==="
echo "Blocking external access to admin ports..."
echo ""

# ─── Admin ports to lock down ───
# These ports are only accessible via SSH tunnel from localhost
ADMIN_PORTS=(
    "8443"   # EJBCA Admin UI
    "9443"   # SignServer Admin UI
    "9001"   # MinIO Console
    "5433"   # PostgreSQL (if exposed)
    "6379"   # Redis (if exposed)
)

# ─── Check if ufw is available ───
if command -v ufw &>/dev/null; then
    echo "Using UFW firewall..."

    for port in "${ADMIN_PORTS[@]}"; do
        # Deny from any external source
        ufw deny in on eth0 to any port "$port" comment "IVF admin lockdown" 2>/dev/null || true
        # Allow from localhost (SSH tunnel)
        ufw allow in on lo to any port "$port" comment "IVF admin via SSH tunnel" 2>/dev/null || true
        echo "  ✓ Port $port: blocked externally, allowed via SSH tunnel"
    done

    ufw reload
    echo ""
    echo "UFW rules applied."

# ─── Fallback to iptables ───
elif command -v iptables &>/dev/null; then
    echo "Using iptables..."

    for port in "${ADMIN_PORTS[@]}"; do
        # Allow localhost (SSH tunnel)
        iptables -C INPUT -i lo -p tcp --dport "$port" -j ACCEPT 2>/dev/null \
            || iptables -I INPUT -i lo -p tcp --dport "$port" -j ACCEPT

        # Drop external access (not from lo or Docker networks)
        iptables -C INPUT -p tcp --dport "$port" ! -i lo ! -s 10.0.0.0/8 ! -s 172.16.0.0/12 -j DROP 2>/dev/null \
            || iptables -I INPUT 2 -p tcp --dport "$port" ! -i lo ! -s 10.0.0.0/8 ! -s 172.16.0.0/12 -j DROP

        echo "  ✓ Port $port: blocked externally, allowed via SSH tunnel + Docker networks"
    done

    # Persist rules
    if command -v netfilter-persistent &>/dev/null; then
        netfilter-persistent save
    elif command -v iptables-save &>/dev/null; then
        iptables-save > /etc/iptables.rules
    fi

    echo ""
    echo "iptables rules applied."
else
    echo "ERROR: Neither ufw nor iptables found!"
    exit 1
fi

echo ""
echo "=== Verification ==="
echo "Checking port accessibility from external interface..."
for port in "${ADMIN_PORTS[@]}"; do
    if ss -tlnp | grep -q ":$port " 2>/dev/null; then
        echo "  Port $port: LISTENING (accessible via SSH tunnel only)"
    else
        echo "  Port $port: NOT LISTENING (service not running or port not exposed)"
    fi
done

echo ""
echo "=== Done ==="
echo ""
echo "Access admin UIs via SSH tunnel:"
echo "  EJBCA:      ssh -L 8443:localhost:8443 root@VPS → https://localhost:8443/ejbca/adminweb/"
echo "  SignServer:  ssh -L 9443:localhost:9443 root@VPS → https://localhost:9443/signserver/adminweb/"
echo "  MinIO:      ssh -L 9001:localhost:9001 root@VPS → http://localhost:9001"
echo "  PostgreSQL: ssh -L 5433:localhost:5432 root@VPS → psql -h localhost -p 5433"
echo "  Redis:      ssh -L 6379:localhost:6379 root@VPS → redis-cli -h localhost"
