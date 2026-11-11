param([ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet run --project tests/Still.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    $out = Join-Path $root "artifacts/Still-$Runtime"
    dotnet publish src/Still.App -c Release -r $Runtime --self-contained true -o $out -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md,LICENSE,THIRD-PARTY-NOTICES.md $out
    New-Item -ItemType Directory -Force "$out/docs" | Out-Null
    Copy-Item docs/* "$out/docs" -Recurse -Force
    # Preserve the licence files from the runtime packs bundled in Still.exe.
    $assets = Get-Content src/Still.App/obj/project.assets.json -Raw | ConvertFrom-Json
    $packages = $assets.project.restore.packagesPath
    $licences = Join-Path $out 'runtime-licences'
    New-Item -ItemType Directory -Force $licences | Out-Null
    foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
        foreach ($dependency in $framework.Value.downloadDependencies) {
            if ($dependency.name -match "^Microsoft\.(NETCore|WindowsDesktop)\.App\.(Runtime|Host)\.$Runtime`$") {
                $version = ($dependency.version -replace '[\[\]]', '').Split(',')[0].Trim()
                $pack = Join-Path $packages ($dependency.name.ToLowerInvariant() + '/' + $version)
                $destination = Join-Path $licences ($dependency.name + '-' + $version)
                New-Item -ItemType Directory -Force $destination | Out-Null
                Get-ChildItem $pack -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' } | Copy-Item -Destination $destination
            }
        }
    }
    Compress-Archive "$out/*" "artifacts/Still-$Runtime.zip" -Force
    Get-FileHash "$out/Still.exe" -Algorithm SHA256
} finally { Pop-Location }
