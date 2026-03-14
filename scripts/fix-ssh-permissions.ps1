# Fix SSH permissions on VPS and verify key setup
# Usage: .\fix-ssh-permissions.ps1 -RootPassword "your_root_password"

param(
    [Parameter(Mandatory=$true)]
    [string]$RootPassword,
    
    [string]$VpsIp = "10.200.0.1",
    
    [string]$TotpCode = ""
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 SSH Permissions Fix Script for VPS" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create SSH commands to execute on VPS
$sshCommands = @"
# Fix SSH permissions
chmod 700 ~/.ssh 2>/dev/null || mkdir -p ~/.ssh && chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys 2>/dev/null || true

# Show current .ssh status
echo '=== SSH Directory Permissions ==='
ls -ld ~/.ssh
echo ''
echo '=== authorized_keys File Permissions ==='
ls -l ~/.ssh/authorized_keys
echo ''
echo '=== Public Keys in authorized_keys ==='
echo "Total keys: \$(wc -l < ~/.ssh/authorized_keys)"
echo ''
echo '=== Key Details ==='
grep -o 'ssh-rsa.*\|ssh-ed25519.*' ~/.ssh/authorized_keys | sed 's/^/  /' | while read -r key; do
  comment=\$(echo "\$key" | awk '{print \$(NF)}')
  type=\$(echo "\$key" | awk '{print \$1}')
  echo "  Type: \$type | Comment: \$comment"
done
"@

# Step 2: Execute commands via SSH
Write-Host "📡 Connecting to VPS at $VpsIp..." -ForegroundColor Yellow

try {
    # Use sshpass if available, otherwise prompt for password interactively
    if (Get-Command sshpass -ErrorAction SilentlyContinue) {
        Write-Host "✓ Using sshpass for automated authentication" -ForegroundColor Green
        
        if ($TotpCode) {
            # If TOTP code provided, combine password + TOTP
            $authCommand = "echo '$RootPassword`n$TotpCode' | sshpass -p '$RootPassword' ssh -o PubkeyAuthentication=no -o KbdInteractiveAuthentication=yes root@$VpsIp"
        } else {
            $authCommand = "sshpass -p '$RootPassword' ssh -o PubkeyAuthentication=no root@$VpsIp"
        }
        
        $result = Invoke-Expression "$authCommand '$sshCommands'" 2>&1
    } else {
        # Fallback: Use expect script on WSL/Git Bash
        Write-Host "ℹ️  sshpass not found, using expect via bash..." -ForegroundColor Yellow
        
        $expectScript = @"
#!/usr/bin/expect -f
set timeout 10
set password "$RootPassword"
set totp "$TotpCode"
set host "$VpsIp"

spawn ssh root@`${host}
expect {
    "password:" {
        send "`${password}\r"
        if {`${totp} ne ""} {
            expect "Code:" { send "`${totp}\r" }
        }
    }
    timeout { puts "Connection timeout"; exit 1 }
}

expect "~#" { send "$sshCommands\r" }
expect "~#" { send "exit\r" }
expect eof
"@

        # Save expect script temporarily
        $expectFile = "$env:TEMP\fix-ssh.exp"
        Set-Content -Path $expectFile -Value $expectScript -Encoding ASCII
        
        & bash -c "expect `'$expectFile`'"
        Remove-Item $expectFile -Force
        exit
    }
    
    Write-Host ""
    Write-Host "✅ VPS SSH Setup Results:" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host $result
    
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Manual Fix" -ForegroundColor Yellow
    Write-Host "1. Open VPS web console at provider's panel"
    Write-Host "2. Run these commands:"
    Write-Host ""
    Write-Host "  chmod 700 ~/.ssh"
    Write-Host "  chmod 600 ~/.ssh/authorized_keys"
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "✨ Setup complete! Try SSH again:" -ForegroundColor Cyan
Write-Host "  ssh root@$VpsIp"
