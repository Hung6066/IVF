# Post-Scaffold Check
# Reads PostToolUse hook input from stdin.
# If a file was created/edited in src/ or ivf-client/src/, injects a build reminder.

$input = $null
try {
    $input = [Console]::In.ReadToEnd() | ConvertFrom-Json -ErrorAction SilentlyContinue
}
catch { }

if (-not $input) {
    exit 0
}

$toolName = $input.toolName
$filePath = ''

# Extract file path from tool input if available
if ($input.toolInput -and $input.toolInput.filePath) {
    $filePath = $input.toolInput.filePath
}

# Only act on file creation/edit tools
$editTools = @('create_file', 'replace_string_in_file', 'multi_replace_string_in_file')
if ($toolName -notin $editTools) {
    exit 0
}

# Check if the edited file is in backend or frontend source
$isBackend = $filePath -match '[\\/]src[\\/]IVF\.'
$isFrontend = $filePath -match '[\\/]ivf-client[\\/]src[\\/]'

if ($isBackend -or $isFrontend) {
    $parts = @()
    if ($isBackend) { $parts += '`dotnet build`' }
    if ($isFrontend) { $parts += '`npm run build` (in ivf-client/)' }
    $msg = "Build verification needed: run $($parts -join ' and ') to confirm the changes compile."

    $output = @{
        systemMessage = $msg
    } | ConvertTo-Json -Compress

    Write-Output $output
}

exit 0
