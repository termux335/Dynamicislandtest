<#
.SYNOPSIS
    Membuat paket rilis yang sama dengan workflow CI (zip + checksum SHA-256).

.DESCRIPTION
    Menjalankan tes (kecuali -SkipTests), menerbitkan app untuk win-x64,
    menyalin dokumen rilis, lalu menulis hasilnya ke dist/.

    Versi diambil dari windows/Directory.Build.props, jadi angka rilis hanya
    diubah di satu tempat. Nama berkas mengikuti workflow rilis GitHub:
    DynamicIsland-<versi>-win-x64.zip dan varian -selfcontained.

.PARAMETER SelfContained
    Ikut membawa runtime .NET (~150 MB). Bawaan: framework-dependent (~2 MB,
    butuh .NET 8 Desktop Runtime).

.PARAMETER SkipTests
    Lewati tes. Hanya untuk build cepat saat menguji skrip ini sendiri.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\Scripts\release.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\Scripts\release.ps1 -SelfContained
#>
param(
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $windowsRoot
$project = Join-Path $windowsRoot 'DynamicIsland.App\DynamicIsland.App.csproj'
$testProject = Join-Path $windowsRoot 'DynamicIsland.Core.Tests\DynamicIsland.Core.Tests.csproj'
$propsPath = Join-Path $windowsRoot 'Directory.Build.props'
$dist = Join-Path $repoRoot 'dist'

$versionMatch = Select-String -Path $propsPath -Pattern '<Version>([^<]+)</Version>'
if (-not $versionMatch) { throw "Versi tidak ditemukan di $propsPath." }
$version = $versionMatch.Matches[0].Groups[1].Value

$suffix = if ($SelfContained) { 'selfcontained' } else { 'framework-dependent' }
$name = "DynamicIsland-$version-win-x64"
if ($SelfContained) { $name += '-selfcontained' }
$stage = Join-Path $env:TEMP ("island-release-" + [guid]::NewGuid().ToString('N'))

Write-Host "Dynamic Island $version ($suffix)" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host 'Menjalankan tes...' -ForegroundColor Cyan
    & dotnet test $testProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Tes gagal (kode $LASTEXITCODE)." }
}

$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
Write-Host "Menerbitkan ke $stage..." -ForegroundColor Cyan
& dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContainedValue `
    -p:Version=$version `
    -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal (kode $LASTEXITCODE)." }

foreach ($doc in @('README.md', 'CHANGELOG.md', 'LICENSE')) {
    $source = Join-Path $repoRoot $doc
    if (Test-Path $source) { Copy-Item $source -Destination $stage }
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist ($name + '.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Remove-Item -Path $stage -Recurse -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
# LF, bukan CRLF: `sha256sum -c` di Git Bash/Linux menolak berkas ber-CRLF.
[System.IO.File]::WriteAllText(
    $zip + '.sha256',
    "$hash  $($name).zip`n",
    [System.Text.Encoding]::ASCII)

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Selesai: $zip ($sizeMb MB)" -ForegroundColor Green
Write-Host "Checksum: $zip.sha256"
if (-not $SelfContained) {
    Write-Host 'Paket ini butuh .NET 8 Desktop Runtime di mesin tujuan.' -ForegroundColor DarkGray
}
