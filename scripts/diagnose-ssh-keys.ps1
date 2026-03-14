# Diagnose SSH key mismatch between local machine and VPS
# Usage: .\diagnose-ssh-keys.ps1 -RootPassword "password"

param(
    [Parameter(Mandatory=$true)]
    [string]$RootPassword,
    
    [string]$VpsIp = "10.200.0.1",
    
    [string]$TotpCode = ""
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 SSH Key Diagnostic Tool" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Display local public keys
Write-Host "📁 Local Public Keys:" -ForegroundColor Yellow
Write-Host "-------------------" -ForegroundColor Yellow

$localKeys = @()

if (Test-Path ~/.ssh/id_rsa.pub) {
    $key = Get-Content ~/.ssh/id_rsa.pub
    $localKeys += [PSCustomObject]@{
        File = "id_rsa.pub"
        Type = "RSA"
        Content = $key
        Fingerprint = & ssh-keygen -lf ~/.ssh/id_rsa.pub 2>$null | awk '{print $2}'
    }
    Write-Host "✓ id_rsa.pub found (RSA 2048-bit)" -ForegroundColor Green
}

if (Test-Path ~/.ssh/id_ed25519_wsl.pub) {
    $key = Get-Content ~/.ssh/id_ed25519_wsl.pub
    $localKeys += [PSCustomObject]@{
        File = "id_ed25519_wsl.pub"
        Type = "Ed25519"
        Content = $key
    }
    Write-Host "✓ id_ed25519_wsl.pub found (Ed25519)" -ForegroundColor Green
}

if (Test-Path ~/.ssh/id_ed25519.pub) {
    $key = Get-Content ~/.ssh/id_ed25519.pub
    $localKeys += [PSCustomObject]@{
        File = "id_ed25519.pub"
        Type = "Ed25519"
        Content = $key
    }
    Write-Host "✓ id_ed25519.pub found (Ed25519)" -ForegroundColor Green
}

if ($localKeys.Count -eq 0) {
    Write-Host "❌ No SSH keys found in ~/.ssh/" -ForegroundColor Red
    Write-Host ""
    Write-Host "Generate new key pair:" -ForegroundColor Yellow
    Write-Host "  ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -N '""'"
    exit 1
}

Write-Host ""

# Step 2: Get VPS authorized_keys
Write-Host "📡 Fetching VPS authorized_keys..." -ForegroundColor Yellow

$sshCommand = @"
grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys 2>/dev/null | sort
"@

try {
    if (Get-Command sshpass -ErrorAction SilentlyContinue) {
        $authCommand = "sshpass -p '$RootPassword' ssh -o PubkeyAuthentication=no -o StrictHostKeyChecking=no root@$VpsIp"
    } else {
        $authCommand = "ssh -o PubkeyAuthentication=no root@$VpsIp"
        Write-Host "⚠️  sshpass not found, SSH will prompt for password interactively" -ForegroundColor Yellow
    }
    
    $vpsKeys = Invoke-Expression "$authCommand '$sshCommand'" 2>&1
} catch {
    Write-Host "❌ Failed to connect to VPS: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔑 VPS authorized_keys:" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Yellow

if ($vpsKeys -and $vpsKeys.Count -gt 0) {
    $vpsKeys | ForEach-Object {
        $type = if ($_ -match '^ssh-ed25519') { "Ed25519" } else { "RSA" }
        $comment = ($_ -split ' ')[-1]
        Write-Host "  • $type → $comment" -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ No keys found in authorized_keys!" -ForegroundColor Red
}

Write-Host ""

# Step 3: Compare keys
Write-Host "🔄 Comparing Local vs VPS Keys:" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Yellow
Write-Host ""

$missingKeys = @()
$foundKeys = @()

foreach ($localKey in $localKeys) {
    # Extract key content (everything except comment)
    $keyParts = $localKey.Content -split '\s+'
    $keyData = $keyParts[0] + ' ' + $keyParts[1]  # ssh-ed25519 AAAA... or ssh-rsa AAAA...
    
    $isFound = $false
    if ($vpsKeys) {
        foreach ($vpsKey in $vpsKeys) {
            $vpsKeyData = ($vpsKey -split '\s+')[0..1] -join ' '
            if ($vpsKeyData -eq $keyData) {
                $isFound = $true
                break
            }
        }
    }
    
    if ($isFound) {
        Write-Host "✅ $($localKey.File) - FOUND on VPS" -ForegroundColor Green
        $foundKeys += $localKey
    } else {
        Write-Host "❌ $($localKey.File) - MISSING on VPS" -ForegroundColor Red
        $missingKeys += $localKey
    }
}

Write-Host ""

# Step 4: Recommendation
if ($missingKeys.Count -eq 0) {
    Write-Host "✨ All local keys are authorized on VPS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  • SSH config might be blocking the key"
    Write-Host "  • Check SSH config at ~/.ssh/config"
    Write-Host "  • Verify key permissions: ls -la ~/.ssh/id_*"
    Write-Host "  • Try connecting with verbose: ssh -vv root@$VpsIp"
    exit 0
}

Write-Host "⚠️  FOUND MISMATCH - $($missingKeys.Count) key(s) not authorized on VPS" -ForegroundColor Red
Write-Host ""
Write-Host "Missing keys:" -ForegroundColor Yellow
$missingKeys | ForEach-Object {
    Write-Host "  • $($_.File) ($($_.Type))" -ForegroundColor Red
}

Write-Host ""
Write-Host "Solution:" -ForegroundColor Green
Write-Host ""

# Option 1: Auto-add
Write-Host "Option 1️⃣  - Auto-add missing key to VPS:" -ForegroundColor Green

$keyToAdd = $missingKeys[0]
$keyContent = $keyToAdd.Content

Write-Host ""
Write-Host "  Run this command:" -ForegroundColor Cyan
Write-Host "  " + ("echo `"" + $keyContent + "`" >> ~/.ssh/authorized_keys") -ForegroundColor Gray
Write-Host ""

# Option 2: Manual via script
Write-Host "Option 2️⃣  - Use script to add:"  -ForegroundColor Green
Write-Host ""

$addKeyScript = @"
`$key = @"
$keyContent
"@

echo "`$key" | ssh root@$VpsIp 'cat >> ~/.ssh/authorized_keys'
ssh root@$VpsIp 'chmod 600 ~/.ssh/authorized_keys; echo SSH key added successfully'
"@

$scriptFile = Join-Path -Path $env:TEMP -ChildPath "add-ssh-key.ps1"
Set-Content -Path $scriptFile -Value $addKeyScript

Write-Host "  .\add-ssh-key.ps1 (password will be prompted)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Script saved to: $scriptFile"

Write-Host ""
Write-Host "Option 3️⃣  - Manual via VPS console:"  -ForegroundColor Green
Write-Host ""
Write-Host "  1. Open VPS provider's web console"
Write-Host "  2. Copy this entire key:" -ForegroundColor Cyan
Write-Host ""

# Show key in chunks for easy copying
$lines = $keyContent | Select-String -Pattern '.' -AllMatches | ForEach-Object { $_.Line }
$lines | ForEach-Object { Write-Host "     $_" -ForegroundColor Gray }

Write-Host ""
Write-Host "  3. Run on VPS console:" -ForegroundColor Cyan
Write-Host "     echo 'PASTE_KEY_HERE' >> ~/.ssh/authorized_keys" -ForegroundColor Gray
Write-Host "     chmod 600 ~/.ssh/authorized_keys" -ForegroundColor Gray
Write-Host ""

Write-Host "Then test:" -ForegroundColor Yellow
Write-Host "  ssh root@$VpsIp" -ForegroundColor Cyan
