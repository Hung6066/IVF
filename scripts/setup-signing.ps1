# ============================================================
# IVF Digital Signing Infrastructure Setup Script (PowerShell)
# Sets up EJBCA CA + SignServer for PDF document signing
# ============================================================
# Usage: .\scripts\setup-signing.ps1
# Requires: Docker Desktop running
# ============================================================

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $PSScriptRoot

function Write-Info { param($msg) Write-Host "[INFO] " -ForegroundColor Blue -NoNewline; Write-Host $msg }
function Write-Ok { param($msg) Write-Host "[OK] " -ForegroundColor Green -NoNewline; Write-Host $msg }
function Write-Warn { param($msg) Write-Host "[WARN] " -ForegroundColor Yellow -NoNewline; Write-Host $msg }
function Write-Err { param($msg) Write-Host "[ERROR] " -ForegroundColor Red -NoNewline; Write-Host $msg }

# ─── Configuration ───────────────────────────────────────────
$EjbcaUrl = "https://localhost:8443"
$EjbcaPublicUrl = "http://localhost:8442"
$SignServerUrl = "https://localhost:9443"
$SignServerHttpUrl = "http://localhost:9080"
$SignerCertCn = "IVF PDF Signer"
$CertValidityDays = 1095
$WorkerName = "PDFSigner"
$WorkerId = 1
$KeystorePassword = "changeit"
$KeyAlias = "signer"
$ContainerKeystorePath = "/tmp/signer.p12"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  IVF Digital Signing Infrastructure Setup" -ForegroundColor Cyan
Write-Host "  EJBCA (Certificate Authority) + SignServer (PDF Signer)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ─── Step 1: Start Docker services ──────────────────────────
Write-Info "Step 1: Starting EJBCA and SignServer Docker containers..."
Push-Location $ProjectDir

docker compose up -d ejbca-db signserver-db
Write-Info "Waiting for databases to be healthy..."
Start-Sleep -Seconds 10

docker compose up -d ejbca
Write-Info "Waiting for EJBCA to start (this may take 2-3 minutes on first run)..."

$maxRetries = 60
for ($i = 1; $i -le $maxRetries; $i++) {
    try {
        $null = Invoke-WebRequest -Uri "$EjbcaUrl/ejbca/publicweb/healthcheck/ejbcahealth" `
            -SkipCertificateCheck -ErrorAction Stop -TimeoutSec 5
        Write-Ok "EJBCA is healthy!"
        break
    }
    catch {
        if ($i -eq $maxRetries) {
            Write-Err "EJBCA failed to start after $maxRetries retries"
            docker compose logs ejbca --tail=50
            exit 1
        }
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 5
    }
}

docker compose up -d signserver
Write-Info "Waiting for SignServer to start..."
for ($i = 1; $i -le $maxRetries; $i++) {
    try {
        $null = Invoke-WebRequest -Uri "$SignServerUrl/signserver/healthcheck/signserverhealth" `
            -SkipCertificateCheck -ErrorAction Stop -TimeoutSec 5
        Write-Ok "SignServer is healthy!"
        break
    }
    catch {
        if ($i -eq $maxRetries) {
            Write-Err "SignServer failed to start after $maxRetries retries"
            docker compose logs signserver --tail=50
            exit 1
        }
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 5
    }
}

Write-Host ""
Write-Ok "All containers are running!"

# ─── Step 2: Generate Signer Keystore (inside container) ────
Write-Info "Step 2: Generating PKCS12 keystore with Java keytool inside SignServer container..."

# Generate keystore using Java keytool inside the container for guaranteed Java compatibility
docker exec ivf-signserver keytool -genkeypair `
    -alias $KeyAlias `
    -keyalg RSA -keysize 2048 -sigalg SHA256withRSA `
    -validity $CertValidityDays `
    -dname "CN=$SignerCertCn,O=IVF Clinic,OU=Digital Signing,C=VN" `
    -keystore $ContainerKeystorePath `
    -storetype PKCS12 `
    -storepass $KeystorePassword `
    -keypass $KeystorePassword 2>&1 | Out-Null

# Verify keystore was created
$keytoolCheck = docker exec ivf-signserver keytool -list -keystore $ContainerKeystorePath -storepass $KeystorePassword 2>&1
if ($keytoolCheck -match $KeyAlias) {
    Write-Ok "Keystore created with alias '$KeyAlias'"
}
else {
    Write-Err "Failed to create keystore!"
    exit 1
}

# ─── Step 3: Configure SignServer PDFSigner Worker ───────────
Write-Info "Step 3: Configuring SignServer PDFSigner worker..."

# Use a properties file for the base config
$propsContent = @"
GLOB.WORKER${WorkerId}.CLASSPATH = org.signserver.module.pdfsigner.PDFSigner
GLOB.WORKER${WorkerId}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.P12CryptoToken
WORKER${WorkerId}.NAME = $WorkerName
WORKER${WorkerId}.AUTHTYPE = NOAUTH
WORKER${WorkerId}.DEFAULTKEY = $KeyAlias
WORKER${WorkerId}.KEYSTOREPATH = $ContainerKeystorePath
WORKER${WorkerId}.KEYSTOREPASSWORD = $KeystorePassword
"@

$propsFile = Join-Path $env:TEMP "signserver-worker.properties"
$propsContent | Set-Content -Path $propsFile -Encoding UTF8
docker cp $propsFile ivf-signserver:/tmp/worker.properties

Write-Info "Loading worker properties..."
docker exec ivf-signserver bin/signserver setproperties /tmp/worker.properties 2>&1 | Out-Null

# Fix TYPE property (setproperties has a known bug that resets TYPE to empty)
Write-Info "Setting TYPE and additional worker properties..."
docker exec ivf-signserver bin/signserver setproperty $WorkerId TYPE PROCESSABLE 2>&1 | Out-Null
docker exec ivf-signserver bin/signserver setproperty $WorkerId CERTIFICATION_LEVEL NOT_CERTIFIED 2>&1 | Out-Null
docker exec ivf-signserver bin/signserver setproperty $WorkerId ADD_VISIBLE_SIGNATURE false 2>&1 | Out-Null
docker exec ivf-signserver bin/signserver setproperty $WorkerId REASON "Xac nhan bao cao y te IVF" 2>&1 | Out-Null
docker exec ivf-signserver bin/signserver setproperty $WorkerId LOCATION "IVF Clinic" 2>&1 | Out-Null
docker exec ivf-signserver bin/signserver setproperty $WorkerId REFUSE_DOUBLE_INDIRECT_OBJECTS true 2>&1 | Out-Null

# Reload worker
docker exec ivf-signserver bin/signserver reload $WorkerId 2>&1 | Out-Null
Write-Ok "Worker configured and reloaded"

# Activate crypto token
Write-Info "Activating crypto token..."
$activateResult = docker exec ivf-signserver bin/signserver activatecryptotoken $WorkerId $KeystorePassword 2>&1
if ($activateResult -match "successful") {
    Write-Ok "Crypto token activated!"
}
else {
    Write-Err "Crypto token activation failed: $activateResult"
    Write-Info "Check logs: docker logs ivf-signserver --tail=50"
    exit 1
}

# Verify worker status
$statusResult = docker exec ivf-signserver bin/signserver getstatus brief all 2>&1
if ($statusResult -match "Active") {
    Write-Ok "PDFSigner worker is Active!"
}
else {
    Write-Warn "Worker status: $statusResult"
}

# ─── Step 4: Test PDF Signing ────────────────────────────────
Write-Info "Step 4: Testing PDF signing via REST API..."

# Create a minimal valid test PDF
$testPdf = Join-Path $env:TEMP "test-signing.pdf"
$testContent = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Test PDF) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000266 00000 n 
trailer
<< /Size 5 /Root 1 0 R >>
startxref
362
%%EOF
"@
[System.IO.File]::WriteAllText($testPdf, $testContent)

$signedPdf = Join-Path $env:TEMP "test-signed.pdf"
try {
    $null = Invoke-WebRequest -Uri "$SignServerHttpUrl/signserver/process" `
        -Method POST `
        -Form @{
        workerName = $WorkerName
        data       = Get-Item $testPdf
    } `
        -OutFile $signedPdf `
        -ErrorAction Stop

    $signedSize = (Get-Item $signedPdf).Length
    Write-Ok "PDF signing test passed! (signed output: $signedSize bytes)"
}
catch {
    Write-Warn "Test signing failed: $_"
    Write-Info "Complete setup at: $SignServerUrl/signserver/adminweb/"
}

Pop-Location

# ─── Summary ─────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Services:" -ForegroundColor White
Write-Host "    EJBCA Admin:       $EjbcaUrl/ejbca/adminweb/"
Write-Host "    EJBCA Public:      $EjbcaPublicUrl/ejbca/publicweb/"
Write-Host "    SignServer Admin:  $SignServerUrl/signserver/adminweb/"
Write-Host "    SignServer REST:   $SignServerHttpUrl/signserver/process"
Write-Host ""
Write-Host "  SignServer Worker:" -ForegroundColor White
Write-Host "    Name:       $WorkerName (ID: $WorkerId)"
Write-Host "    Key Alias:  $KeyAlias"
Write-Host "    Keystore:   $ContainerKeystorePath (inside container)"
Write-Host "    Password:   $KeystorePassword"
Write-Host ""
Write-Host "  IVF API Endpoints:" -ForegroundColor White
Write-Host "    Export with signing:  GET /api/forms/responses/{id}/export-pdf?sign=true"
Write-Host "    Report with signing:  GET /api/forms/reports/{id}/export-pdf?sign=true"
Write-Host "    Upload & sign:        POST /api/signing/sign-pdf"
Write-Host "    Health check:         GET /api/signing/health"
Write-Host ""
Write-Host "  Next Steps:" -ForegroundColor Yellow
Write-Host "    1. Set DigitalSigning:Enabled=true in appsettings.json (or env var)"
Write-Host "    2. Restart IVF API"
Write-Host '    3. Test: Invoke-RestMethod http://localhost:5000/api/signing/health'
Write-Host ""
Write-Ok "Digital signing infrastructure is ready!"
