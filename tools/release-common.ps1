# Shared by release-key.ps1 and release.ps1.

Add-Type -AssemblyName System.Security

$KeyFile = Join-Path $env:APPDATA 'PrinterSwitch-release\signing-key.dpapi'
$KeyEntropy = [Text.Encoding]::UTF8.GetBytes('printer-switch release signing key')

function Save-ReleaseKey([byte[]]$pkcs8) {
    New-Item -ItemType Directory -Force (Split-Path $KeyFile) | Out-Null
    $blob = [System.Security.Cryptography.ProtectedData]::Protect($pkcs8, $KeyEntropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($KeyFile, $blob)
}

function Get-ReleaseKey {
    if (-not (Test-Path $KeyFile)) { throw "no signing key; create one with tools\release-key.ps1 -New" }
    $pkcs8 = [System.Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($KeyFile), $KeyEntropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
    $n = 0
    $ecdsa.ImportPkcs8PrivateKey($pkcs8, [ref]$n)
    return $ecdsa
}

# The exact text the app verifies (Core/Updater.cs, SignedText). Binding the
# version and the file name stops an old signed installer from being passed
# off as a newer one.
function Get-SignedText([string]$Version, [string]$FileName, [string]$Sha256) {
    "printer-switch release`nversion=$Version`nfile=$FileName`nsha256=$($Sha256.ToLowerInvariant())`n"
}
