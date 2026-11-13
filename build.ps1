# Builds App\Reclaim.exe and, when Inno Setup 6 is installed, the installer
# dist\Reclaim-Setup-<version>.exe.
#
# The exe is self-contained: it runs on any Windows 10/11 x64 PC without
# installing .NET. The version comes from <Version> in Reclaim.csproj.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = [regex]::Match((Get-Content -Raw Reclaim.csproj), '<Version>([^<]+)</Version>').Groups[1].Value
Write-Host "Building Reclaim $version" -ForegroundColor Cyan

dotnet publish Reclaim.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -o App
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Write-Host "`n  Built  App\Reclaim.exe" -ForegroundColor Green

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host '  Inno Setup 6 is not installed, so only App\Reclaim.exe was built.' -ForegroundColor Yellow
    Write-Host '  Install it from https://jrsoftware.org/isdl.php, then run this again.' -ForegroundColor Yellow
    return
}

$log = & $iscc "/DAppVersion=$version" 'installer\Reclaim.iss' 2>&1
if ($LASTEXITCODE -ne 0) { $log | Write-Host; throw 'Inno Setup failed.' }
Write-Host "  Built  dist\Reclaim-Setup-$version.exe" -ForegroundColor Green
