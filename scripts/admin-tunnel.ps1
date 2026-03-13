# ═══════════════════════════════════════════════════════════════
# IVF Admin Tunnel — Secure access to all admin services
# ═══════════════════════════════════════════════════════════════
# Windows PowerShell script to open SSH tunnels (Azure Bastion-style)
#
# Usage:
#   .\scripts\admin-tunnel.ps1                  # All services
#   .\scripts\admin-tunnel.ps1 -Service ejbca   # EJBCA only
#   .\scripts\admin-tunnel.ps1 -Service db      # PostgreSQL only
# ═══════════════════════════════════════════════════════════════

param(
    [ValidateSet("all", "ejbca", "signserver", "minio", "db", "redis", "grafana")]
    [string]$Service = "all",

    [string]$VPS = "root@45.134.226.56",

    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519"
)

$ErrorActionPreference = "Stop"

# ─── Service definitions ───
# IMPORTANT: Use 127.0.0.1 (not localhost) — localhost may resolve to ::1 (IPv6)
# and Docker Swarm ingress only listens on IPv4.
# Local ports use 1xxxx prefix to avoid conflicts with local Docker dev containers.
$services = @{
    ejbca      = @{ LocalPort = 18443; RemotePort = 8443; Protocol = "https"; Path = "/ejbca/adminweb/"; Name = "EJBCA CA Admin" }
    signserver = @{ LocalPort = 19443; RemotePort = 9443; Protocol = "https"; Path = "/signserver/adminweb/"; Name = "SignServer Admin" }
    minio      = @{ LocalPort = 19001; RemotePort = 9001; Protocol = "http"; Path = "/"; Name = "MinIO Console" }
    db         = @{ LocalPort = 15433; RemotePort = 5433; Protocol = "tcp"; Path = $null; Name = "PostgreSQL" }
    redis      = @{ LocalPort = 26379; RemotePort = 6379; Protocol = "tcp"; Path = $null; Name = "Redis" }
    grafana    = @{ LocalPort = 13000; RemotePort = 3000; Protocol = "http"; Path = "/"; Name = "Grafana" }
}

# ─── Select services ───
if ($Service -eq "all") {
    $selected = $services.Keys
}
else {
    $selected = @($Service)
}

# ─── Build SSH tunnel arguments ───
$tunnelArgs = @()
foreach ($svc in $selected) {
    $s = $services[$svc]
    $tunnelArgs += "-L"
    # Bind local side to 127.0.0.1 and forward to 127.0.0.1 on remote (avoid IPv6)
    $tunnelArgs += "127.0.0.1:$($s.LocalPort):127.0.0.1:$($s.RemotePort)"
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  IVF Admin Tunnel (Azure Bastion-style)" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Tunneling to: $VPS" -ForegroundColor Yellow
Write-Host ""

foreach ($svc in $selected) {
    $s = $services[$svc]
    if ($s.Protocol -in @("http", "https")) {
        $url = "$($s.Protocol)://127.0.0.1:$($s.LocalPort)$($s.Path)"
        Write-Host "  $($s.Name):" -ForegroundColor Green -NoNewline
        Write-Host " $url"
    }
    else {
        Write-Host "  $($s.Name):" -ForegroundColor Green -NoNewline
        Write-Host " 127.0.0.1:$($s.LocalPort)"
    }
}

Write-Host ""
Write-Host "  Press Ctrl+C to close all tunnels" -ForegroundColor DarkGray
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ─── Launch SSH tunnel ───
$sshArgs = @(
    "-i", $SshKey,
    "-N",                      # No remote command
    "-o", "ServerAliveInterval=60",
    "-o", "ServerAliveCountMax=3",
    "-o", "ExitOnForwardFailure=yes"
) + $tunnelArgs + @($VPS)

try {
    & ssh @sshArgs
}
catch {
    Write-Host "SSH tunnel closed." -ForegroundColor Yellow
}
