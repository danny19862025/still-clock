param(
    [string]$Compiler = '',
    [string]$Version = '2.5.1',
    [string]$PackageDir = '',
    [string]$OutputDir = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $PackageDir) { $PackageDir = Join-Path $root 'artifacts/Still-win-x64' }
if (-not $OutputDir) { $OutputDir = Join-Path $root 'artifacts' }
$PackageDir = [IO.Path]::GetFullPath($PackageDir)
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path (Join-Path $PackageDir 'Still.exe'))) { throw 'Publish Still first with scripts/publish.ps1.' }
if (-not $Compiler) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $Compiler = $found.Source }
    else {
        foreach ($candidate in @("$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")) {
            if (Test-Path $candidate) { $Compiler = $candidate; break }
        }
    }
}
if (-not $Compiler -or -not (Test-Path $Compiler)) { throw 'Install Inno Setup 6.3+ or 7, or pass -Compiler with the ISCC.exe path.' }
New-Item -ItemType Directory -Force $OutputDir | Out-Null
& $Compiler "/DAppVersion=$Version" "/DSourceDir=$PackageDir" "/DOutputDir=$OutputDir" (Join-Path $root 'installer/Still.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed ($LASTEXITCODE)." }
Get-FileHash (Join-Path $OutputDir 'Still-Setup-win-x64.exe') -Algorithm SHA256
