# Automatically add local SSH public key to VPS authorized_keys
# Usage: .\add-ssh-key-to-vps.ps1 -RootPassword "password" -LocalKeyFile "id_rsa.pub"

param(
    [Parameter(Mandatory=$true)]
    [string]$RootPassword,
    
    [string]$LocalKeyFile = "~/.ssh/id_rsa.pub",
    
    [string]$VpsIp = "10.200.0.1",
    
    [string]$TotpCode = ""
)

$ErrorActionPreference = "Stop"

Write-Host "🔑 Adding SSH Public Key to VPS" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Expand path
$KeyPath = $LocalKeyFile -replace '~', $HOME

# Check if key exists
if (-not (Test-Path $KeyPath)) {
    Write-Host "❌ Key file not found: $KeyPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Available keys:" -ForegroundColor Yellow
    Get-ChildItem ~/.ssh/*.pub 2>/dev/null | ForEach-Object { Write-Host "  • $($_.Name)" }
    exit 1
}

# Read key
$keyContent = Get-Content $KeyPath
Write-Host "✓ Key file found: $(Split-Path -Leaf $KeyPath)" -ForegroundColor Green
Write-Host "  Key type: $(($keyContent -split ' ')[0])" -ForegroundColor Gray
Write-Host "  Key comment: $(($keyContent -split ' ')[-1])" -ForegroundColor Gray
Write-Host ""

# Escape key for bash/ssh
$escapedKey = $keyContent.Replace('"', '\"').Replace('$', '\$')

# Create SSH command to add key
$sshCommand = @"
# Add key to authorized_keys if not already present
KEY=`"$escapedKey`"
AUTHKEYS=~/.ssh/authorized_keys

# Create .ssh directory if missing
mkdir -p ~/.ssh

# Add key if not already present
if ! grep -q `"\${KEY:0:50}`" `$AUTHKEYS 2>/dev/null; then
  echo `"\$KEY`" >> `$AUTHKEYS
  echo "✨ Key added to authorized_keys"
else
  echo "ℹ️  Key already present in authorized_keys"
fi

# Fix permissions
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

# Verify
echo ""
echo "📊 Final check:"
echo "  Total keys: \$(wc -l < ~/.ssh/authorized_keys)"
echo "  Your key check: $(grep -q "$escapedKey" && echo "✅ FOUND" || echo "❌ NOT FOUND")"
"@

Write-Host "🔄 Connecting to VPS..." -ForegroundColor Yellow

try {
    if (Get-Command sshpass -ErrorAction SilentlyContinue) {
        Write-Host "  Using sshpass for authentication" -ForegroundColor Gray
        
        if ($TotpCode) {
            # sshpass doesn't support keyboard-interactive directly
            # Fall back to expect or manual
            Write-Host "  ⚠️  TOTP code detected but sshpass doesn't support it" -ForegroundColor Yellow
            Write-Host "  Please enter password manually when prompted..." -ForegroundColor Yellow
            Start-Process "ssh" -ArgumentList @("-v", "root@$VpsIp") -NoNewWindow -Wait
        } else {
            $result = echo "$sshCommand" | & sshpass -p "$RootPassword" ssh -o PubkeyAuthentication=no -o StrictHostKeyChecking=no root@$VpsIp 2>&1
        }
    } else {
        Write-Host "  sshpass not installed, you'll be prompted for password" -ForegroundColor Yellow
        Write-Host ""
        $result = echo "$sshCommand" | ssh -o PubkeyAuthentication=no root@$VpsIp 2>&1
    }
    
    Write-Host ""
    Write-Host "✅ VPS Response:" -ForegroundColor Green
    Write-Host "=================" -ForegroundColor Green
    Write-Host $result
    Write-Host ""
    
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host "✨ Done! Test your SSH connection:" -ForegroundColor Cyan
Write-Host "  ssh root@$VpsIp" -ForegroundColor Gray
