# Publishes MouseToPad, compiles the Inno Setup installer, then launches it.
# Used by the "Build installer (Inno Setup)" launch profile in Visual Studio;
# also fine to run by hand. Pass -NoLaunch to build without starting setup.
param([switch]$NoLaunch)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup not found. Install it with:  winget install -e --id JRSoftware.InnoSetup --scope user"
}

Write-Host "==> dotnet publish (Release, win-x64)" -ForegroundColor Cyan
dotnet publish (Join-Path $root 'MouseToPad.csproj') -c Release -r win-x64 --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Compiling installer.iss" -ForegroundColor Cyan
& $iscc (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setup = Join-Path $root 'installer\MouseToPadSetup.exe'
Write-Host "==> Built: $setup" -ForegroundColor Green

if (-not $NoLaunch) {
    Write-Host "==> Launching installer..." -ForegroundColor Cyan
    Start-Process $setup
}
