<#
.SYNOPSIS
    Builds Printer Switch and its installer.

.DESCRIPTION
    1. dotnet publish (self-contained, win-x64) into .\publish
    2. Inno Setup compiles .\installer\PrinterSwitch.iss into .\dist

    Needs the .NET 10 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).

.EXAMPLE
    .\build.ps1 -Version 1.0.0
#>
param(
    [string]$Version = "1.0.0",
    [string]$RepoUrl = "https://github.com/",
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publish = Join-Path $root 'publish'

# First dotnet that actually has an SDK (a runtime-only install is not enough).
$dotnet = @(
    "$env:LOCALAPPDATA\dotnet-sdk\dotnet.exe",
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
) | Where-Object { $_ -and (Test-Path $_) -and (& $_ --list-sdks) } | Select-Object -First 1
if (-not $dotnet) { throw ".NET 10 SDK not found (https://dotnet.microsoft.com/download)" }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $dotnet publish (Join-Path $root 'src\PrinterSwitch\PrinterSwitch.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=none -p:GenerateDocumentationFile=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

if ($SkipInstaller) { return }

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found (winget install JRSoftware.InnoSetup)" }

& $iscc "/DAppVersion=$Version" "/DRepoUrl=$RepoUrl" (Join-Path $root 'installer\PrinterSwitch.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Get-ChildItem (Join-Path $root 'dist') -Filter "*$Version*.exe" | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "{0}  {1:N1} MB  sha256 {2}" -f $_.Name, ($_.Length / 1MB), $hash
}
