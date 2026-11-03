<#
.SYNOPSIS
    Mengukur pemakaian CPU Dynamic Island saat idle atau di dalam konteks game.

.DESCRIPTION
    Menjalankan app, menunggu 8 detik supaya semua monitor siap, lalu menghitung
    rata-rata CPU proses selama -Seconds detik. Hasilnya satu baris persen
    terhadap seluruh mesin, sama seperti yang dicatat di kriteria Tahap 4.

    App dihentikan lagi setelah pengukuran (proses yang sudah jalan sebelumnya
    tidak disentuh).

.PARAMETER Seconds
    Lama pengukuran setelah pemanasan. Bawaan 60 detik.

.PARAMETER Context
    Mengisi ISLAND_CONTEXT untuk menguji konteks palsu: game, video, meeting,
    share, presentation, atau normal.

.PARAMETER Exe
    Path DynamicIsland.App.exe. Bawaan: hasil build Release, lalu instalasi di
    %LOCALAPPDATA%\Programs\DynamicIsland.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\Scripts\measure-cpu.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\Scripts\measure-cpu.ps1 -Context game -Seconds 120
#>
param(
    [int]$Seconds = 60,
    [ValidateSet('', 'game', 'video', 'meeting', 'share', 'presentation', 'normal')]
    [string]$Context = '',
    [string]$Exe = ''
)

$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot

if (-not $Exe) {
    $candidates = @(
        (Join-Path $windowsRoot 'DynamicIsland.App\bin\Release\net8.0-windows10.0.19041.0\DynamicIsland.App.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\DynamicIsland\DynamicIsland.App.exe')
    )

    $Exe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $Exe -or -not (Test-Path $Exe)) {
    throw 'DynamicIsland.App.exe tidak ditemukan. Jalankan build Release atau beri -Exe.'
}

$env:ISLAND_NO_ONBOARDING = '1'
if ($Context) { $env:ISLAND_CONTEXT = $Context } else { Remove-Item Env:\ISLAND_CONTEXT -ErrorAction SilentlyContinue }

Write-Host "Mengukur $Exe (konteks: $(if ($Context) { $Context } else { 'normal' }))..." -ForegroundColor Cyan

$process = Start-Process -FilePath $Exe -PassThru
try {
    Start-Sleep -Seconds 8

    $process.Refresh()
    $startCpu = $process.TotalProcessorTime
    $started = [DateTime]::UtcNow

    Start-Sleep -Seconds $Seconds

    $process.Refresh()
    $elapsed = ([DateTime]::UtcNow - $started).TotalSeconds
    $used = ($process.TotalProcessorTime - $startCpu).TotalSeconds
    $cores = [Environment]::ProcessorCount
    $percent = 100 * $used / ($elapsed * $cores)

    "CPU rata-rata: {0:N2}% dari total mesin ({1:N1} detik, {2} inti)" -f $percent, $elapsed, $cores
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
