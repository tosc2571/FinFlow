# FinFlow launcher. Run this any time you want to use FinFlow:
#   - No local install yet, or a newer release exists -> downloads it, verifies its
#     SHA256 checksum, and installs it into .\app (existing data is untouched - the
#     database lives outside .\app, see README).
#   - Already up to date -> just starts the app.
#   - Already running -> just opens the browser.
# Fully automatic, no prompts. Safe to re-run any time.
#
# -LauncherDir lets finflow.bat (a plain double-clickable wrapper - .ps1 files don't run on
# double-click) invoke this script's content directly without saving it to disk first, in which
# case $PSScriptRoot would be empty; the .bat passes its own folder explicitly instead.
param(
    [string]$LauncherDir = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'

$repo = 'tosc2571/FinFlow'
$launcherDir = $LauncherDir
$appDir = Join-Path $launcherDir 'app'
$versionFile = Join-Path $appDir 'VERSION'
$healthUrl = 'http://localhost:5199/api/import/batches'

function Get-LocalVersion {
    if (Test-Path $versionFile) { return (Get-Content $versionFile -Raw).Trim() }
    return $null
}

function Get-LatestRelease {
    try {
        return Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" `
            -Headers @{ 'User-Agent' = 'FinFlow-Launcher' }
    }
    catch {
        $status = $_.Exception.Response.StatusCode
        if ($status -eq 404) {
            Write-Warning "No FinFlow release has been published yet."
        }
        else {
            Write-Warning "Could not reach GitHub to check for updates: $($_.Exception.Message)"
        }
        return $null
    }
}

function Install-Update($release) {
    $tag = $release.tag_name
    $assetName = "finflow-$tag-win-x64.zip"
    $asset = $release.assets | Where-Object { $_.name -eq $assetName }
    $checksumAsset = $release.assets | Where-Object { $_.name -eq "$assetName.sha256" }
    if (-not $asset -or -not $checksumAsset) {
        throw "Release $tag is missing the expected win-x64 assets ($assetName [.sha256])."
    }

    Write-Host "Downloading FinFlow $tag ..."
    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $tmpDir | Out-Null
    try {
        $zipPath = Join-Path $tmpDir 'finflow.zip'
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath
        # Download-to-file-then-read, not .Content directly: GitHub serves release
        # assets as application/octet-stream, which makes Invoke-WebRequest return
        # .Content as a byte array rather than a string, breaking .Trim().
        $shaPath = Join-Path $tmpDir 'finflow.zip.sha256'
        Invoke-WebRequest -Uri $checksumAsset.browser_download_url -OutFile $shaPath
        $expectedHash = (Get-Content -Path $shaPath -Raw).Trim().ToLower()

        $actualHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLower()
        if ($actualHash -ne $expectedHash) {
            throw "Checksum mismatch for $tag (expected $expectedHash, got $actualHash). Update aborted - existing install untouched."
        }
        Write-Host "Checksum verified."

        # Extract into a staging folder first and only swap it into place once we know
        # it's a valid FinFlow build - minimizes the window where .\app could end up
        # half-replaced if the download or extraction failed partway.
        $stagingDir = Join-Path $tmpDir 'staged'
        Expand-Archive -Path $zipPath -DestinationPath $stagingDir -Force
        if (-not (Test-Path (Join-Path $stagingDir 'FinFlow.Api.exe'))) {
            throw "Downloaded archive for $tag doesn't look like a valid FinFlow release (FinFlow.Api.exe missing)."
        }

        Write-Host "Installing ..."
        if (Test-Path $appDir) { Remove-Item -Recurse -Force $appDir }
        Move-Item -Path $stagingDir -Destination $appDir
        Write-Host "FinFlow updated to $tag."
    }
    finally {
        Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
    }
}

function Test-Running {
    try {
        Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# 1. Update check.
$localVersion = Get-LocalVersion
$release = Get-LatestRelease

if ($release -and $release.tag_name -ne $localVersion) {
    Install-Update $release
}
elseif (-not $release -and -not $localVersion) {
    throw "No local FinFlow install found and no release is available - nothing to start."
}
elseif (-not $release) {
    Write-Warning "Starting the existing install without checking for updates."
}
else {
    Write-Host "FinFlow is up to date ($localVersion)."
}

$exe = Join-Path $appDir 'FinFlow.Api.exe'
if (-not (Test-Path $exe)) {
    throw "FinFlow is not installed and no update could be downloaded."
}

# 2. Start (unless it's already running) and open the browser.
if (Test-Running) {
    Write-Host "FinFlow is already running."
}
else {
    Write-Host "Starting FinFlow ..."
    Start-Process -FilePath $exe -WorkingDirectory $appDir
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        if (Test-Running) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        Write-Warning "FinFlow didn't respond in time - open http://localhost:5199 manually once it's up."
    }
}

Start-Process 'http://localhost:5199'
