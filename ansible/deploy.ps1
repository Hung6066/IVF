# ═══════════════════════════════════════════════════════════════
#  IVF Production Deployment Script — Windows PowerShell Wrapper
# ═══════════════════════════════════════════════════════════════
#
#  Sử dụng:
#    .\deploy.ps1 -Help
#    .\deploy.ps1 -Backend
#    .\deploy.ps1 -Frontend
#    .\deploy.ps1 -Full -Tag sha-abc123
#    .\deploy.ps1 -Full -Token $env:GHCR_TOKEN
#
# ═══════════════════════════════════════════════════════════════

param(
    [Parameter(HelpMessage = "Deploy backend (API) only")]
    [switch]$Backend,
    
    [Parameter(HelpMessage = "Deploy frontend only")]
    [switch]$Frontend,
    
    [Parameter(HelpMessage = "Deploy both backend and frontend")]
    [switch]$Full,
    
    [Parameter(HelpMessage = "Docker image tag (e.g., sha-c7d4766)")]
    [string]$Tag = "",
    
    [Parameter(HelpMessage = "GitHub Container Registry token")]
    [string]$Token = "",
    
    [Parameter(HelpMessage = "Show help message")]
    [switch]$Help,
    
    [Parameter(HelpMessage = "Show what would be deployed (no changes)")]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Colors (only work in newer PowerShell)
function Write-Info { Write-Host "ℹ  $args" -ForegroundColor Cyan }
function Write-Success { Write-Host "✅ $args" -ForegroundColor Green }
function Write-Warning { Write-Host "⚠️  $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "❌ $args" -ForegroundColor Red }

function Show-Help {
    Write-Host @"
╔════════════════════════════════════════════════════════════════════╗
║      IVF Production Deployment Script — Windows PowerShell         ║
╚════════════════════════════════════════════════════════════════════╝

USAGE:
  .\deploy.ps1 [OPTIONS]

OPTIONS:
  -Backend              Deploy backend (API) only
  -Frontend             Deploy frontend only
  -Full                 Deploy both backend and frontend (default)
  -Tag TAG              Docker image tag (e.g., sha-c7d4766)
  -Token TOKEN          GitHub Container Registry token
  -DryRun               Show what would be deployed (no changes)
  -Help                 Show this help message

EXAMPLES:
  # Deploy both (interactive - will prompt for token)
  .\deploy.ps1 -Full

  # Deploy only backend with specific tag
  .\deploy.ps1 -Backend -Tag sha-c7d4766

  # Deploy only frontend
  .\deploy.ps1 -Frontend -Tag latest

  # Dry run to see deployment details
  .\deploy.ps1 -DryRun

ENVIRONMENT VARIABLES:
  GHCR_TOKEN            GitHub Container Registry token

REQUIREMENTS:
  - WSL (Windows Subsystem for Linux) with Ansible installed
  - SSH access to production VPS (45.134.226.56)
  - Valid GitHub Container Registry token

"@
}

function Validate-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    # Check if WSL is available
    try {
        $wslCheck = wsl --list 2>&1
        Write-Success "WSL installed"
    }
    catch {
        Write-Error "WSL not found. Install WSL first: https://aka.ms/wsl"
        exit 1
    }
    
    # Check if ansible-playbook is in WSL
    $ansibleCheck = wsl bash -c "command -v ansible-playbook" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Ansible available in WSL"
    }
    else {
        Write-Warning "Ansible not found in WSL"
        Write-Info "Install with: wsl bash -c 'pip install ansible'"
        exit 1
    }
}

function Get-ImageTag {
    if ([string]::IsNullOrEmpty($Tag)) {
        # Try to get from git
        try {
            $shortSha = (git rev-parse --short HEAD 2>$null)
            if ($shortSha) {
                $Tag = "sha-$shortSha"
                Write-Info "Using git commit: $Tag"
            }
            else {
                $Tag = "latest"
                Write-Warning "Using default tag: $Tag"
            }
        }
        catch {
            $Tag = "latest"
            Write-Warning "Using default tag: $Tag"
        }
    }
    
    $script:Tag = $Tag
}

function Get-GhcrToken {
    if ([string]::IsNullOrEmpty($Token)) {
        # Try environment variable
        if (-not [string]::IsNullOrEmpty($env:GHCR_TOKEN)) {
            $Token = $env:GHCR_TOKEN
            Write-Success "Using GHCR_TOKEN from environment"
        }
        else {
            # Prompt for token
            $creds = Get-Credential -UserName "hung6066" -Message "Enter GitHub Container Registry Token"
            if ($creds) {
                $Token = $creds.GetNetworkCredential().Password
            }
        }
    }
    
    if ([string]::IsNullOrEmpty($Token)) {
        Write-Error "GHCR token is required"
        exit 1
    }
    
    $script:Token = $Token
}

function Show-DeploymentConfig {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗"
    Write-Host "║                    Deployment Configuration                        ║"
    Write-Host "╚════════════════════════════════════════════════════════════════════╝"
    Write-Host ""
    Write-Host "  Backend:        $(if ($script:DeployBackend) { 'ENABLED' } else { 'DISABLED' })"
    Write-Host "  Frontend:       $(if ($script:DeployFrontend) { 'ENABLED' } else { 'DISABLED' })"
    Write-Host "  Image Tag:      $script:Tag"
    Write-Host "  Registry:       ghcr.io"
    Write-Host "  API Image:      ghcr.io/hung6066/ivf:$script:Tag"
    Write-Host "  FE Image:       ghcr.io/hung6066/ivf-client:$script:Tag"
    Write-Host "  Dry Run:        $(if ($DryRun) { 'YES' } else { 'NO' })"
    Write-Host ""
}

function Confirm-Deployment {
    if ($DryRun) {
        Write-Warning "DRY RUN mode - no actual changes will be made"
    }
    
    $continue = Read-Host "Continue with deployment? (y/N)"
    if ($continue -notmatch "^[Yy]$") {
        Write-Info "Deployment cancelled"
        exit 0
    }
}

function Run-Deployment {
    Write-Info "Preparing deployment command for WSL..."
    
    # Convert Windows paths to WSL paths
    $playbookPath = wsl wslpath "$(Get-Location)\ansible"
    
    $extraVars = @(
        "deploy_backend=$($script:DeployBackend.ToString().ToLower())"
        "deploy_frontend=$($script:DeployFrontend.ToString().ToLower())"
        "image_tag=$script:Tag"
        "ghcr_token=$script:Token"
    ) -join " "
    
    $command = "cd '$playbookPath' && ansible-playbook -i hosts.yml deploy.yml -v --extra-vars '$extraVars'"
    
    if ($DryRun) {
        $command += " --check"
    }
    
    Write-Info "Running deployment via WSL..."
    Write-Host ""
    
    # Run the command in WSL
    wsl bash -c $command
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Deployment completed successfully"
        Write-Host ""
        Write-Host "Monitor progress at:"
        Write-Host "  📊 Grafana:     https://natra.site/grafana/"
        Write-Host "  🏥 Health:      https://natra.site/api/health/live"
        Write-Host "  🌐 Frontend:    https://natra.site/"
    }
    else {
        Write-Error "Deployment failed"
        exit 1
    }
}

# Main execution
function Main {
    # Show header
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║     IVF Production Deployment — Windows PowerShell Wrapper          ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    
    if ($Help) {
        Show-Help
        exit 0
    }
    
    # Determine deployment targets
    if (-not $Backend -and -not $Frontend -and -not $Full) {
        $script:DeployBackend = $true
        $script:DeployFrontend = $true
    }
    elseif ($Backend) {
        $script:DeployBackend = $true
        $script:DeployFrontend = $false
    }
    elseif ($Frontend) {
        $script:DeployBackend = $false
        $script:DeployFrontend = $true
    }
    else {
        $script:DeployBackend = $true
        $script:DeployFrontend = $true
    }
    
    # Run deployment steps
    Validate-Prerequisites
    Get-ImageTag
    Get-GhcrToken
    Show-DeploymentConfig
    Confirm-Deployment
    Run-Deployment
}

Main
