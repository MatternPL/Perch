<#
.SYNOPSIS
    Publishes Perch and wraps it in a per-user installer.

.DESCRIPTION
    Produces two things in dist\:
      PerchSetup-<version>.exe  the installer (self-contained, no .NET needed)
      Perch.exe                 a portable single file, for people who prefer that

.EXAMPLE
    pwsh installer\build-installer.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\Perch\Perch.csproj"
$publish = Join-Path $root "publish"
$dist = Join-Path $root "dist"

if (-not $Version) {
    $csproj = [xml](Get-Content $project)
    $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
}
Write-Host "Building Perch $Version" -ForegroundColor Cyan

foreach ($dir in @($publish, $dist)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

# Self-contained but not single-file: the installer compresses a folder of
# assemblies far better than one packed executable, and the app starts faster.
Write-Host "-> dotnet publish (folder)"
dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Trim the things an end user has no use for.
Get-ChildItem $publish -Include *.pdb, *.xml -Recurse | Remove-Item -Force

Write-Host "-> ISCC"
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup"
}

& $iscc "/DAppVersion=$Version" (Join-Path $PSScriptRoot "Perch.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

# The portable build, for people who would rather not run an installer.
Write-Host "-> dotnet publish (portable single file)"
$portable = Join-Path $root "publish-portable"
if (Test-Path $portable) { Remove-Item $portable -Recurse -Force }

dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:Version=$Version -o $portable
if ($LASTEXITCODE -ne 0) { throw "portable publish failed" }

Copy-Item (Join-Path $portable "Perch.exe") (Join-Path $dist "Perch.exe") -Force

Write-Host ""
Get-ChildItem $dist | ForEach-Object {
    "{0,-28} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)
}
