# Crée dist\Vectra.zip : Vectra.exe autonome (aucun .NET à installer) + LISEZMOI.txt.
# Usage : powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Version 1.2.3]
param([string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\Vectra'
$zip = Join-Path $root 'dist\Vectra.zip'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

if (Get-Process Vectra -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$out*" }) { throw "Ferme Vectra (lancé depuis dist) avant de recréer le paquet." }
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$extra = @()
if ($Version) { $extra += "-p:Version=$Version" } # sinon : version de Directory.Build.props
& $dotnet publish (Join-Path $root 'src\DualTune.App') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None @extra -o $out
if ($LASTEXITCODE -ne 0) { throw "La publication a échoué ($LASTEXITCODE)." }

Copy-Item (Join-Path $PSScriptRoot 'LISEZMOI.txt') $out
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $out 'LICENSE.txt')
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $out
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Paquet prêt : $zip ($size Mo)"
