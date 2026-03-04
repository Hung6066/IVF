<#
.SYNOPSIS
    Thu thập bằng chứng kiểm toán tự động cho IVF Compliance
.DESCRIPTION
    Script thu thập evidence từ API, database, Git history.
    Output được lưu vào docs/compliance/evidence/ với format chuẩn.
.PARAMETER ApiUrl
    URL của backend API (default: http://localhost:5000)
.PARAMETER Token
    JWT token để authenticate API calls
.PARAMETER OutputDir
    Thư mục output (default: docs/compliance/evidence)
.PARAMETER Categories
    Danh sách categories cần thu thập (default: all)
.EXAMPLE
    .\collect-evidence.ps1 -Token "eyJhbG..."
    .\collect-evidence.ps1 -Categories "access_control","training" -Token $token
#>
param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$Token,
    [string]$OutputDir = "",
    [string[]]$Categories = @("access_control", "incident_response", "training", "change_management", "encryption", "backup", "vendor", "policy_versions"),
    [switch]$SkipApi,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "docs\compliance\evidence"
}

$today = Get-Date -Format "yyyy-MM-dd"
$quarter = "Q" + [Math]::Ceiling((Get-Date).Month / 3)
$year = (Get-Date).Year
$yearQuarter = "${year}-${quarter}"

# ─── Helpers ──────────────────────────────────────────────

function Write-Status($msg) { Write-Host "  [+] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "  [x] $msg" -ForegroundColor Red }

function Invoke-Api {
    param([string]$Path, [string]$Method = "GET")
    if ($SkipApi) { throw "API calls skipped" }
    if (-not $Token) { throw "No JWT token provided. Use -Token parameter." }

    $headers = @{
        "Authorization" = "Bearer $Token"
        "Content-Type"  = "application/json"
    }
    try {
        $response = Invoke-RestMethod -Uri "${ApiUrl}${Path}" -Method $Method -Headers $headers -TimeoutSec 30
        return $response
    }
    catch {
        Write-Warn "API call failed: $Path - $($_.Exception.Message)"
        return $null
    }
}

function Export-ToCsv {
    param($Data, [string]$FilePath)
    if ($null -eq $Data -or ($Data -is [array] -and $Data.Count -eq 0)) {
        Write-Warn "No data to export for $FilePath"
        return
    }
    $dir = Split-Path -Parent $FilePath
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    $Data | Export-Csv -Path $FilePath -NoTypeInformation -Encoding UTF8
    Write-Status "Exported: $FilePath ($(@($Data).Count) records)"
}

function Save-Json {
    param($Data, [string]$FilePath)
    if ($null -eq $Data) { return }
    $dir = Split-Path -Parent $FilePath
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    $Data | ConvertTo-Json -Depth 10 | Set-Content -Path $FilePath -Encoding UTF8
    Write-Status "Saved: $FilePath"
}

function Save-Text {
    param([string]$Text, [string]$FilePath)
    $dir = Split-Path -Parent $FilePath
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    $Text | Set-Content -Path $FilePath -Encoding UTF8
    Write-Status "Saved: $FilePath"
}

# ─── Access Control ──────────────────────────────────────

function Collect-AccessControl {
    Write-Host "`n=== ACCESS CONTROL ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "access_control"

    # User list
    $users = Invoke-Api "/api/users?page=1&pageSize=1000"
    if ($users) {
        $items = if ($users.items) { $users.items } elseif ($users.Items) { $users.Items } else { @() }
        Export-ToCsv -Data $items -FilePath (Join-Path $dir "${today}_user-list.csv")

        # Privileged users (Admin role)
        $admins = $items | Where-Object { $_.role -eq "Admin" -or $_.Role -eq "Admin" }
        if ($admins) {
            Export-ToCsv -Data $admins -FilePath (Join-Path $dir "${today}_privileged-users.csv")
        }

        # Inactive users
        $inactive = $items | Where-Object { ($_.isActive -eq $false) -or ($_.IsActive -eq $false) }
        if ($inactive) {
            Export-ToCsv -Data $inactive -FilePath (Join-Path $dir "${today}_inactive-users.csv")
        }

        # Summary
        $summary = @{
            Date          = $today
            TotalUsers    = @($items).Count
            ActiveUsers   = @($items | Where-Object { $_.isActive -ne $false -and $_.IsActive -ne $false }).Count
            AdminUsers    = @($admins).Count
            InactiveUsers = @($inactive).Count
        }
        Save-Json -Data $summary -FilePath (Join-Path $dir "${today}_access-summary.json")
    }

    # Roles
    $roles = Invoke-Api "/api/users/roles"
    if ($roles) {
        Save-Json -Data $roles -FilePath (Join-Path $dir "${today}_roles.json")
    }

    # Active sessions
    try {
        $sessions = Invoke-Api "/api/enterprise/sessions/active"
        if ($sessions) {
            Save-Json -Data $sessions -FilePath (Join-Path $dir "${today}_active-sessions.json")
        }
    } catch { Write-Warn "Enterprise sessions API not available" }

    # User groups
    try {
        $groups = Invoke-Api "/api/enterprise/groups?page=1&pageSize=100"
        if ($groups) {
            $groupItems = if ($groups.items) { $groups.items } elseif ($groups.Items) { $groups.Items } else { $groups }
            Save-Json -Data $groupItems -FilePath (Join-Path $dir "${today}_user-groups.json")
        }
    } catch { Write-Warn "Enterprise groups API not available" }

    Write-Status "Access control evidence collected"
}

# ─── Incident Response ───────────────────────────────────

function Collect-IncidentResponse {
    Write-Host "`n=== INCIDENT RESPONSE ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "incident_response"

    # Security incidents
    try {
        $incidents = Invoke-Api "/api/enterprise/security-incidents?page=1&pageSize=100"
        if ($incidents) {
            $items = if ($incidents.items) { $incidents.items } elseif ($incidents.Items) { $incidents.Items } else { @() }
            Export-ToCsv -Data $items -FilePath (Join-Path $dir "${yearQuarter}_incidents.csv")

            # Summary metrics
            $resolved = $items | Where-Object { $_.status -eq "Resolved" -or $_.Status -eq "Resolved" }
            $summary = @{
                Period          = $yearQuarter
                TotalIncidents  = @($items).Count
                ResolvedCount   = @($resolved).Count
                OpenCount       = @($items).Count - @($resolved).Count
            }
            Save-Json -Data $summary -FilePath (Join-Path $dir "${yearQuarter}_incident-summary.json")
        }
    } catch { Write-Warn "Security incidents API not available" }

    # Incident response rules
    try {
        $rules = Invoke-Api "/api/enterprise/incident-response-rules"
        if ($rules) {
            Save-Json -Data $rules -FilePath (Join-Path $dir "${today}_ir-rules.json")
        }
    } catch { Write-Warn "IR rules API not available" }

    # Breach notifications
    try {
        $breaches = Invoke-Api "/api/compliance/breaches"
        if ($breaches) {
            Export-ToCsv -Data $breaches -FilePath (Join-Path $dir "${yearQuarter}_breaches.csv")
        }
    } catch { Write-Warn "Breach API not available" }

    Write-Status "Incident response evidence collected"
}

# ─── Training ────────────────────────────────────────────

function Collect-Training {
    Write-Host "`n=== TRAINING ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "training"

    # All trainings
    $trainings = Invoke-Api "/api/compliance/trainings?page=1&pageSize=1000"
    if ($trainings) {
        $items = if ($trainings.items) { $trainings.items } elseif ($trainings.Items) { $trainings.Items } else { @() }
        Export-ToCsv -Data $items -FilePath (Join-Path $dir "${yearQuarter}_training-all.csv")

        # Completed
        $completed = $items | Where-Object { $_.isCompleted -eq $true -or $_.IsCompleted -eq $true }
        if ($completed) {
            Export-ToCsv -Data $completed -FilePath (Join-Path $dir "${yearQuarter}_training-completed.csv")
        }

        # Overdue
        $overdue = Invoke-Api "/api/compliance/trainings?overdue=true&page=1&pageSize=1000"
        if ($overdue) {
            $overdueItems = if ($overdue.items) { $overdue.items } elseif ($overdue.Items) { $overdue.Items } else { @() }
            if ($overdueItems) {
                Export-ToCsv -Data $overdueItems -FilePath (Join-Path $dir "${yearQuarter}_training-overdue.csv")
            }
        }

        # Summary by type
        $types = @("HIPAA", "GDPR", "Security Awareness", "Incident Response", "Data Handling", "AI Ethics")
        $typeSummary = foreach ($type in $types) {
            $typeItems = $items | Where-Object { $_.trainingType -eq $type -or $_.TrainingType -eq $type }
            $typeCompleted = $typeItems | Where-Object { $_.isCompleted -eq $true -or $_.IsCompleted -eq $true }
            [PSCustomObject]@{
                Type           = $type
                Total          = @($typeItems).Count
                Completed      = @($typeCompleted).Count
                CompletionRate = if (@($typeItems).Count -gt 0) { [math]::Round(@($typeCompleted).Count / @($typeItems).Count * 100, 1) } else { 0 }
            }
        }
        Export-ToCsv -Data $typeSummary -FilePath (Join-Path $dir "${yearQuarter}_training-summary.csv")
    }

    Write-Status "Training evidence collected"
}

# ─── Change Management ───────────────────────────────────

function Collect-ChangeManagement {
    Write-Host "`n=== CHANGE MANAGEMENT ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "change_management"

    Push-Location $repoRoot

    # Git log for current quarter
    $quarterStart = Get-Date -Year $year -Month (([Math]::Ceiling((Get-Date).Month / 3) - 1) * 3 + 1) -Day 1 -Hour 0 -Minute 0 -Second 0
    $sinceDate = $quarterStart.ToString("yyyy-MM-dd")

    $gitLog = git log --since="$sinceDate" --oneline --no-merges 2>&1
    if ($gitLog) {
        Save-Text -Text ($gitLog -join "`n") -FilePath (Join-Path $dir "${yearQuarter}_git-log.txt")
    }

    # Contributors
    $contributors = git shortlog -sn --since="$sinceDate" --no-merges 2>&1
    if ($contributors) {
        Save-Text -Text ($contributors -join "`n") -FilePath (Join-Path $dir "${yearQuarter}_contributors.txt")
    }

    # Tags/releases
    $tags = git tag -l "v*" --sort=-creatordate 2>&1
    if ($tags) {
        Save-Text -Text ($tags -join "`n") -FilePath (Join-Path $dir "${year}_releases.txt")
    }

    # Commit stats
    $stats = git log --since="$sinceDate" --no-merges --format="%H" 2>&1
    $commitCount = if ($stats -is [array]) { $stats.Count } else { if ($stats) { 1 } else { 0 } }
    $authorCount = (git log --since="$sinceDate" --no-merges --format="%aN" 2>&1 | Sort-Object -Unique).Count

    $summary = @{
        Period         = $yearQuarter
        Since          = $sinceDate
        TotalCommits   = $commitCount
        UniqueAuthors  = $authorCount
        Releases       = if ($tags -is [array]) { $tags.Count } else { if ($tags) { 1 } else { 0 } }
    }
    Save-Json -Data $summary -FilePath (Join-Path $dir "${yearQuarter}_change-summary.json")

    # EF Core migrations list
    $migrations = Get-ChildItem -Path (Join-Path $repoRoot "src\IVF.Infrastructure\Migrations") -Filter "*.cs" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch "Snapshot|Designer" } |
        Select-Object Name, LastWriteTime |
        Sort-Object LastWriteTime -Descending
    if ($migrations) {
        Export-ToCsv -Data $migrations -FilePath (Join-Path $dir "${yearQuarter}_ef-migrations.csv")
    }

    Pop-Location
    Write-Status "Change management evidence collected"
}

# ─── Encryption ──────────────────────────────────────────

function Collect-Encryption {
    Write-Host "`n=== ENCRYPTION ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "encryption"

    # Certificate inventory from certs/ directory
    $certFiles = Get-ChildItem -Path (Join-Path $repoRoot "certs") -Recurse -Include "*.pem","*.crt","*.p12","*.pfx" -ErrorAction SilentlyContinue
    if ($certFiles) {
        $certInventory = foreach ($cert in $certFiles) {
            [PSCustomObject]@{
                File         = $cert.Name
                Path         = $cert.FullName.Replace($repoRoot, ".")
                Size         = $cert.Length
                LastModified = $cert.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            }
        }
        Export-ToCsv -Data $certInventory -FilePath (Join-Path $dir "${year}_certificate-inventory.csv")
    }

    # TLS config from appsettings
    $appSettings = Join-Path $repoRoot "src\IVF.API\appsettings.json"
    if (Test-Path $appSettings) {
        $config = Get-Content $appSettings -Raw | ConvertFrom-Json
        $tlsConfig = @{
            Date = $today
            JwtAlgorithm = "HMAC-SHA256"
            DatabaseSsl = if ($config.ConnectionStrings.DefaultConnection -match "SslMode") { "Enabled" } else { "Not configured" }
            MinioEndpoint = if ($config.MinIO) { $config.MinIO.Endpoint } else { "Not configured" }
            SignServerUrl = if ($config.DigitalSigning) { $config.DigitalSigning.SignServerUrl } else { "Not configured" }
        }
        Save-Json -Data $tlsConfig -FilePath (Join-Path $dir "${year}_tls-config.json")
    }

    # Check if OpenSSL is available for deeper checks
    try {
        $opensslVersion = openssl version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Save-Text -Text $opensslVersion -FilePath (Join-Path $dir "${year}_openssl-version.txt")
        }
    } catch { Write-Warn "OpenSSL not available for TLS checks" }

    Write-Status "Encryption evidence collected"
}

# ─── Backup ──────────────────────────────────────────────

function Collect-Backup {
    Write-Host "`n=== BACKUP ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "backup"

    $backupDir = Join-Path $repoRoot "backups"
    if (-not (Test-Path $backupDir)) {
        Write-Warn "Backup directory not found: $backupDir"
        return
    }

    # Backup file inventory
    $backupFiles = Get-ChildItem -Path $backupDir -File | Sort-Object LastWriteTime -Descending
    if ($backupFiles) {
        $inventory = foreach ($file in $backupFiles) {
            # Parse date from filename: ivf_db_YYYYMMDD_HHMMSS.sql.gz.sha256
            $dateMatch = [regex]::Match($file.Name, '(\d{8})_(\d{6})')
            $backupDate = if ($dateMatch.Success) { $dateMatch.Groups[1].Value } else { "" }

            [PSCustomObject]@{
                FileName     = $file.Name
                Size         = $file.Length
                BackupDate   = $backupDate
                LastModified = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                Type         = if ($file.Name -match "basebackup") { "Base Backup" } else { "SQL Dump" }
                HasSha256    = $file.Extension -eq ".sha256"
            }
        }
        Export-ToCsv -Data $inventory -FilePath (Join-Path $dir "${today}_backup-inventory.csv")

        # Monthly summary
        $currentMonth = (Get-Date).ToString("yyyy-MM")
        $monthlyBackups = $backupFiles | Where-Object { $_.LastWriteTime.ToString("yyyy-MM") -eq $currentMonth }
        $summary = @{
            Date               = $today
            TotalBackupFiles   = $backupFiles.Count
            ThisMonthBackups   = @($monthlyBackups).Count
            LatestBackup       = $backupFiles[0].Name
            LatestBackupDate   = $backupFiles[0].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            OldestBackup       = $backupFiles[-1].Name
            TotalSizeBytes     = ($backupFiles | Measure-Object -Property Length -Sum).Sum
        }
        Save-Json -Data $summary -FilePath (Join-Path $dir "${today}_backup-summary.json")
    }

    Write-Status "Backup evidence collected"
}

# ─── Vendor ──────────────────────────────────────────────

function Collect-Vendor {
    Write-Host "`n=== VENDOR ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "vendor"

    # Asset inventory from API (SaaS / ThirdParty)
    try {
        $assets = Invoke-Api "/api/compliance/assets?page=1&pageSize=100"
        if ($assets) {
            $items = if ($assets.items) { $assets.items } elseif ($assets.Items) { $assets.Items } else { @() }
            if ($items) {
                Export-ToCsv -Data $items -FilePath (Join-Path $dir "${year}_asset-inventory.csv")
            }
        }
    } catch { Write-Warn "Asset inventory API not available" }

    # Docker compose vendor analysis
    $dockerCompose = Join-Path $repoRoot "docker-compose.yml"
    if (Test-Path $dockerCompose) {
        $composeContent = Get-Content $dockerCompose -Raw
        $services = [regex]::Matches($composeContent, 'image:\s*(.+)')
        if ($services.Count -gt 0) {
            $vendorImages = foreach ($match in $services) {
                $image = $match.Groups[1].Value.Trim()
                [PSCustomObject]@{
                    Image      = $image
                    Source     = "docker-compose.yml"
                    RecordDate = $today
                }
            }
            Export-ToCsv -Data $vendorImages -FilePath (Join-Path $dir "${year}_docker-images.csv")
        }
    }

    # NuGet package list (vendor dependencies)
    Push-Location $repoRoot
    $csprojFiles = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse
    $allPackages = @()
    foreach ($csproj in $csprojFiles) {
        [xml]$xml = Get-Content $csproj.FullName
        $packages = $xml.SelectNodes("//PackageReference")
        foreach ($pkg in $packages) {
            $allPackages += [PSCustomObject]@{
                Project = $csproj.Directory.Name
                Package = $pkg.GetAttribute("Include")
                Version = $pkg.GetAttribute("Version")
            }
        }
    }
    if ($allPackages) {
        Export-ToCsv -Data ($allPackages | Sort-Object Package -Unique) -FilePath (Join-Path $dir "${year}_nuget-packages.csv")
    }

    # npm packages
    $packageJson = Join-Path $repoRoot "ivf-client\package.json"
    if (Test-Path $packageJson) {
        $pkg = Get-Content $packageJson -Raw | ConvertFrom-Json
        $npmPackages = @()
        if ($pkg.dependencies) {
            $pkg.dependencies.PSObject.Properties | ForEach-Object {
                $npmPackages += [PSCustomObject]@{ Package = $_.Name; Version = $_.Value; Type = "dependency" }
            }
        }
        if ($pkg.devDependencies) {
            $pkg.devDependencies.PSObject.Properties | ForEach-Object {
                $npmPackages += [PSCustomObject]@{ Package = $_.Name; Version = $_.Value; Type = "devDependency" }
            }
        }
        if ($npmPackages) {
            Export-ToCsv -Data $npmPackages -FilePath (Join-Path $dir "${year}_npm-packages.csv")
        }
    }
    Pop-Location

    Write-Status "Vendor evidence collected"
}

# ─── Policy Versions ─────────────────────────────────────

function Collect-PolicyVersions {
    Write-Host "`n=== POLICY VERSIONS ===" -ForegroundColor Cyan
    $dir = Join-Path $OutputDir "policy_versions"

    Push-Location $repoRoot

    # Policy file inventory with last git commit
    $policyFiles = Get-ChildItem -Path "docs\compliance" -Filter "*.md" -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -notmatch "evidence" }

    if ($policyFiles) {
        $inventory = foreach ($file in $policyFiles) {
            $relativePath = $file.FullName.Replace($repoRoot + "\", "").Replace("\", "/")
            $lastCommit = git log -1 --format="%H|%ai|%s" -- $relativePath 2>&1
            $parts = if ($lastCommit -is [string]) { $lastCommit.Split("|") } else { @("","","") }

            [PSCustomObject]@{
                Policy       = $file.BaseName -replace '_', ' '
                File         = $relativePath
                LastModified = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                LastCommit   = if ($parts.Count -ge 1) { $parts[0].Substring(0, [Math]::Min(8, $parts[0].Length)) } else { "" }
                CommitDate   = if ($parts.Count -ge 2) { $parts[1] } else { "" }
                CommitMsg    = if ($parts.Count -ge 3) { $parts[2] } else { "" }
            }
        }
        Export-ToCsv -Data $inventory -FilePath (Join-Path $dir "${year}_policy-inventory.csv")
    }

    # Full git history for compliance docs
    $policyHistory = git log --oneline --follow -- "docs/compliance/*.md" 2>&1
    if ($policyHistory) {
        Save-Text -Text ($policyHistory -join "`n") -FilePath (Join-Path $dir "${year}_policy-git-history.txt")
    }

    Pop-Location
    Write-Status "Policy version evidence collected"
}

# ─── Main Execution ──────────────────────────────────────

Write-Host "╔═══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   IVF Compliance Evidence Collector               ║" -ForegroundColor Cyan
Write-Host "║   Date: $today                              ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host "Categories: $($Categories -join ', ')" -ForegroundColor Gray
if ($SkipApi) { Write-Warn "API calls SKIPPED (offline mode)" }
Write-Host ""

$startTime = Get-Date
$collected = @()
$failed = @()

foreach ($category in $Categories) {
    try {
        switch ($category) {
            "access_control"    { Collect-AccessControl }
            "incident_response" { Collect-IncidentResponse }
            "training"          { Collect-Training }
            "change_management" { Collect-ChangeManagement }
            "encryption"        { Collect-Encryption }
            "backup"            { Collect-Backup }
            "vendor"            { Collect-Vendor }
            "policy_versions"   { Collect-PolicyVersions }
            default             { Write-Warn "Unknown category: $category" }
        }
        $collected += $category
    }
    catch {
        Write-Err "Failed: $category - $($_.Exception.Message)"
        $failed += $category
    }
}

$elapsed = (Get-Date) - $startTime

# Generate collection report
$report = @{
    CollectionDate  = $today
    Duration        = $elapsed.ToString("mm\:ss")
    ApiUrl          = $ApiUrl
    ApiSkipped      = $SkipApi.IsPresent
    Categories      = @{
        Collected = $collected
        Failed    = $failed
    }
    OutputDirectory = $OutputDir
}
Save-Json -Data $report -FilePath (Join-Path $OutputDir "_collection-report_${today}.json")

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " Done in $($elapsed.ToString('mm\:ss'))" -ForegroundColor Green
Write-Host " Collected: $($collected.Count)/$($Categories.Count) categories" -ForegroundColor $(if ($failed.Count -eq 0) { "Green" } else { "Yellow" })
if ($failed.Count -gt 0) {
    Write-Host " Failed: $($failed -join ', ')" -ForegroundColor Red
}
Write-Host " Report: $OutputDir\_collection-report_${today}.json" -ForegroundColor Gray
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
