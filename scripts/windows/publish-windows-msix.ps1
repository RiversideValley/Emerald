param(
    [string]$ProjectPath = ".\Emerald\Emerald.csproj",
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net10.0-windows10.0.26100",
    [string[]]$Platforms = @("x64", "arm64"),
    [string]$OutputRoot = ".\artifacts\windows",
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,
    [string]$CertificatePassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
    $CertificatePassword = $env:WINDOWS_SIGNING_CERT_PASSWORD
}

if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
    throw "Certificate password was not provided. Pass -CertificatePassword or set WINDOWS_SIGNING_CERT_PASSWORD."
}

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

New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $bundleInput -Force | Out-Null
New-Item -ItemType Directory -Path $bundleOutput -Force | Out-Null

Write-Host "Restoring project dependencies..."
& dotnet msbuild $projectFullPath `
    /r /t:Restore `
    "/p:Configuration=$Configuration" `
    /p:PublishReadyToRun=false `
    /nologo /v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code $LASTEXITCODE."
}

$msixFiles = @()
foreach ($platform in $Platforms) {
    $appxDir = Join-Path $packagesRoot "$platform\"
    New-Item -ItemType Directory -Path $appxDir -Force | Out-Null

    Write-Host "Publishing signed MSIX for platform '$platform'..."
    & dotnet msbuild $projectFullPath `
        "/p:TargetFramework=$TargetFramework" `
        "/p:Configuration=$Configuration" `
        "/p:Platform=$platform" `
        /p:PublishReadyToRun=false `
        /p:PublishSignedPackage=true `
        "/p:AppxPackageDir=$appxDir" `
        "/p:PackageCertificateKeyFile=$certificateFullPath" `
        "/p:PackageCertificatePassword=$CertificatePassword" `
        /p:PackageCertificateThumbprint= `
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
& $makeAppx bundle /o /d $bundleInput /p $bundlePath

if ($LASTEXITCODE -ne 0) {
    throw "makeappx bundle command failed with exit code $LASTEXITCODE."
}

Write-Host "Signing MSIX bundle..."
& $signTool sign /fd SHA256 /f $certificateFullPath /p $CertificatePassword $bundlePath

if ($LASTEXITCODE -ne 0) {
    throw "signtool sign command failed with exit code $LASTEXITCODE."
}

Write-Host "Verifying MSIX bundle signature..."
& $signTool verify /pa /v $bundlePath

if ($LASTEXITCODE -ne 0) {
    throw "signtool verify command failed with exit code $LASTEXITCODE."
}

$zipPath = Join-Path $bundleOutput "Emerald-Windows-Signed-$platformSlug.zip"
Compress-Archive -Path $bundlePath -DestinationPath $zipPath -Force

Write-Host "Signed bundle created: $bundlePath"
Write-Host "Signed archive created: $zipPath"

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "windows_bundle_path=$bundlePath"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "windows_zip_path=$zipPath"
}
