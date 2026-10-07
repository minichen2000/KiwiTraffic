#Requires -Version 7.0
<#
.SYNOPSIS
    Builds KiwiTraffic into a self-contained, single-file, green EXE.

.DESCRIPTION
    fast    Quick publish for daily verification. No tests, no ReadyToRun.
            -> dist/fast/KiwiTraffic.exe
    release Full quality gates (warnings as errors + tests) and a ReadyToRun
            self-contained single-file publish.
            -> dist/release/KiwiTraffic.exe

    No installer is produced, ever. The deliverable is always a single EXE.

.PARAMETER Configuration
    fast or release. Defaults to fast.

.PARAMETER Clean
    Remove bin/ and obj/ of every project plus the whole dist/ tree first.

.EXAMPLE
    pwsh -File scripts/build.ps1 -Configuration fast
#>
[CmdletBinding()]
param(
    [ValidateSet('fast', 'release')]
    [string]$Configuration = 'fast',

    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot 'src/KiwiTraffic.App/KiwiTraffic.App.csproj'
$propsPath  = Join-Path $repoRoot 'Directory.Build.props'
$distRoot   = Join-Path $repoRoot 'dist'
$distDir    = Join-Path $distRoot $Configuration

$solution = (Get-ChildItem -Path $repoRoot -Filter 'KiwiTraffic.sln*' -File |
             Select-Object -First 1 -ExpandProperty FullName)
if (-not $solution) { throw "No solution file found in $repoRoot" }
if (-not (Test-Path $appProject)) { throw "App project not found: $appProject" }

function Write-Step  { param([string]$Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note  { param([string]$Text) Write-Host "    $Text" -ForegroundColor DarkGray }

function Invoke-DotNet {
    param([string[]]$Arguments)
    Write-Note "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE"
    }
}

# --- version (single source of truth: Directory.Build.props) -------------------
# XPath rather than property access: with Set-StrictMode, walking
# .Project.PropertyGroup.Version throws on the PropertyGroups that have no
# <Version> child.
if (-not (Test-Path $propsPath)) { throw "Directory.Build.props not found: $propsPath" }
$propsXml = [xml](Get-Content -Raw -Path $propsPath)
$versionNode = $propsXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode) { throw "No <Version> element found in $propsPath" }
$version = $versionNode.InnerText.Trim()

Write-Step "KiwiTraffic $version - $Configuration build"

if ($Clean) {
    Write-Step 'Clean'
    Get-ChildItem -Path $repoRoot -Include 'bin', 'obj' -Directory -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/]node_modules[\\/]' } |
        Remove-Item -Recurse -Force
    if (Test-Path $distRoot) { Remove-Item -Recurse -Force $distRoot }
}

# --- restore ------------------------------------------------------------------
Write-Step 'Restore'
Invoke-DotNet @('restore', $solution, '--nologo')

if ($Configuration -eq 'release') {
    # Warnings as errors is enforced through Directory.Build.props for the
    # Release configuration, so a plain build is the quality gate here.
    Write-Step 'Build (warnings as errors)'
    Invoke-DotNet @('build', $solution, '-c', 'Release', '--no-restore', '--nologo')

    Write-Step 'Test'
    Invoke-DotNet @('test', $solution, '-c', 'Release', '--no-build', '--nologo')
}

# --- publish ------------------------------------------------------------------
# SelfContained / PublishSingleFile / RuntimeIdentifier live in the App csproj.
# Only the expensive, quality-gated knobs are switched here.
$publishArgs = @(
    'publish', $appProject,
    '-c', 'Release',
    '--nologo',
    '-o', $distDir
)
if ($Configuration -eq 'release') {
    $publishArgs += '-p:PublishReadyToRun=true'
}
else {
    $publishArgs += '-p:PublishReadyToRun=false'
}

Write-Step "Publish ($Configuration)"
Invoke-DotNet $publishArgs

# --- verify artifact ----------------------------------------------------------
Write-Step 'Verify artifact'
$exe = Join-Path $distDir 'KiwiTraffic.exe'
if (-not (Test-Path $exe)) { throw "Expected artifact not found: $exe" }

$exeInfo = Get-Item $exe
$sizeMb  = [math]::Round($exeInfo.Length / 1MB, 1)
$hash    = (Get-FileHash -Path $exe -Algorithm SHA256).Hash

# Debug symbols are useful but are not part of the deliverable: keep them in a
# subfolder so that dist/<config>/ itself holds exactly one user-facing file.
$symbols = Get-ChildItem -Path $distDir -Filter '*.pdb' -File
if ($symbols) {
    $symbolDir = Join-Path $distDir 'symbols'
    New-Item -ItemType Directory -Path $symbolDir -Force | Out-Null
    $symbols | Move-Item -Destination $symbolDir -Force
}

$extra = Get-ChildItem -Path $distDir -File | Where-Object { $_.Name -ne 'KiwiTraffic.exe' }
if ($extra) {
    Write-Warning ("Extra files in the output directory (not part of the " +
                   "distributable single EXE): " + (($extra | ForEach-Object Name) -join ', '))
}

Write-Host ''
Write-Host "Artifact : $exe"      -ForegroundColor Green
Write-Host "Version  : $version"  -ForegroundColor Green
Write-Host "Size     : $sizeMb MB" -ForegroundColor Green
Write-Host "SHA-256  : $hash"     -ForegroundColor Green
