# updateVersionNetcore.ps1
# Updates netcore/DoorApp.Familab/src/AssemblyInfo.cs with version information.
#
# Version source (in priority order):
#   1. -Version parameter:    .\updateVersionNetcore.ps1 -Version 1.2.3-beta
#   2. A repo-root version-*.txt file (produced by the GitVersion CI step)
#   3. Fallback "0.0.1-dev"
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$assemblyInfo = Join-Path $scriptDir "..\src\AssemblyInfo.cs"

$majorMinorPatch = ""

if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionFile = Get-ChildItem "version-*.txt" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $versionFile) {
        Write-Host "Reading version from $($versionFile.Name)"
        $content = Get-Content $versionFile.FullName
        $Version = ($content | Where-Object { $_ -match "^version=" }) -replace "^version=", ""
        $majorMinorPatch = ($content | Where-Object { $_ -match "^major_minor_patch=" }) -replace "^major_minor_patch=", ""
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "0.0.1-dev"
}

if ([string]::IsNullOrWhiteSpace($majorMinorPatch)) {
    $majorMinorPatch = ($Version -replace "[-+].*$", "")
}

if ($majorMinorPatch -notmatch "^\d+\.\d+\.\d+$") {
    $majorMinorPatch = "0.0.1"
}

Write-Host "Informational version: $Version"
Write-Host "Assembly version:      $majorMinorPatch.0"
Write-Host "Updating $assemblyInfo"

$body = @"
// -----------------------------------------------------------------------------
// AssemblyInfo.cs
//
// This file holds the embedded version of the application. It is updated
// automatically by the CI pipeline (see .github/workflows/deploynetcore.yml).
//
// The values here are read at runtime by AssemblyVersionProvider.
// -----------------------------------------------------------------------------
using System.Reflection;

[assembly: AssemblyVersion("$majorMinorPatch.0")]
[assembly: AssemblyFileVersion("$majorMinorPatch.0")]
[assembly: AssemblyInformationalVersion("$Version")]
"@

Set-Content -Path $assemblyInfo -Value $body -NoNewline
Write-Host "AssemblyInfo.cs updated to $Version"
