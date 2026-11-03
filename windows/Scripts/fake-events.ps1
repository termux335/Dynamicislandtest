<#
.SYNOPSIS
  Mengirim event palsu ke Dynamic Island yang sedang berjalan.

.DESCRIPTION
  Satu baris JSON per koneksi ke named pipe lokal. Format event ada di
  shared/events.md. App harus sudah jalan (dotnet run --project DynamicIsland.App).

.EXAMPLE
  ./fake-events.ps1 all
  ./fake-events.ps1 timer
  ./fake-events.ps1 progress
  ./fake-events.ps1 ics        # tulis kalender uji .ics (acara 15 menit lagi)
  ./fake-events.ps1 raw '{"op":"clearAll"}'
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Mode = "all",

    [Parameter(Position = 1)]
    [string]$Raw,

    [string]$PipeName = $(if ($env:ISLAND_PIPE) { $env:ISLAND_PIPE } else { "DynamicIslandEvents" })
)

$ErrorActionPreference = "Stop"

function Send-Event {
    param([string]$Json)

    $client = [System.IO.Pipes.NamedPipeClientStream]::new(".", $PipeName, [System.IO.Pipes.PipeDirection]::Out)
    try {
        $client.Connect(3000)
        $writer = [System.IO.StreamWriter]::new($client, [System.Text.UTF8Encoding]::new($false))
        $writer.AutoFlush = $true
        $writer.WriteLine($Json)
        $writer.Dispose()
    }
    finally {
        $client.Dispose()
    }

    Write-Host "-> $Json"
}

# Tahap 2: app membaca acara berikutnya dari .ics lokal (ISLAND_ICS atau %TEMP%).
function Write-TestCalendar {
    $path = if ($env:ISLAND_ICS) {
        $env:ISLAND_ICS
    }
    else {
        Join-Path $env:TEMP "dynamic-island.ics"
    }

    $start = (Get-Date).AddMinutes(15).ToUniversalTime()
    $end = $start.AddMinutes(30)
    $startText = $start.ToString("yyyyMMdd'T'HHmmss'Z'")
    $endText = $end.ToString("yyyyMMdd'T'HHmmss'Z'")

    @(
        "BEGIN:VCALENDAR"
        "VERSION:2.0"
        "BEGIN:VEVENT"
        "UID:dynamic-island-test"
        "SUMMARY:Rapat Produk"
        "DTSTART:$startText"
        "DTEND:$endText"
        "DESCRIPTION:Gabung di https://meet.example/abc"
        "END:VEVENT"
        "END:VCALENDAR"
    ) | Set-Content -Path $path -Encoding utf8

    Write-Host "-> kalender uji ditulis ke $path (acara mulai 15 menit lagi)"
}

$media = '{"op":"upsert","activity":{"id":"track-1","kind":"media","title":"Lagu A","subtitle":"Artis B","progress":0.42,"actions":[{"id":"playpause","label":"Jeda","glyph":"pause"},{"id":"next","label":"Lagu berikutnya","glyph":"next"}]}}'
$timer = '{"op":"upsert","activity":{"id":"timer-1","kind":"timer","title":"Fokus 24:38","subtitle":"Sesi 2 dari 4","progress":0.18,"actions":[{"id":"pause","label":"Jeda timer","glyph":"pause"}]}}'
$call = '{"op":"upsert","activity":{"id":"call-1","kind":"call","title":"Rapat Desain","subtitle":"12:04","actions":[{"id":"mute","label":"Bisukan","glyph":"mute"},{"id":"join","label":"Kembali ke rapat","glyph":"join"}]}}'
$notify = '{"op":"upsert","activity":{"id":"notif-1","kind":"notification","title":"Pesan baru dari Rani","subtitle":"Bisa lihat ini?","ttlMs":4000,"actions":[{"id":"reply","label":"Balas","glyph":"reply"}]}}'
$download = '{"op":"upsert","activity":{"id":"download-1","kind":"download","title":"Mengunduh album.zip","subtitle":"12 MB dari 40 MB","progress":0.3}}'
$battery = '{"op":"upsert","activity":{"id":"battery-1","kind":"battery","title":"Baterai lemah","subtitle":"14% tersisa","ttlMs":4000}}'
$calendar = '{"op":"upsert","activity":{"id":"calendar-1","kind":"calendar","title":"Rapat Produk","subtitle":"mulai dalam 15 menit","actions":[{"id":"join","label":"Gabung","glyph":"join"}]}}'
$order = '{"op":"upsert","activity":{"id":"order-1","kind":"order","title":"Paket #A-1024","subtitle":"Dikirim · tiba hari ini","progress":0.6,"actions":[{"id":"track","label":"Lacak","glyph":"track"}]}}'
$humorTickle = '{"op":"upsert","activity":{"id":"humor-tickle","kind":"notification","title":"Aduh, geli.","ttlMs":3000}}'
$humorSulk = '{"op":"upsert","activity":{"id":"timer-1","kind":"timer","title":"Fokus 25:00","subtitle":"Halo? Masih hidup?","progress":0.5}}'
$humorRepeat = '{"op":"upsert","activity":{"id":"humor-repeat","kind":"notification","title":"Lagu ini lagi? Aku sampai hafal.","ttlMs":4000}}'
$humorEaster = '{"op":"upsert","activity":{"id":"humor-easter","kind":"notification","title":"Kamu menemukan rahasia!","ttlMs":3000}}'

switch ($Mode) {
    "all" {
        Send-Event $media
        Start-Sleep -Milliseconds 800
        Send-Event $timer
        Start-Sleep -Milliseconds 800
        Send-Event $notify
        Start-Sleep -Milliseconds 800
        Send-Event $call
        Start-Sleep -Milliseconds 800
        Send-Event $download
    }
    "media" { Send-Event $media }
    "timer" { Send-Event $timer }
    "call" { Send-Event $call }
    "notify" { Send-Event $notify }
    "download" { Send-Event $download }
    "battery" { Send-Event $battery }
    "calendar" { Send-Event $calendar }
    "order" { Send-Event $order }
    "humor-tickle" { Send-Event $humorTickle }
    "humor-sulk" { Send-Event $humorSulk }
    "humor-repeat" { Send-Event $humorRepeat }
    "humor-easter" { Send-Event $humorEaster }
    "progress" { Send-Event '{"op":"progress","id":"track-1","progress":0.75}' }
    "expire" { Send-Event '{"op":"expire","id":"notif-1"}' }
    "clear" { Send-Event '{"op":"clear","id":"track-1"}' }
    "clearAll" { Send-Event '{"op":"clearAll"}' }
    "ics" { Write-TestCalendar }
    "ics-clear" {
        $path = if ($env:ISLAND_ICS) { $env:ISLAND_ICS } else { Join-Path $env:TEMP "dynamic-island.ics" }
        if (Test-Path $path) { Remove-Item $path -Force }
        Write-Host "-> kalender uji dihapus: $path"
    }
    "raw" {
        if (-not $Raw) { throw "Mode raw butuh argumen JSON, mis. raw '{`"op`":`"clearAll`"}'" }
        Send-Event $Raw
    }
    default { throw "Mode tidak dikenal: $Mode (pakai all, media, timer, call, notify, download, battery, calendar, order, progress, expire, clear, clearAll, ics, ics-clear, raw)" }
}
