# Windows MSIX Signing

Emerald Windows packaged publish now targets signed MSIX output.

## Why signing is required

- MSIX signing proves package integrity and signer identity.
- The package manifest `Publisher` value must match the signing certificate `Subject` exactly.
- The private key (`.pfx`) is sensitive and must never be committed to source control.

## Create a signing certificate (self-signed)

Run in an elevated PowerShell prompt on Windows:

```powershell
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=Riverside Valley" `
  -KeyUsage DigitalSignature `
  -FriendlyName "Emerald MSIX Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

$password = Read-Host -AsSecureString "PFX password"
Export-PfxCertificate -Cert $cert -FilePath "C:\secrets\EmeraldSigning.pfx" -Password $password | Out-Null
Export-Certificate -Cert $cert -FilePath "C:\secrets\EmeraldSigning.cer" | Out-Null
```

Keep `EmeraldSigning.pfx` private. You may distribute `EmeraldSigning.cer` to trust the signer.

## Local signed publish

From the repository root on Windows:

```powershell
pwsh ./scripts/windows/publish-windows-msix.ps1 `
  -CertificatePath "C:\secrets\EmeraldSigning.pfx" `
  -CertificatePassword "<pfx-password>"
```

Output archive:

- `artifacts/windows/final/Emerald-Windows-Signed-x64-arm64.zip`

## GitHub Actions secrets

Set these repository secrets:

- `WINDOWS_SIGNING_CERT_BASE64`: Base64 content of `EmeraldSigning.pfx`
- `WINDOWS_SIGNING_CERT_PASSWORD`: PFX password

Create Base64:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\secrets\EmeraldSigning.pfx"))
```

## Trust and install

For sideload install on Windows, import the public cert first:

```powershell
Import-Certificate -FilePath "C:\secrets\EmeraldSigning.cer" -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"
```

Then install the `.msixbundle`.
