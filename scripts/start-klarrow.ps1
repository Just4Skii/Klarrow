param(
    [switch]$RunApi,
    [switch]$RunWeb,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ($RunApi) {
    $Host.UI.RawUI.WindowTitle = "Klarrow API  (http://127.0.0.1:5099)"
    $root = Split-Path -Parent $PSScriptRoot
    Set-Location $root
    Write-Host "Building and starting the Klarrow API..." -ForegroundColor Cyan
    Write-Host "First run builds the solution and seeds the demo workspace (can take a minute)." -ForegroundColor DarkGray
    dotnet run --project src\RecallGraph.Api --urls http://127.0.0.1:5099
    Write-Host ""
    Write-Host "API process exited." -ForegroundColor Yellow
    Read-Host "Press Enter to close this window" | Out-Null
    exit $LASTEXITCODE
}

if ($RunWeb) {
    $Host.UI.RawUI.WindowTitle = "Klarrow Web  (http://localhost:5173)"
    $frontend = Join-Path (Split-Path -Parent $PSScriptRoot) "src\frontend"
    Set-Location $frontend
    Write-Host "Starting the Klarrow web portal..." -ForegroundColor Cyan
    npm run dev
    Write-Host ""
    Write-Host "Web process exited." -ForegroundColor Yellow
    Read-Host "Press Enter to close this window" | Out-Null
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------- launcher

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$scriptPath = $PSCommandPath
$apiUrl = "http://127.0.0.1:5099"
$webUrl = "http://localhost:5173"
$ollamaUrl = "http://127.0.0.1:11434"

function Test-UrlOk([string]$url, [int]$timeoutSec = 3) {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec $timeoutSec
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 400)
    } catch { return $false }
}

function Wait-ForUrl([string]$url, [string]$label, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $spinner = @("|", "/", "-", "\")
    $i = 0
    while ((Get-Date) -lt $deadline) {
        if (Test-UrlOk $url) { Write-Host ""; return $true }
        Write-Host ("`r  waiting for {0} {1}   " -f $label, $spinner[$i % 4]) -NoNewline -ForegroundColor DarkGray
        $i++
        Start-Sleep -Milliseconds 1500
    }
    Write-Host ""
    return $false
}

Write-Host ""
Write-Host "  KLARROW" -ForegroundColor White -NoNewline
Write-Host "  -  evidence reconciliation workbench" -ForegroundColor DarkGray
Write-Host ""

$missing = @()
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += ".NET SDK 10    https://dotnet.microsoft.com/download" }
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { $missing += "Node.js + npm    https://nodejs.org" }
if ($missing.Count -gt 0) {
    Write-Host "  Missing prerequisites:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "  Install the missing tools, then run this launcher again."
    exit 1
}

$apiUp = Test-UrlOk "$apiUrl/api/health"
$webUp = Test-UrlOk "$webUrl/"

if ($apiUp) { Write-Host "  API   already running on $apiUrl" -ForegroundColor Green }
if ($webUp) { Write-Host "  Web   already running on $webUrl" -ForegroundColor Green }

if (-not $apiUp) {
    Write-Host "  API   starting on $apiUrl" -ForegroundColor Cyan
    $apiArgs = "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$scriptPath`" -RunApi"
    if ($DryRun) { Write-Host "  [dry-run] Start-Process powershell $apiArgs" -ForegroundColor DarkGray }
    else { Start-Process powershell -ArgumentList $apiArgs | Out-Null }
}

if (-not $webUp) {
    $frontend = Join-Path $root "src\frontend"
    if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
        Write-Host "  Web   installing dependencies (first run, this can take a few minutes)..." -ForegroundColor Cyan
        if ($DryRun) { Write-Host "  [dry-run] npm install in $frontend" -ForegroundColor DarkGray }
        else {
            Push-Location $frontend
            npm install
            Pop-Location
        }
    }
    Write-Host "  Web   starting on $webUrl" -ForegroundColor Cyan
    $webArgs = "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$scriptPath`" -RunWeb"
    if ($DryRun) { Write-Host "  [dry-run] Start-Process powershell $webArgs" -ForegroundColor DarkGray }
    else { Start-Process powershell -ArgumentList $webArgs | Out-Null }
}

$apiReady = $apiUp
$webReady = $webUp
if (-not $apiUp -and -not $DryRun) { $apiReady = Wait-ForUrl "$apiUrl/api/health" "API" 180 }
if (-not $webUp -and -not $DryRun) { $webReady = Wait-ForUrl "$webUrl/" "web portal" 90 }

if (Test-UrlOk "$ollamaUrl/api/tags" 2) {
    Write-Host "  AI    Ollama detected - live model extraction available" -ForegroundColor Green
} else {
    Write-Host "  AI    Ollama not running - deterministic parsers will be used (optional)" -ForegroundColor DarkGray
}

Write-Host ""
if ($DryRun) {
    Write-Host "  Dry run complete. No processes were started."
    exit 0
}

if ($apiReady -and $webReady) {
    Write-Host "  Klarrow is ready:  $webUrl" -ForegroundColor Green
    Write-Host "  Logs stay in the 'Klarrow API' and 'Klarrow Web' windows; close them to stop." -ForegroundColor DarkGray
    try { Start-Process $webUrl } catch { Write-Host "  Open $webUrl in your browser." -ForegroundColor Yellow }
    exit 0
}

Write-Host "  Starting is taking longer than expected. Check the service windows for errors." -ForegroundColor Yellow
Write-Host "  API health: $apiUrl/api/health    Web: $webUrl" -ForegroundColor Yellow
exit 1
