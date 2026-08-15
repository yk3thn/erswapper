param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$clean = $Version.TrimStart('v', 'V')
if ($clean -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like 1.2.3 (got '$Version')."
}

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'ERSwapper\ERSwapper.csproj'
$staging = Join-Path $root "artifacts\ERSwapper-v$clean"
$zip = Join-Path $root "artifacts\ERSwapper-v$clean.zip"

Write-Host "Packaging ER Swapper v$clean"

if (Test-Path (Join-Path $root 'artifacts')) {
    Remove-Item (Join-Path $root 'artifacts') -Recurse -Force
}

New-Item -ItemType Directory -Path $staging -Force | Out-Null

& dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:Version=$clean `
    -p:AssemblyVersion="$clean.0" `
    -p:FileVersion="$clean.0" `
    -p:PublishSingleFile=false `
    -o $staging

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Get-ChildItem $staging -Include *.pdb, *.xml -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

$exe = Join-Path $staging 'ERSwapper.exe'
if (-not (Test-Path $exe)) { throw "ERSwapper.exe missing from the publish output." }

$presets = Join-Path $staging 'Config\presets.json'
if (-not (Test-Path $presets)) { throw "Config\presets.json missing - the catalogue would ship empty." }

$allowedConfigFiles = @('presets.json', 'bundles.json', 'unsupported.json', 'offset_cache.json', 'release.json')
$allowedConfigDirs = @('Signatures', 'Thumbnails')

$stagedConfig = Join-Path $staging 'Config'

foreach ($entry in Get-ChildItem $stagedConfig -ErrorAction SilentlyContinue) {
    $allowed = if ($entry.PSIsContainer) { $allowedConfigDirs -contains $entry.Name } else { $allowedConfigFiles -contains $entry.Name }

    if (-not $allowed) {
        Write-Warning "Removing '$($entry.Name)' from Config - it is user data, not shipped content."
        Remove-Item $entry.FullName -Recurse -Force
    }
}

$bundles = Join-Path $staging 'Config\bundles.json'
if (-not (Test-Path $bundles)) { throw "Config\bundles.json missing - signatures could not be resolved." }

$installerVersion = 1
$minimumInstaller = 1

$manifest = [ordered]@{
    FormatVersion           = 1
    MinimumInstallerVersion = $minimumInstaller
    AppVersion              = $clean
    ExecutableName          = 'ERSwapper.exe'
    RequiredFiles           = @('presets.json', 'bundles.json')
}

$manifestPath = Join-Path $staging 'Config\release.json'
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8

if ($minimumInstaller -gt $installerVersion) {
    throw "minimumInstallerVersion ($minimumInstaller) is above this build's installer ($installerVersion)."
}

foreach ($required in $manifest.RequiredFiles) {
    if (-not (Test-Path (Join-Path $staging "Config\$required"))) {
        throw "release.json lists '$required' as required but it is not in Config."
    }
}

$itemCount = (Get-Content $presets -Raw | ConvertFrom-Json).Count
$signatures = @(Get-ChildItem (Join-Path $staging 'Config\Signatures') -Filter *.sig -ErrorAction SilentlyContinue).Count
$thumbnails = @(Get-ChildItem (Join-Path $staging 'Config\Thumbnails') -Filter *.png -ErrorAction SilentlyContinue).Count
$texconv = Test-Path (Join-Path $staging 'texconv.exe')

if ($thumbnails -eq 0) {
    throw "No thumbnails reached Config\Thumbnails - every install would rebuild all $itemCount previews on first run."
}

if ($thumbnails -lt $itemCount) {
    Write-Warning "Only $thumbnails of $itemCount items have a shipped preview; the rest build on first run."
}

if ($thumbnails -gt $itemCount) {
    throw ("Config has $thumbnails thumbnails but presets.json only has $itemCount items. " +
           "Publish writes both together, so the catalogue is stale - it was probably published to a " +
           "build output folder instead of the ERSwapper project's Config folder. " +
           "Re-publish from the dev build before releasing.")
}

if ($signatures -eq 0) {
    throw "No signatures reached Config\Signatures - no bundle could be located."
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal

$sizeMb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash

Write-Host ""
Write-Host "  items       : $itemCount"
Write-Host "  signatures  : $signatures"
Write-Host "  thumbnails  : $thumbnails"
Write-Host "  texconv.exe : $(if ($texconv) { 'included' } else { 'MISSING' })"
Write-Host ""
Write-Host "  zip    : $zip"
Write-Host "  installer format : $($manifest.FormatVersion) (needs installer >= $minimumInstaller)"
Write-Host "  size   : $sizeMb MB"
Write-Host "  sha256 : $hash"
Write-Host ""
Write-Host "Next: create a GitHub release tagged v$clean and attach that zip."
Write-Host "The asset name must start with 'ERSwapper' and end with '.zip'."
