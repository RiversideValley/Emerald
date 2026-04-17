# Windows MSIX Signing

Emerald Windows packaged publish now targets signed MSIX output.

## Why signing is required

- MSIX signing proves package integrity and signer identity.
- The package manifest `Publisher` value must match the signing certificate `Subject` exactly.
- The private key (`.pfx`) is sensitive and must never be committed to source control.

## Local signed publish

From the repository root on Windows:

```powershell
$certPassword = Read-Host -AsSecureString "PFX password"

pwsh ./scripts/windows/publish-windows-msix.ps1 `
  -CertificatePath "PATH\to\EmeraldSigning.pfx" `
  -CertificatePassword $certPassword
```

Output archive:

- `artifacts/windows/final/Emerald-Windows-Signed-x64-arm64.zip`

## Trust and install

For sideload install on Windows, import the public cert first:

```powershell
Import-Certificate -FilePath "C:\secrets\EmeraldSigning.cer" -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"
```

Then install the `.msixbundle`.

## CI troubleshooting (APPX0105 / APPX0107)

If CI fails with certificate import/signing errors:

- Ensure the PFX actually contains a private key.
- Ensure certificate `Subject` exactly matches manifest publisher (`CN=Riverside Valley`).
- Ensure certificate includes Code Signing EKU (`1.3.6.1.5.5.7.3.3`).
- Recreate GitHub secrets after exporting a fresh PFX:
  - `WINDOWS_SIGNING_CERT_BASE64`
  - `WINDOWS_SIGNING_CERT_PASSWORD`

`mspdbcmf.exe` warning during packaging is non-blocking and does not cause signing failure.

In CI, verification now trusts the imported cert in the current user root store temporarily, then removes it during cleanup.

To reduce CI runtime, workflow packaging runs with `-SkipBundleVerify` and relies on signing command success.
