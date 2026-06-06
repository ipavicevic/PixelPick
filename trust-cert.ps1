param(
    [Parameter(Mandatory)][string]$MsixPath
)

$cert = (Get-AuthenticodeSignature $MsixPath).SignerCertificate
if ($null -eq $cert) {
    Write-Error "No certificate found in $MsixPath"
    exit 1
}

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "LocalMachine")
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()

Write-Host "Trusted: $($cert.Subject)"
Write-Host "You can now install $MsixPath"
