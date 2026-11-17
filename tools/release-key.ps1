<#
.SYNOPSIS
    Manages the key that signs Printer Switch releases.

.DESCRIPTION
    The app installs an update only when it carries a signature made with this
    key, so whoever takes over the GitHub account or its build cannot push a
    fake version to users. The private key never leaves this PC: it is stored
    encrypted with Windows DPAPI (usable only by this Windows account).

      -New               create the key (refuses to overwrite one)
      -Public            print the public key (goes into Core/Updater.cs)
      -Backup <file>     export the key encrypted with a password you type
      -Restore <file>    import such a backup on a new PC

    Losing the key means installed copies can no longer update themselves;
    users would have to download the next version by hand. Keep a backup.

    Needs PowerShell 7.
#>
param(
    [switch]$New,
    [switch]$Public,
    [string]$Backup,
    [string]$Restore
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-common.ps1')

if ($New) {
    if (Test-Path $KeyFile) { throw "a key already exists: $KeyFile" }
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
    Save-ReleaseKey $ecdsa.ExportPkcs8PrivateKey()
    "created $KeyFile"
    "public key: " + [Convert]::ToBase64String($ecdsa.ExportSubjectPublicKeyInfo())
}
elseif ($Public) {
    [Convert]::ToBase64String((Get-ReleaseKey).ExportSubjectPublicKeyInfo())
}
elseif ($Backup) {
    $pw = Read-Host "Password for the backup" -AsSecureString
    $pw2 = Read-Host "Again" -AsSecureString
    $a = [System.Net.NetworkCredential]::new('', $pw).Password
    if ($a -ne [System.Net.NetworkCredential]::new('', $pw2).Password) { throw "passwords differ" }
    if ($a.Length -lt 12) { throw "use at least 12 characters" }
    $pbe = [System.Security.Cryptography.PbeParameters]::new(
        [System.Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256, 600000)
    $pem = (Get-ReleaseKey).ExportEncryptedPkcs8PrivateKeyPem($a, $pbe)
    Set-Content -LiteralPath $Backup -Value $pem -Encoding ascii
    "backup written to $Backup"
}
elseif ($Restore) {
    if (Test-Path $KeyFile) { throw "a key already exists: $KeyFile" }
    $pw = Read-Host "Backup password" -AsSecureString
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
    $ecdsa.ImportFromEncryptedPem((Get-Content -LiteralPath $Restore -Raw),
        [System.Net.NetworkCredential]::new('', $pw).Password)
    Save-ReleaseKey $ecdsa.ExportPkcs8PrivateKey()
    "restored to $KeyFile"
}
else {
    Get-Help $PSCommandPath -Detailed
}
