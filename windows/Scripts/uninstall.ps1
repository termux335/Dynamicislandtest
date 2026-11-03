<#
.SYNOPSIS
    Mencabut pemasangan Dynamic Island untuk pengguna saat ini.

.DESCRIPTION
    Menutup app yang sedang jalan, menghapus pintasan menu Start, folder
    %LOCALAPPDATA%\Programs\DynamicIsland, dan entri autostart miliknya.

    Pengaturan dan preferensi di %APPDATA%\DynamicIsland sengaja dibiarkan
    kecuali -PurgeSettings dipakai.
#>
param(
    [switch]$PurgeSettings
)

$ErrorActionPreference = 'Stop'

$target = Join-Path $env:LOCALAPPDATA 'Programs\DynamicIsland'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Dynamic Island.lnk'
$settings = Join-Path $env:APPDATA 'DynamicIsland'

$running = Get-Process -Name 'DynamicIsland.App' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Menutup Dynamic Island..." -ForegroundColor Yellow
    $running | Stop-Process
    Start-Sleep -Milliseconds 600
}

if (Test-Path $shortcut) { Remove-Item -Path $shortcut -Force }
if (Test-Path $target) { Remove-Item -Path $target -Recurse -Force }

$runKey = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -Path $runKey -Name 'DynamicIsland' -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name 'DynamicIsland'
    Write-Host 'Entri autostart dicabut.'
}

if ($PurgeSettings -and (Test-Path $settings)) {
    Remove-Item -Path $settings -Recurse -Force
    Write-Host 'Pengaturan dihapus.' -ForegroundColor Yellow
}

Write-Host 'Dynamic Island telah dicabut.' -ForegroundColor Green
