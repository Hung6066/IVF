#!/usr/bin/env pwsh
# ========================================================================
# Script: Check EJBCA Certificates
# Purpose: Diagnose EJBCA certificate locations and status
# ========================================================================

Write-Host "🔍 EJBCA Certificate Diagnostic Tool" -ForegroundColor Cyan
Write-Host ""

# Check if EJBCA container is running
Write-Host "📦 Checking EJBCA container..." -ForegroundColor Yellow
$ejbcaRunning = docker ps --filter "name=ivf-ejbca" --format "{{.Names}}" | Select-String "ivf-ejbca"

if (-not $ejbcaRunning) {
    Write-Host "❌ EJBCA container is not running!" -ForegroundColor Red
    Write-Host "   Start it with: docker compose up -d ejbca" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Container 'ivf-ejbca' is running" -ForegroundColor Green
Write-Host ""

# Check EJBCA health
Write-Host "💚 Checking EJBCA health..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:8443/ejbca/publicweb/healthcheck/ejbcahealth" `
        -SkipCertificateCheck -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ EJBCA is healthy (HTTP $($response.StatusCode))" -ForegroundColor Green
}
catch {
    Write-Host "⚠️ EJBCA health check failed or still starting up" -ForegroundColor Yellow
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}
Write-Host ""

# Search for .p12 files
Write-Host "🔐 Searching for .p12 certificate files..." -ForegroundColor Yellow
$p12Files = docker exec ivf-ejbca find /opt -name "*.p12" 2>$null | Where-Object { $_ }

if ($p12Files) {
    Write-Host "✅ Found $($p12Files.Count) certificate file(s):" -ForegroundColor Green
    Write-Host ""
    
    $index = 1
    foreach ($file in $p12Files) {
        Write-Host "   [$index] $file" -ForegroundColor Cyan
        
        # Try to get file info
        $fileInfo = docker exec ivf-ejbca ls -lh $file 2>$null
        if ($fileInfo) {
            Write-Host "       $fileInfo" -ForegroundColor Gray
        }
        
        $index++
    }
    
    Write-Host ""
    Write-Host "📋 To extract a certificate, run:" -ForegroundColor Yellow
    Write-Host "   docker cp ivf-ejbca:<path> ./certs/ejbca-admin.p12" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Example:" -ForegroundColor Yellow
    Write-Host "   docker cp ivf-ejbca:$($p12Files[0]) ./certs/ejbca-admin.p12" -ForegroundColor Cyan
    Write-Host ""
    
    # Recommend most likely candidate
    $candidates = $p12Files | Where-Object { $_ -match "superadmin|admin|management" }
    if ($candidates) {
        Write-Host "💡 Recommended certificate (contains 'admin'):" -ForegroundColor Yellow
        Write-Host "   $($candidates[0])" -ForegroundColor Green
        Write-Host ""
        Write-Host "   Quick extract command:" -ForegroundColor Yellow
        Write-Host "   mkdir -p certs; docker cp ivf-ejbca:$($candidates[0]) ./certs/ejbca-admin.p12" -ForegroundColor Cyan
    }
}
else {
    Write-Host "❌ No .p12 files found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔍 Possible reasons:" -ForegroundColor Yellow
    Write-Host "   1. EJBCA is still initializing (first start can take 5-10 minutes)" -ForegroundColor Gray
    Write-Host "   2. EJBCA setup failed" -ForegroundColor Gray
    Write-Host "   3. Certificates stored in unexpected location" -ForegroundColor Gray
    Write-Host ""
}

# Search for keystore directories
Write-Host "📁 Checking keystore directories..." -ForegroundColor Yellow
$keystoreDirs = docker exec ivf-ejbca find /opt -type d -name "*keystore*" -o -name "*p12*" 2>$null | Where-Object { $_ }

if ($keystoreDirs) {
    Write-Host "   Found potential keystore locations:" -ForegroundColor Gray
    $keystoreDirs | ForEach-Object { Write-Host "     - $_" -ForegroundColor Gray }
    Write-Host ""
}

# Check EJBCA logs for certificate generation
Write-Host "📜 Checking recent EJBCA logs for certificate info..." -ForegroundColor Yellow
$logs = docker logs ivf-ejbca --tail 50 2>&1 | Select-String -Pattern "superadmin|certificate|p12|keystore" -CaseSensitive:$false

if ($logs) {
    Write-Host "   Recent certificate-related log entries:" -ForegroundColor Gray
    $logs | Select-Object -First 5 | ForEach-Object { 
        Write-Host "     $_" -ForegroundColor DarkGray 
    }
    Write-Host ""
}

# Alternative: Access admin web UI
Write-Host "🌐 Alternative: Use EJBCA Admin Web UI" -ForegroundColor Cyan
Write-Host "   If certificates aren't auto-generated, create one manually:" -ForegroundColor Gray
Write-Host ""
Write-Host "   1. Open in browser: https://localhost:8443/ejbca/adminweb/" -ForegroundColor Gray
Write-Host "      (Accept self-signed certificate warning)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "   2. Navigate to: RA Web → Make New Request" -ForegroundColor Gray
Write-Host ""
Write-Host "   3. Create administrator certificate:" -ForegroundColor Gray
Write-Host "      - Key algorithm: RSA 2048" -ForegroundColor DarkGray
Write-Host "      - End Entity Profile: ENDUSER or ADMINISTRATOR" -ForegroundColor DarkGray
Write-Host "      - Certificate Profile: ENDUSER or ADMINISTRATOR" -ForegroundColor DarkGray
Write-Host ""
Write-Host "   4. Download as PKCS#12 (.p12 format)" -ForegroundColor Gray
Write-Host ""
Write-Host "   5. Save to: ./certs/ejbca-admin.p12" -ForegroundColor Gray
Write-Host ""

# Container shell access
Write-Host "🐚 Manual exploration:" -ForegroundColor Cyan
Write-Host "   docker exec -it ivf-ejbca bash" -ForegroundColor Gray
Write-Host "   # Then run inside container:" -ForegroundColor DarkGray
Write-Host "   find /opt -name '*.p12'" -ForegroundColor DarkGray
Write-Host "   find /opt -name '*admin*'" -ForegroundColor DarkGray
Write-Host ""

Write-Host "✅ Diagnostic complete" -ForegroundColor Green
