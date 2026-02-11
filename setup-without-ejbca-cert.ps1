#!/usr/bin/env pwsh
# ========================================================================
# Script: Run Without EJBCA Client Certificate
# Purpose: Configure API to work without EJBCA REST API access
# Note: SignServer PDF signing will still work - only EJBCA admin tab affected
# ========================================================================

Write-Host "🔧 Configuring API without EJBCA client certificate..." -ForegroundColor Cyan
Write-Host ""
Write-Host "ℹ️  Note: This skips EJBCA REST API integration." -ForegroundColor Yellow
Write-Host "   - PDF signing via SignServer will work normally" -ForegroundColor Gray
Write-Host "   - EJBCA admin tab will show 'Forbidden' message (expected)" -ForegroundColor Gray
Write-Host "   - Use EJBCA Admin Web UI directly: https://localhost:8443/ejbca/adminweb/" -ForegroundColor Gray
Write-Host ""

# Update appsettings.json to remove certificate paths
Write-Host "1️⃣ Updating appsettings.json..." -ForegroundColor Yellow

$appSettingsPath = "./src/IVF.API/appsettings.json"

if (-not (Test-Path $appSettingsPath)) {
    Write-Host "❌ appsettings.json not found at $appSettingsPath" -ForegroundColor Red
    exit 1
}

$appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json

# Remove certificate configuration
$appSettings.DigitalSigning.ClientCertificatePath = $null
$appSettings.DigitalSigning.ClientCertificatePassword = $null

# Ensure signing is enabled
$appSettings.DigitalSigning.Enabled = $true

$appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath

Write-Host "✅ Removed certificate configuration from appsettings.json" -ForegroundColor Green
Write-Host ""

# Update docker-compose.yml to remove volume mount
Write-Host "2️⃣ Updating docker-compose.yml..." -ForegroundColor Yellow

$composePath = "./docker-compose.yml"
$compose = Get-Content $composePath -Raw

# Remove certificate environment variables
$compose = $compose -replace '\s*- DigitalSigning__ClientCertificatePath=.*\n', ''
$compose = $compose -replace '\s*- DigitalSigning__ClientCertificatePassword=.*\n', ''

# Remove volume mount
$compose = $compose -replace '\s*volumes:\s*\n\s*- \./certs/ejbca-admin\.p12:/app/certs/ejbca-admin\.p12:ro\n', ''

Set-Content $composePath $compose

Write-Host "✅ Removed certificate mount from docker-compose.yml" -ForegroundColor Green
Write-Host ""

Write-Host "🎉 Configuration complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Restart API:" -ForegroundColor White
Write-Host "      docker compose restart api" -ForegroundColor Gray
Write-Host "      # Or for local dev: cd src/IVF.API && dotnet run" -ForegroundColor DarkGray
Write-Host ""
Write-Host "   2. Test digital signing:" -ForegroundColor White
Write-Host "      Navigate to: http://localhost:4200/admin/signing" -ForegroundColor Gray
Write-Host "      Click 'Kiểm tra' tab → 'Chạy kiểm tra ký số'" -ForegroundColor Gray
Write-Host ""
Write-Host "✅ Expected behavior:" -ForegroundColor Green
Write-Host "   - Dashboard: Shows system status" -ForegroundColor Gray
Write-Host "   - SignServer tab: Shows worker info (✅ working)" -ForegroundColor Gray
Write-Host "   - EJBCA tab: Shows 'Forbidden' message (⚠️ expected - use Admin Web UI)" -ForegroundColor Gray
Write-Host "   - Test tab: PDF signing works (✅ working)" -ForegroundColor Gray
Write-Host ""
Write-Host "🌐 Access EJBCA Admin Web UI directly:" -ForegroundColor Cyan
Write-Host "   https://localhost:8443/ejbca/adminweb/" -ForegroundColor Gray
Write-Host "   (Accept self-signed certificate warning)" -ForegroundColor DarkGray
Write-Host ""
