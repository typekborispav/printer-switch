<#
.SYNOPSIS
    Builds, signs and publishes a Printer Switch release from this PC.

.DESCRIPTION
    1. checks that the work tree is clean, on main and that the version in the
       project and the installer script matches -Version
    2. builds the installer (build.ps1)
    3. signs it with the release key (tools\release-key.ps1) and checks the
       signature against the public key built into the app
    4. tags vX.Y.Z, pushes, and creates the GitHub release with the installer
       and its .sig

    The build happens here, not on GitHub, so a compromised GitHub account or
    build cannot get a signature for code you have not built yourself.

.EXAMPLE
    .\tools\release.ps1 -Version 1.1.2 -NotesFile notes.md
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$NotesFile,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-common.ps1')
$root = Split-Path $PSScriptRoot
Push-Location $root
try {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "version must look like 1.2.3" }
    $tag = "v$Version"

    # ------------------------------------------------------------ checks
    if (git status --porcelain) { throw "uncommitted changes; commit them first" }
    if ((git rev-parse --abbrev-ref HEAD) -ne 'main') { throw "not on main" }
    if (git tag --list $tag) { throw "tag $tag already exists" }
    $proj = Get-Content src\PrinterSwitch\PrinterSwitch.csproj -Raw
    if ($proj -notmatch "<Version>$([regex]::Escape($Version))</Version>") { throw "PrinterSwitch.csproj has a different <Version>" }
    $iss = Get-Content installer\PrinterSwitch.iss -Raw
    if ($iss -notmatch "#define AppVersion `"$([regex]::Escape($Version))`"") { throw "PrinterSwitch.iss has a different AppVersion" }
    $updater = Get-Content src\PrinterSwitch\Core\Updater.cs -Raw
    if ($updater -notmatch 'SigningKey\s*=\s*"([A-Za-z0-9+/=]+)"') { throw "SigningKey not found in Updater.cs" }
    $appKey = $Matches[1]
    $key = Get-ReleaseKey
    if ([Convert]::ToBase64String($key.ExportSubjectPublicKeyInfo()) -ne $appKey) {
        throw "the release key does not match SigningKey in Updater.cs"
    }

    # ------------------------------------------------------------- build
    $repoUrl = (gh repo view --json url --jq .url)
    & .\build.ps1 -Version $Version -RepoUrl $repoUrl
    $exe = Get-Item "dist\PrinterSwitch-Setup-$Version.exe"

    # -------------------------------------------------------------- sign
    $sha = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $text = [Text.Encoding]::UTF8.GetBytes((Get-SignedText $Version $exe.Name $sha))
    $sig = $key.SignData($text, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
    $check = [System.Security.Cryptography.ECDsa]::Create()
    $n = 0
    $check.ImportSubjectPublicKeyInfo([Convert]::FromBase64String($appKey), [ref]$n)
    if (-not $check.VerifyData($text, $sig, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)) {
        throw "signature does not verify"
    }
    $sigFile = "$($exe.FullName).sig"
    Set-Content -LiteralPath $sigFile -Value ([Convert]::ToBase64String($sig)) -Encoding ascii -NoNewline
    "signed $($exe.Name)  sha256 $sha"

    if ($DryRun) { "dry run: not tagging or publishing"; return }

    # ----------------------------------------------------------- publish
    git tag -a $tag -m "Printer Switch $Version"
    git push origin main $tag
    $notes = if ($NotesFile) { @('--notes-file', $NotesFile) } else { @('--generate-notes') }
    gh release create $tag $exe.FullName $sigFile --title "Printer Switch $Version" @notes
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
    "published $tag"
}
finally {
    Pop-Location
}
