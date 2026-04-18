param(
    [string]$ProjectPath = ".\Emerald\Emerald.csproj",
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net10.0-windows10.0.26100",
    [string[]]$Platforms = @("x64", "arm64"),
    [string]$OutputRoot = ".\artifacts\windows",
    [string]$Version = "",
    [string]$FileVersion = "",
    [string]$AssemblyVersion = "",
    [string]$InformationalVersion = "",
    [string]$UpdateChannel = "",
    [string]$PublicVersion = "",
    [string]$ReleaseTag = "",
    [string]$CommitSha = "",
    [string]$BuildTimestampUtc = "",
    [switch]$SkipBundleVerify,
    [switch]$SkipBundleArchive,
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,
    [SecureString]$CertificatePassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$importedCert = $null
$importedThumbprint = $null
$hadMyCertBeforeImport = $false
$addedToRootStore = $false

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file was not found: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $CertificatePath)) {
    throw "Certificate file was not found: $CertificatePath"
}

$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$certificateFullPath = (Resolve-Path -LiteralPath $CertificatePath).Path
$outputRootFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$packagesRoot = Join-Path $outputRootFullPath "packages"
$bundleInput = Join-Path $outputRootFullPath "bundle-input"
$bundleOutput = Join-Path $outputRootFullPath "final"

if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "0.1.0.1" }
if ([string]::IsNullOrWhiteSpace($FileVersion)) { $FileVersion = $Version }
if ([string]::IsNullOrWhiteSpace($AssemblyVersion)) { $AssemblyVersion = $Version }
if ([string]::IsNullOrWhiteSpace($InformationalVersion)) { $InformationalVersion = $Version }
if ([string]::IsNullOrWhiteSpace($UpdateChannel)) { $UpdateChannel = "nightly" }
if ([string]::IsNullOrWhiteSpace($PublicVersion)) { $PublicVersion = "nightly-local" }
if ([string]::IsNullOrWhiteSpace($ReleaseTag)) { $ReleaseTag = "nightly-local" }
if ([string]::IsNullOrWhiteSpace($CommitSha)) { $CommitSha = "local" }
if ([string]::IsNullOrWhiteSpace($BuildTimestampUtc)) { $BuildTimestampUtc = [DateTime]::UtcNow.ToString("o") }

New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $bundleInput -Force | Out-Null
New-Item -ItemType Directory -Path $bundleOutput -Force | Out-Null

function Write-Step([string]$Message) {
    Write-Host "[$([DateTime]::UtcNow.ToString('u'))] $Message"
}

function Convert-PlainTextToSecureString([string]$PlainText) {
    $secure = New-Object System.Security.SecureString
    foreach ($ch in $PlainText.ToCharArray()) {
        $secure.AppendChar($ch)
    }

    $secure.MakeReadOnly()
    return $secure
}

try {
    if (-not $CertificatePassword) {
        $passwordFromEnv = $env:WINDOWS_SIGNING_CERT_PASSWORD
        if ([string]::IsNullOrWhiteSpace($passwordFromEnv)) {
            throw "Certificate password was not provided. Pass -CertificatePassword or set WINDOWS_SIGNING_CERT_PASSWORD."
        }

        $CertificatePassword = Convert-PlainTextToSecureString -PlainText $passwordFromEnv
        $passwordFromEnv = $null
    }

    Write-Step "Loading certificate metadata..."
    $pfxProbe = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificateFullPath, $CertificatePassword)
    try {
        $hadMyCertBeforeImport = Test-Path -LiteralPath "Cert:\CurrentUser\My\$($pfxProbe.Thumbprint)"
    }
    finally {
        $pfxProbe.Dispose()
    }

    Write-Step "Importing signing certificate into CurrentUser\\My..."
    $importedCerts = Import-PfxCertificate `
        -FilePath $certificateFullPath `
        -Password $CertificatePassword `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -Exportable

    if (-not $importedCerts) {
        throw "Certificate import returned no certificate object."
    }

    if ($importedCerts -is [array]) {
        $importedCert = $importedCerts | Where-Object { $_.HasPrivateKey } | Select-Object -First 1
    }
    else {
        $importedCert = $importedCerts
    }

    if (-not $importedCert.HasPrivateKey) {
        throw "Imported certificate does not include a private key."
    }

    $hasCodeSigningEku = $false
    foreach ($extension in $importedCert.Extensions) {
        if ($extension -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
            foreach ($eku in $extension.EnhancedKeyUsages) {
                if ($eku.Value -eq "1.3.6.1.5.5.7.3.3") {
                    $hasCodeSigningEku = $true
                    break
                }
            }
        }

        if ($hasCodeSigningEku) {
            break
        }
    }

    if (-not $hasCodeSigningEku) {
        throw "Imported certificate does not have the Code Signing EKU (OID 1.3.6.1.5.5.7.3.3)."
    }

    $manifestPath = Join-Path (Split-Path -Parent $projectFullPath) "Package.appxmanifest"
    if (Test-Path -LiteralPath $manifestPath) {
        [xml]$manifestXml = Get-Content -LiteralPath $manifestPath
        $manifestPublisher = $manifestXml.Package.Identity.Publisher
        if (-not [string]::IsNullOrWhiteSpace($manifestPublisher) -and $importedCert.Subject -ne $manifestPublisher) {
            throw "Certificate subject '$($importedCert.Subject)' does not match manifest publisher '$manifestPublisher'."
        }
    }

    $importedThumbprint = $importedCert.Thumbprint

    if (-not $SkipBundleVerify) {
        Write-Step "Ensuring certificate is trusted in CurrentUser\\Root for bundle verification..."
        $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new("Root", "CurrentUser")
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        try {
            $alreadyInRoot = $false
            foreach ($cert in $rootStore.Certificates) {
                if ($cert.Thumbprint -eq $importedThumbprint) {
                    $alreadyInRoot = $true
                    break
                }
            }

            if (-not $alreadyInRoot) {
                $rootStore.Add($importedCert)
                $addedToRootStore = $true
            }
        }
        finally {
            $rootStore.Close()
        }
    }

    Write-Step "Restoring project dependencies..."
    & dotnet msbuild $projectFullPath `
        /r /t:Restore `
        "/p:Configuration=$Configuration" `
        /p:PublishReadyToRun=false `
        /m `
        /nologo /v:minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE."
    }

    $msixFiles = @()
    foreach ($platform in $Platforms) {
        $appxDir = Join-Path $packagesRoot "$platform\"
        New-Item -ItemType Directory -Path $appxDir -Force | Out-Null

        Write-Step "Publishing signed MSIX for platform '$platform'..."
        & dotnet msbuild $projectFullPath `
            "/p:TargetFramework=$TargetFramework" `
            "/p:Configuration=$Configuration" `
            "/p:Platform=$platform" `
            /p:Restore=false `
            /p:PublishReadyToRun=false `
            /p:PublishSignedPackage=true `
            /p:GenerateTestArtifacts=false `
            /p:AppxSymbolPackageEnabled=false `
            "/p:Version=$Version" `
            "/p:FileVersion=$FileVersion" `
            "/p:AssemblyVersion=$AssemblyVersion" `
            "/p:InformationalVersion=$InformationalVersion" `
            "/p:EmeraldPackageVersion=$Version" `
            "/p:EmeraldPublicVersion=$PublicVersion" `
            "/p:EmeraldUpdateChannel=$UpdateChannel" `
            "/p:EmeraldReleaseTag=$ReleaseTag" `
            "/p:EmeraldCommitSha=$CommitSha" `
            "/p:EmeraldBuildTimestampUtc=$BuildTimestampUtc" `
            "/p:AppxPackageDir=$appxDir" `
            "/p:PackageCertificateThumbprint=$importedThumbprint" `
            /p:PackageCertificateKeyFile= `
            /p:PackageCertificatePassword= `
            /m `
            /nologo /v:minimal

        if ($LASTEXITCODE -ne 0) {
            throw "MSIX publish failed for platform '$platform' with exit code $LASTEXITCODE."
        }

        $package = Get-ChildItem $appxDir -Recurse -Filter *.msix -File `
            | Where-Object { $_.Name -match "_$platform\.msix$" } `
            | Sort-Object LastWriteTime -Descending `
            | Select-Object -First 1

        if (-not $package) {
            $package = Get-ChildItem $appxDir -Recurse -Filter *.msix -File `
                | Sort-Object LastWriteTime -Descending `
                | Select-Object -First 1
        }

        if (-not $package) {
            throw "No .msix package found for platform '$platform' in $appxDir."
        }

        $msixFiles += $package
    }

    $msixFiles | ForEach-Object {
        Copy-Item $_.FullName -Destination (Join-Path $bundleInput $_.Name) -Force
        Copy-Item $_.FullName -Destination (Join-Path $bundleOutput $_.Name) -Force
    }

    $makeAppx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" `
        | Sort-Object FullName -Descending `
        | Select-Object -First 1 -ExpandProperty FullName

    if (-not $makeAppx) {
        throw "makeappx.exe not found."
    }

    $signTool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" `
        | Sort-Object FullName -Descending `
        | Select-Object -First 1 -ExpandProperty FullName

    if (-not $signTool) {
        throw "signtool.exe not found."
    }

    $platformSlug = ($Platforms -join "-")
    $bundlePath = Join-Path $bundleOutput "Emerald-Windows-Signed-$platformSlug.msixbundle"
    $appxBundlePath = Join-Path $bundleOutput "Emerald-Windows-Signed-$platformSlug.appxbundle"
    & $makeAppx bundle /o /d $bundleInput /p $bundlePath /bv $Version

    if ($LASTEXITCODE -ne 0) {
        throw "makeappx bundle command failed with exit code $LASTEXITCODE."
    }

    Write-Step "Signing MSIX bundle..."
    & $signTool sign /fd SHA256 /sha1 $importedThumbprint /s My $bundlePath

    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign command failed with exit code $LASTEXITCODE."
    }

    if ($SkipBundleVerify) {
        Write-Step "Skipping bundle verification (SkipBundleVerify was set)."
    }
    else {
        Write-Step "Verifying MSIX bundle signature..."
        & $signTool verify /pa /v $bundlePath

        if ($LASTEXITCODE -ne 0) {
            throw "signtool verify command failed with exit code $LASTEXITCODE."
        }
    }

    $zipPath = Join-Path $bundleOutput "Emerald-Windows-Signed-$platformSlug.zip"
    if ($SkipBundleArchive) {
        Write-Step "Skipping bundle archive creation (SkipBundleArchive was set)."
    }
    else {
        Compress-Archive -Path $bundlePath -DestinationPath $zipPath -Force
        Write-Step "Signed archive created: $zipPath"
    }

    Write-Step "Signed bundle created: $bundlePath"
    Copy-Item -LiteralPath $bundlePath -Destination $appxBundlePath -Force
    Write-Step "Appx-compatible bundle copy created: $appxBundlePath"

    if ($env:GITHUB_OUTPUT) {
        Add-Content -Path $env:GITHUB_OUTPUT -Value "windows_bundle_path=$bundlePath"
        Add-Content -Path $env:GITHUB_OUTPUT -Value "windows_appxbundle_path=$appxBundlePath"
        if (-not $SkipBundleArchive) {
            Add-Content -Path $env:GITHUB_OUTPUT -Value "windows_zip_path=$zipPath"
        }
    }
}
finally {
    if ($addedToRootStore -and -not [string]::IsNullOrWhiteSpace($importedThumbprint)) {
        $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new("Root", "CurrentUser")
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        try {
            $rootMatches = @($rootStore.Certificates | Where-Object { $_.Thumbprint -eq $importedThumbprint })
            foreach ($rootMatch in $rootMatches) {
                $rootStore.Remove($rootMatch)
            }
        }
        finally {
            $rootStore.Close()
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($importedThumbprint)) {
        $certPathInStore = "Cert:\CurrentUser\My\$importedThumbprint"
        if ((-not $hadMyCertBeforeImport) -and (Test-Path -LiteralPath $certPathInStore)) {
            Remove-Item -LiteralPath $certPathInStore -Force
        }
    }
}
