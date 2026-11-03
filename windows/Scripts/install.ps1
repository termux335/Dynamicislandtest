<#
.SYNOPSIS
    Memasang Dynamic Island untuk pengguna saat ini, tanpa hak admin.

.DESCRIPTION
    Menerbitkan app (Release, win-x64) lalu menyalinnya ke
    %LOCALAPPDATA%\Programs\DynamicIsland dan membuat pintasan di menu Start.

    Tidak ada installer pihak ketiga: cukup .NET SDK. Pengaturan pengguna
    (%APPDATA%\DynamicIsland\settings.json) tidak disentuh, jadi pemasangan
    ulang tetap memakai preferensi lama.

    Autostart tidak dinyalakan diam-diam: nyalakan dari menu klik kanan atau
    jendela Pengaturan setelah app jalan.

.PARAMETER SelfContained
    Ikut membawa runtime .NET (~150 MB) supaya tidak butuh .NET Desktop Runtime.

.PARAMETER Launch
    Jalankan app setelah pemasangan selesai.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\Scripts\install.ps1 -Launch
#>
param(
    [switch]$SelfContained,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $windowsRoot 'DynamicIsland.App\DynamicIsland.App.csproj'
$target = Join-Path $env:LOCALAPPDATA 'Programs\DynamicIsland'
$staging = Join-Path $env:TEMP ("island-publish-" + [guid]::NewGuid().ToString('N'))
$shortcutFolder = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcut = Join-Path $shortcutFolder 'Dynamic Island.lnk'
$exe = Join-Path $target 'DynamicIsland.App.exe'

Write-Host "Menerbitkan Dynamic Island..." -ForegroundColor Cyan

$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', $selfContainedValue,
    '-o', $staging
)

& dotnet @arguments | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal (kode $LASTEXITCODE)." }

# App yang sedang jalan memegang berkasnya; tutup dulu supaya bisa diganti.
$running = Get-Process -Name 'DynamicIsland.App' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Menutup instance yang sedang jalan..." -ForegroundColor Yellow
    $running | Stop-Process
    Start-Sleep -Milliseconds 600
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
Get-ChildItem -Path $target -Force | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $staging '*') -Destination $target -Recurse -Force
Remove-Item -Path $staging -Recurse -Force

New-Item -ItemType Directory -Force -Path $shortcutFolder | Out-Null
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $target
$link.Description = 'Dynamic Island'
$link.Save()

Write-Host "Terpasang di $target" -ForegroundColor Green
Write-Host "Pintasan: $shortcut"
if (-not $SelfContained) {
    Write-Host "Butuh .NET 8 Desktop Runtime (sudah ada bila 'dotnet --list-runtimes' menampilkannya)." -ForegroundColor DarkGray
}

if ($Launch) {
    Start-Process -FilePath $exe
}
