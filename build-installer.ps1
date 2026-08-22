# Builds WorkStationX into an installer.
#
#   .\build-installer.ps1
#
# Step 1 always runs. Step 2 needs Inno Setup installed; if it is missing the
# script says so and leaves the published folder ready to package by hand.

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "== 1/2  Publishing self-contained build ==" -ForegroundColor Cyan

$publish = Join-Path $root 'publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish (Join-Path $root 'WorkStationX\WorkStationX.csproj') -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$size = [math]::Round((Get-ChildItem $publish -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "   Published to $publish  ($size MB)" -ForegroundColor Green

Write-Host "== 2/2  Building installer ==" -ForegroundColor Cyan

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "   Inno Setup not found." -ForegroundColor Yellow
    Write-Host "   Install it from https://jrsoftware.org/isdl.php then run this again."
    Write-Host "   The published folder is ready at: $publish"
    return
}

& $iscc (Join-Path $root 'installer\WorkStationX.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

Write-Host "   Installer written to $(Join-Path $root 'dist')" -ForegroundColor Green
