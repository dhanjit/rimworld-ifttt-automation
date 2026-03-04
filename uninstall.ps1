#Requires -Version 5.0
<#
.SYNOPSIS
    Removes the RimWorld Automation (IFTTT Framework) mod from RimWorld.

.PARAMETER RimWorldPath
    Path to your RimWorld installation folder.
    Default: C:\Program Files (x86)\Steam\steamapps\common\RimWorld

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    .\uninstall.ps1
    .\uninstall.ps1 -RimWorldPath "D:\Games\RimWorld"
    .\uninstall.ps1 -Force
#>
param(
    [string]$RimWorldPath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$ModName  = "RimWorldIFTTT"
$ModsDir  = Join-Path $RimWorldPath "Mods"
$TargetDir = Join-Path $ModsDir $ModName

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  RimWorld IFTTT Automation — Uninstaller " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Target : $TargetDir"
Write-Host ""

# ── Check it exists ───────────────────────────────────────────────────────────
if (-not (Test-Path $TargetDir)) {
    Write-Host "Mod folder not found at:" -ForegroundColor Yellow
    Write-Host "  $TargetDir"
    Write-Host ""
    Write-Host "Nothing to remove. Already uninstalled?" -ForegroundColor Yellow
    exit 0
}

# ── Show what will be deleted ─────────────────────────────────────────────────
Write-Host "Files that will be deleted:" -ForegroundColor Yellow
Get-ChildItem $TargetDir -Recurse -File | ForEach-Object {
    Write-Host "  $($_.FullName.Substring($TargetDir.Length + 1))"
}
Write-Host ""

# ── Confirm ───────────────────────────────────────────────────────────────────
if (-not $Force) {
    $confirm = Read-Host "Permanently delete '$TargetDir'? Type YES to confirm"
    if ($confirm -ne "YES") {
        Write-Host ""
        Write-Host "Aborted. Nothing was changed." -ForegroundColor Yellow
        exit 0
    }
}

# ── Remove ────────────────────────────────────────────────────────────────────
Remove-Item $TargetDir -Recurse -Force
Write-Host ""

if (Test-Path $TargetDir) {
    Write-Host "ERROR: Folder still exists after deletion. Check file locks." -ForegroundColor Red
    exit 1
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Uninstall complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Removed: $TargetDir" -ForegroundColor Green
Write-Host ""
Write-Host "Note: If you had an active save using this mod, load it once" -ForegroundColor Yellow
Write-Host "in RimWorld and save again to clear any leftover mod data." -ForegroundColor Yellow
Write-Host ""
