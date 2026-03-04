#Requires -Version 5.0
<#
.SYNOPSIS
    Builds and installs the RimWorld Automation (IFTTT Framework) mod into RimWorld.

.PARAMETER RimWorldPath
    Path to your RimWorld installation folder.
    Default: C:\Program Files (x86)\Steam\steamapps\common\RimWorld

.PARAMETER NoBuild
    Skip the dotnet build step (use the last compiled DLL as-is).

.EXAMPLE
    .\install.ps1
    .\install.ps1 -RimWorldPath "D:\Games\RimWorld"
    .\install.ps1 -NoBuild
#>
param(
    [string]$RimWorldPath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$ModName    = "RimWorldIFTTT"
$ModsDir    = Join-Path $RimWorldPath "Mods"
$DestDir    = Join-Path $ModsDir $ModName
$SourceRoot = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RimWorld IFTTT Automation - Installer  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Source : $SourceRoot"
Write-Host "  Target : $DestDir"
Write-Host ""

# -- 1. Validate RimWorld path ------------------------------------------------
if (-not (Test-Path $RimWorldPath)) {
    Write-Host "ERROR: RimWorld not found at:" -ForegroundColor Red
    Write-Host "       $RimWorldPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run again with the correct path, e.g.:" -ForegroundColor Yellow
    Write-Host '  .\install.ps1 -RimWorldPath "D:\SteamLibrary\steamapps\common\RimWorld"'
    exit 1
}

if (-not (Test-Path $ModsDir)) {
    Write-Host "ERROR: Mods folder not found:" -ForegroundColor Red
    Write-Host "       $ModsDir" -ForegroundColor Red
    exit 1
}

# -- 2. Build -----------------------------------------------------------------
if (-not $NoBuild) {
    Write-Host "Step 1/3  Building mod..." -ForegroundColor Yellow
    $buildDir = Join-Path $SourceRoot "Source"
    Push-Location $buildDir
    try {
        & dotnet build RimworldAutomation.csproj -c Debug --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "ERROR: Build failed. Fix the errors above before installing." -ForegroundColor Red
            exit 1
        }
    }
    finally {
        Pop-Location
    }
    Write-Host "  Build succeeded." -ForegroundColor Green
} else {
    Write-Host "Step 1/3  Skipped build (-NoBuild)." -ForegroundColor DarkGray
}

# -- 3. Create destination folders --------------------------------------------
Write-Host ""
Write-Host "Step 2/3  Creating mod folder structure..." -ForegroundColor Yellow

$folders = @(
    (Join-Path $DestDir "About"),
    (Join-Path $DestDir "1.6\Assemblies"),
    (Join-Path $DestDir "1.6\Defs"),
    (Join-Path $DestDir "1.6\Defs\JobDefs")
)
foreach ($f in $folders) {
    New-Item -ItemType Directory -Force -Path $f | Out-Null
}
Write-Host "  Folders ready." -ForegroundColor Green

# -- 4. Copy files ------------------------------------------------------------
Write-Host ""
Write-Host "Step 3/3  Copying files..." -ForegroundColor Yellow

# -- Individual files (About + Assemblies) ------------------------------------
$filemap = @(
    [pscustomobject]@{ Rel = "About\About.xml";                   Dst = Join-Path $DestDir "About\About.xml"                   },
    [pscustomobject]@{ Rel = "1.6\Assemblies\RimWorldIFTTT.dll"; Dst = Join-Path $DestDir "1.6\Assemblies\RimWorldIFTTT.dll" },
    [pscustomobject]@{ Rel = "1.6\Assemblies\RimWorldIFTTT.pdb"; Dst = Join-Path $DestDir "1.6\Assemblies\RimWorldIFTTT.pdb" }
)

$copied  = 0
$missing = 0
foreach ($f in $filemap) {
    $src = Join-Path $SourceRoot $f.Rel
    if (Test-Path $src) {
        Copy-Item $src $f.Dst -Force
        Write-Host "  [OK] $($f.Rel)" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "  [??] $($f.Rel) -- not found, skipped" -ForegroundColor Yellow
        $missing++
    }
}

# -- Defs folder (recursive — picks up any new subfolders automatically) ------
$defsSource = Join-Path $SourceRoot "1.6\Defs"
$defsTarget = Join-Path $DestDir    "1.6\Defs"
if (Test-Path $defsSource) {
    Get-ChildItem -Path $defsSource -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($defsSource.Length + 1)
        $dst = Join-Path $defsTarget $rel
        $dstDir = Split-Path $dst -Parent
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }
        Copy-Item $_.FullName $dst -Force
        Write-Host "  [OK] 1.6\Defs\$rel" -ForegroundColor Green
        $copied++
    }
} else {
    Write-Host "  [??] 1.6\Defs -- not found, skipped" -ForegroundColor Yellow
}

# -- 5. Summary ---------------------------------------------------------------
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Install complete!  ($($copied) files copied)" -ForegroundColor Green
if ($missing -gt 0) {
    Write-Host "  ($($missing) optional files not found)" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installed to:"
Write-Host "  $DestDir" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Start RimWorld"
Write-Host "  2. Main Menu -> Mods"
Write-Host "  3. Enable RimWorld IFTTT Automation"
Write-Host "  4. Restart when prompted"
Write-Host "  5. In-game: look for the [IFTTT] button in the bottom tab bar"
Write-Host ""
