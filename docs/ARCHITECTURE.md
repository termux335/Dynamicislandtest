# Arsitektur

Dokumen ini menjelaskan bagaimana kode disusun supaya perubahan tidak merusak
aturan di `shared/SPEC.md`.

## Lapisan

```
DynamicIsland.App (WPF, net8.0-windows10.0.19041.0)
  ├── IslandWindow / IslandViewModel   tampilan, animasi, menu, input
  ├── Monitor*                        sumber data dari Windows
  ├── IslandEventPipe                 event JSON dari proses lain (uji manual)
  └── SettingsWindow / StartupRegistration
        │  memakai
        ▼
DynamicIsland.Core (net8.0, tanpa UI)
  ├── Activity, ActivityQueue         model + antrean prioritas
  ├── StateMachine                    transisi minimal/compact/expanded
  ├── IslandController                orkestrasi, AutoCollapse, presentasi
  ├── IslandEvent                     parser event JSON
  ├── IslandMetrics, DisplayMode      ukuran kapsul per state
  ├── TimerEngine, CalendarPlan, IcsCalendar, Alerts
  ├── Placement, Customization, Meetings, Settings
  ├── ContextAwareness, SafePlacement, AppProfiles   mode tidak mengganggu
  ├── Scenarios, Habits, PrayerTimes, WeatherAlert, OledGuard
  ├── ClipboardSafety, Calculator, Palette, Osd   modul Tahap 5a
  ├── Notes, Presence, VoiceNotes, Accessibility   modul Tahap 5b
  ├── Stage5C                                     modul Tahap 5c
  ├── HumorBehaviors, QuickActions                perilaku kocak & aksi cepat
  └── Strings, Lyrics, LyricsFiles
        ▲
        │ dites oleh
DynamicIsland.Core.Tests (xunit, 307 tes)
```

Aturan pemisahan: **Core tidak boleh menyentuh WPF atau P/Invoke.** Semua yang
butuh jendela, registry, Core Audio, atau WinRT tinggal di App. Dengan begitu
logika antrean/state/timer bisa dites tanpa mesin Windows.

## Alur data

1. Monitor (media, timer, mikrofon, notifikasi, baterai, drive, kalender) atau
   named pipe membuat `Activity`.
2. `IslandController` menaruhnya di `ActivityQueue`, menerapkan preset prioritas,
   lalu memanggil `Publish()`.
3. `Publish()` menghasilkan `IslandPresentation` (utama, kedua, mode) dan
   memicu `Changed` hanya bila isinya berbeda.
4. `IslandViewModel` meneruskan presentasi ke binding WPF; `IslandWindow`
   menyesuaikan ukuran/posisi dan menjalankan animasi pegas.
5. `DispatcherTimer` 100 ms memanggil `Tick()`: mencabut alert kedaluwarsa,
   menciutkan expanded setelah 4 detik, dan menerbitkan ulang presentasi.

Semua monitor dan timer berjalan di Dispatcher UI. Pekerjaan lambat (publish,
I/O) berjalan di thread lain dan hasilnya dikembalikan lewat
`Dispatcher.BeginInvoke`.

## Kontrak penting

- **Satu aktivitas utama.** `ActivityQueue.Primary` selalu prioritas tertinggi;
  seri dimenangkan urutan masuk.
- **Upsert di tempat.** Id yang sama memperbarui konten tanpa mengubah urutan
  FIFO (`insertionOrder`).
- **Promosi pengguna** menimpa urutan tanpa mengubah nilai prioritas, dan
  berakhir saat aktivitas yang lebih penting masuk.
- **Transient Alert** punya `ExpiresAt`; `PurgeExpired` mencabutnya, termasuk
  yang sedang menunggu.
- **Transisi state** hanya satu langkah; `minimal → expanded` dipecah dua.

## Threading dan sumber daya

- Setiap monitor memegang sumber dayanya sendiri dan wajib `Dispose`.
- Kegagalan perangkat/izin ditangkap di monitor, dicatat lewat `Log`, dan
  dilaporkan ke kapsul bila terlihat pengguna. App tidak boleh mati karena satu
  fitur gagal.
- Tidak ada hook keyboard, injeksi proses, atau pembacaan memori proses lain.
  Interaksi dengan aplikasi lain memakai API publik Windows.

## Lokasi berkas

| Berkas | Isi |
|---|---|
| `%APPDATA%\DynamicIsland\settings.json` | preferensi pengguna |
| `%APPDATA%\DynamicIsland\lyrics\*.lrc` | lirik lokal (dibaca, tidak diunduh) |
| `%TEMP%\dynamic-island.log` | log uji manual |
| `%TEMP%\dynamic-island.ics` | kalender uji (bisa dialihkan `ISLAND_ICS`) |
| `%APPDATA%\DynamicIsland\app-profiles.json` | posisi/opacity terakhir per aplikasi (bisa dialihkan `ISLAND_APP_PROFILES`) |
| `%APPDATA%\DynamicIsland\agent-status.json` | status agen lokal yang ditulis alat pengguna |
| `%APPDATA%\DynamicIsland\agent-response.json` | jawaban setuju/tolak untuk alat agen |
| `%APPDATA%\DynamicIsland\island-hook.ps1` / `.cmd` | skrip relay hook Claude Code (dipasang dari Pengaturan) |
| `~/.claude/settings.json` | entri hook `island-hook` untuk Claude Code (cadangan `.bak` dibuat dulu) |
| `%APPDATA%\DynamicIsland\commands.json` | perintah terminal yang didaftarkan pengguna |
| `%APPDATA%\DynamicIsland\plugins\*\plugin.json` | manifest plugin dan izin yang diminta |
| `Pictures\DynamicIsland\*.png` | hasil tangkapan layar |

## Mode tidak mengganggu (Tahap 4)

`ContextMonitor` (App) mengumpulkan sinyal dari window depan, mikrofon, dan
`SHQueryUserNotificationState`, lalu `ContextRules.Classify` memilih satu
konteks. `ContextDetector` menahan konteks game 3 detik setelah keluar.
`ContextResolver` menerapkan preferensi per konteks/aplikasi dan menghasilkan
`ResponseEffect`; `IslandController.Publish` memaksa state minimal, menyaring
aktivitas non-kritis, dan menandai `Visible`/`ClickThrough`/`Opacity`.
`IslandWindow` yang mengeksekusi: ubah opacity, pasang `WS_EX_TRANSPARENT`,
sembunyikan window, dan pindah ke posisi aman. Tidak ada hook atau injeksi.

Saklar modul (konteks, berbagi aman, kebiasaan, salat, cuaca, OLED) disimpan di
`IslandSettings` dan diterapkan lewat `App.ApplyModules` tanpa restart.

## Peta tes

| Berkas tes | Mengunci perilaku |
|---|---|
| `ActivityQueueTests` | prioritas, FIFO, upsert, promosi, expiry, kestabilan |
| `StateMachineTests` | graf transisi dan pemecahan lompatan |
| `IslandControllerTests` | publish, auto-collapse, preset prioritas, dua kapsul |
| `IslandEventTests` | validasi dan parsing event JSON |
| `IslandMetricsTests`, `DisplayModeTests` | ukuran per state/mode |
| `TimerEngineTests`, `MeetingsTests` | hitung waktu dan durasi rapat |
| `CalendarPlanTests`, `AlertsTests` | label hitung mundur dan alert |
| `PlacementTests` | penempatan adaptif |
| `SettingsTests`, `CustomizationTests` | persistensi dan preset |
| `StringsTests` | paritas kunci dua bahasa, fallback humor, a11y serius |
| `LyricsTests`, `LyricsFilesTests`, `LyricsLibraryTests` | parser LRC dan nama berkas |
| `ContextAwarenessTests`, `ContextControllerTests` | klasifikasi konteks, tingkat respons, tahan 3 detik, penyaringan kritis |
| `SafePlacementTests` | urutan posisi aman, zona terlarang, dodge kursor, profil aplikasi |
| `Stage4ModuleTests` | skenario, kebiasaan, jadwal salat, BMKG, pelindung OLED |
| `ClipboardSafetyTests` | penyamaran kartu/Luhn/password/token, retensi, pencarian |
| `CalculatorTests`, `PaletteTests` | aritmetika, persen, konversi satuan, pencarian palette |
| `OsdTests` | format dan throttle tampilan volume/kecerahan |
| `Stage5BTests` | catatan, pengingat, tunda mengetik, jam tenang, fokus, aksesibilitas, warna sampul |
| `Stage5CTests` | status agen, terminal, RSS, asisten, manifest plugin |
| `HumorBehaviorsTests` | perilaku kocak (mata idle, geli, ngambek, lagu berulang, drama timer, telur paskah) dan kustomisasi aksi cepat |

Menambah perilaku baru berarti menambah tes di salah satu berkas ini; tes
prioritas dan transisi state wajib ada sebelum UI-nya.

## Cara memperluas

- **Aktivitas baru:** tambah `ActivityKind` hanya bila benar-benar jenis baru;
  kalau tidak, pakai kind yang ada dengan `priority` eksplisit. Perbarui
  `Glyphs.For`, `IslandMetrics`, dan `strings-*.json`.
- **Monitor baru:** ikuti pola `BatteryMonitor` (timer + `Dispose` + `Refresh`),
  atau `MediaMonitor` bila datanya async.
- **Modul opsional (Tahap 5 nanti):** satu saklar di `IslandSettings`, satu file
  monitor, satu baris di `App.OnStartup`/`OnExit`, dan tes sendiri — supaya bisa
  dimatikan tanpa memengaruhi modul lain.
