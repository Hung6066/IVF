#!/usr/bin/env pwsh
# ========================================================================
# Script: Setup EJBCA Client Certificate for REST API Access
# Purpose: Extract admin.p12 from EJBCA container and configure IVF.API
# ========================================================================

Write-Host "🔐 Setting up EJBCA Client Certificate Authentication..." -ForegroundColor Cyan
Write-Host ""

# Check if EJBCA container is running
Write-Host "1️⃣ Checking EJBCA container status..." -ForegroundColor Yellow
$ejbcaRunning = docker ps --filter "name=ivf-ejbca" --format "{{.Names}}" | Select-String "ivf-ejbca"

if (-not $ejbcaRunning) {
    Write-Host "❌ EJBCA container is not running!" -ForegroundColor Red
    Write-Host "   Start it with: docker compose up -d ejbca" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ EJBCA container is running" -ForegroundColor Green
Write-Host ""

# Wait for EJBCA to be fully ready
Write-Host "2️⃣ Waiting for EJBCA to be ready (this may take 2-3 minutes)..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$ejbcaReady = $false

while ($attempt -lt $maxAttempts -and -not $ejbcaReady) {
    $attempt++
    Write-Host "   Attempt $attempt/$maxAttempts..." -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri "https://localhost:8443/ejbca/publicweb/healthcheck/ejbcahealth" `
            -SkipCertificateCheck -TimeoutSec 5 -ErrorAction SilentlyContinue
        
        if ($response.StatusCode -eq 200) {
            $ejbcaReady = $true
            Write-Host "✅ EJBCA is ready!" -ForegroundColor Green
        }
    }
    catch {
        Start-Sleep -Seconds 10
    }
}

if (-not $ejbcaReady) {
    Write-Host "❌ EJBCA did not become ready in time!" -ForegroundColor Red
    Write-Host "   Check logs: docker logs ivf-ejbca" -ForegroundColor Gray
    exit 1
}

Write-Host ""

# Extract superadmin.p12 from EJBCA container
Write-Host "3️⃣ Extracting admin certificate from EJBCA..." -ForegroundColor Yellow

# The default superadmin certificate is generated during EJBCA first start
# Common locations in EJBCA CE:
# - /opt/keyfactor/appserver/standalone/configuration/keystore/superadmin.p12
# - /opt/keyfactor/persistent/conf/p12/superadmin.p12
# - /opt/primekey/persistent/etc/ejbca/ManagementCA/superadmin.p12
# Default password: ejbca

$certPath = "./certs"
if (-not (Test-Path $certPath)) {
    New-Item -ItemType Directory -Path $certPath | Out-Null
    Write-Host "   Created $certPath directory" -ForegroundColor Gray
}

Write-Host "   Searching for admin certificates in container..." -ForegroundColor Gray

# Try to find all .p12 files
$p12Files = docker exec ivf-ejbca find /opt -name "*.p12" 2>$null | Where-Object { $_ }

if ($p12Files) {
    Write-Host "   Found .p12 certificate files:" -ForegroundColor Green
    $p12Files | ForEach-Object { Write-Host "     - $_" -ForegroundColor Gray }
    
    # Try each common path in order
    $pathsToTry = @(
        "/opt/keyfactor/appserver/standalone/configuration/keystore/superadmin.p12",
        "/opt/keyfactor/persistent/conf/p12/superadmin.p12",
        "/opt/primekey/persistent/etc/ejbca/ManagementCA/superadmin.p12"
    )
    
    $foundCert = $false
    foreach ($path in $pathsToTry) {
        if ($p12Files -contains $path) {
            Write-Host "   Copying from: $path" -ForegroundColor Gray
            docker cp "ivf-ejbca:$path" "$certPath/ejbca-admin.p12" 2>$null
            
            if (Test-Path "$certPath/ejbca-admin.p12") {
                $foundCert = $true
                break
            }
        }
    }
    
    # If standard paths didn't work, try the first .p12 file found
    if (-not $foundCert -and $p12Files.Count -gt 0) {
        $firstP12 = $p12Files[0]
        Write-Host "   Trying first found certificate: $firstP12" -ForegroundColor Gray
        docker cp "ivf-ejbca:$firstP12" "$certPath/ejbca-admin.p12" 2>$null
        
        if (Test-Path "$certPath/ejbca-admin.p12") {
            $foundCert = $true
        }
    }
    
    if (-not $foundCert) {
        Write-Host "❌ Could not copy certificate from container!" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Manual extraction:" -ForegroundColor Yellow
        Write-Host "   Try one of these paths:" -ForegroundColor Gray
        $p12Files | ForEach-Object { 
            Write-Host "   docker cp ivf-ejbca:$_ ./certs/ejbca-admin.p12" -ForegroundColor Gray 
        }
        exit 1
    }
}
else {
    Write-Host "❌ No .p12 certificate files found in EJBCA container!" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔍 Possible causes:" -ForegroundColor Yellow
    Write-Host "   1. EJBCA is still initializing (may take 5-10 minutes on first start)" -ForegroundColor Gray
    Write-Host "   2. EJBCA setup failed - check logs: docker logs ivf-ejbca" -ForegroundColor Gray
    Write-Host "   3. Different EJBCA version with different paths" -ForegroundColor Gray
    Write-Host ""
    Write-Host "💡 Wait a few more minutes and run this script again, or:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Check EJBCA Admin Web UI (accepts self-signed cert):" -ForegroundColor Gray
    Write-Host "   https://localhost:8443/ejbca/adminweb/" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   If you can access the admin web, you can create a new admin certificate there:" -ForegroundColor Gray
    Write-Host "   1. Navigate to RA Web → Make New Request" -ForegroundColor Gray
    Write-Host "   2. Create a new administrator certificate" -ForegroundColor Gray
    Write-Host "   3. Download as PKCS#12 (.p12)" -ForegroundColor Gray
    Write-Host "   4. Place in ./certs/ejbca-admin.p12" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Or explore the container filesystem:" -ForegroundColor Gray
    Write-Host "   docker exec -it ivf-ejbca bash" -ForegroundColor Gray
    Write-Host "   find /opt -name '*.p12' -o -name '*admin*'" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "✅ Certificate extracted: $certPath/ejbca-admin.p12" -ForegroundColor Green
Write-Host ""

# Update appsettings.json
Write-Host "4️⃣ Configuring API appsettings..." -ForegroundColor Yellow

$appSettingsPath = "./src/IVF.API/appsettings.json"
$appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json

# Update DigitalSigning section
$appSettings.DigitalSigning.ClientCertificatePath = "/app/certs/ejbca-admin.p12"
$appSettings.DigitalSigning.ClientCertificatePassword = "ejbca"

$appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath

Write-Host "✅ Updated appsettings.json" -ForegroundColor Green
Write-Host ""

# Update Dockerfile to copy certificate
Write-Host "5️⃣ Updating Dockerfile..." -ForegroundColor Yellow

$dockerfilePath = "./src/IVF.API/Dockerfile"
$dockerfile = Get-Content $dockerfilePath -Raw

# Check if certificate copy already exists
if ($dockerfile -notmatch "COPY.*certs/ejbca-admin.p12") {
    # Add certificate copy before ENTRYPOINT
    $dockerfile = $dockerfile -replace "(WORKDIR /app\s+COPY --from=publish /app/publish \.)", 
    "`$1`nRUN mkdir -p /app/certs`nCOPY certs/ejbca-admin.p12 /app/certs/"
    
    Set-Content $dockerfilePath $dockerfile
    Write-Host "✅ Updated Dockerfile" -ForegroundColor Green
}
else {
    Write-Host "✅ Dockerfile already configured" -ForegroundColor Green
}

Write-Host ""

# Update docker-compose.yml
Write-Host "6️⃣ Updating docker-compose.yml..." -ForegroundColor Yellow

$composePath = "./docker-compose.yml"
$compose = Get-Content $composePath -Raw

# Check if certificate volume mount already exists
if ($compose -notmatch "certs/ejbca-admin.p12:/app/certs/ejbca-admin.p12") {
    # Add volume mount under api service
    $compose = $compose -replace "(api:.*?environment:)", 
    "`$1`n    volumes:`n      - ./certs/ejbca-admin.p12:/app/certs/ejbca-admin.p12:ro"
    
    Set-Content $composePath $compose
    Write-Host "✅ Updated docker-compose.yml" -ForegroundColor Green
}
else {
    Write-Host "✅ docker-compose.yml already configured" -ForegroundColor Green
}

Write-Host ""
Write-Host "🎉 Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Rebuild and restart API:" -ForegroundColor White
Write-Host "      docker compose up -d --build api" -ForegroundColor Gray
Write-Host ""
Write-Host "   2. Verify EJBCA REST API access in admin dashboard:" -ForegroundColor White
Write-Host "      http://localhost:4200/admin/signing" -ForegroundColor Gray
Write-Host ""
Write-Host "   3. The EJBCA tab should now show Certificate Authorities list" -ForegroundColor White
Write-Host ""
Write-Host "📝 Certificate Details:" -ForegroundColor Cyan
Write-Host "   Path: $certPath/ejbca-admin.p12" -ForegroundColor Gray
Write-Host "   Password: ejbca" -ForegroundColor Gray
Write-Host "   Container Path: /app/certs/ejbca-admin.p12" -ForegroundColor Gray
Write-Host ""
