# Dynamic Island untuk Windows

Kapsul mengambang di tengah atas layar Windows 10/11 yang menampilkan musik,
timer, rapat, notifikasi, dan aktivitas lain — lalu ciut sendiri saat tidak
dibutuhkan. Windows tidak punya notch, jadi kapsulnya melayang 8 px dari tepi
atas.

Status: **0.4.0-beta**. Fitur Tahap 1–4 sudah dipakai harian, termasuk mode tidak
mengganggu (game, video layar penuh, rapat, berbagi layar), mode berbagi layar
aman, jadwal salat lokal, peringatan cuaca BMKG opsional, pengingat kebiasaan,
skenario otomatis, dan pelindung layar OLED.

Tahap 5a sudah tersedia: riwayat clipboard aman (data sensitif disamarkan,
memori saja), command palette dengan kalkulator dan konversi satuan offline,
cermin kamera sementara, tampilan volume/kecerahan, dan pintasan aplikasi.
Tahap 5b–5c belum dikerjakan. Belum ditandatangani dan belum ada installer MSIX.

Kontrol tanpa keluar dari game: **Ctrl+Alt+Spasi** putar/jeda, **Ctrl+Alt+N**
lagu berikutnya, **Ctrl+Alt+M** bisukan mikrofon, **Ctrl+Alt+V** command palette.

## Isi repo

| Path | Keterangan |
|---|---|
| [`windows/`](windows/) | Solusi C# .NET 8 + WPF (`DynamicIsland.App`, `DynamicIsland.Core`, tes) |
| [`shared/SPEC.md`](shared/SPEC.md) | Sumber kebenaran model data, antrean, state, dan ukuran tampilan |
| [`shared/events.md`](shared/events.md) | Format event JSON untuk uji manual |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Lapisan, alur data, threading, dan peta tes |
| [`docs/PRIVACY.md`](docs/PRIVACY.md) | Data yang dibaca/ditulis dan izin tiap fitur |
| [`windows/README.md`](windows/README.md) | Cara menjalankan, daftar fitur, izin, dan batasan |

## Mode tidak mengganggu (Tahap 4)

Saat game layar penuh, video layar penuh, rapat, presentasi, atau berbagi layar,
kapsul menyesuaikan diri: redup 40%, ciut, pindah ke posisi aman, tembus klik,
hanya peringatan kritis, atau sembunyi. Konteks game ditahan 3 detik setelah
keluar supaya tidak berkedip saat Alt-Tab.

Deteksi memakai `SHQueryUserNotificationState` dan window depan — tanpa injeksi,
DLL ke proses lain, atau hook DirectX. Uji manual:

```powershell
$env:ISLAND_CONTEXT = 'game'     # game | video | meeting | share | presentation | normal
$env:ISLAND_SCREEN_SHARE = '1'  # paksa mode berbagi layar aman
```

## Mulai cepat

Butuh Windows 10 2004 (build 19041)+ dan .NET SDK 8+.

```powershell
cd windows
dotnet build -c Release
dotnet test -c Release
dotnet run --project DynamicIsland.App
```

Pasang untuk dipakai harian (tanpa admin):

```powershell
powershell -ExecutionPolicy Bypass -File windows\Scripts\install.ps1 -Launch
```

## Rilis

Build rilis dibuat oleh CI pada tag `v*` (lihat
`.github/workflows/release.yml`) atau secara lokal:

```powershell
powershell -ExecutionPolicy Bypass -File windows\Scripts\release.ps1
```

Hasilnya ada di `dist/`: zip framework-dependent, zip self-contained opsional,
dan checksum SHA-256. Riwayat perubahan ada di [CHANGELOG.md](CHANGELOG.md).

## Kontribusi

Baca [CONTRIBUTING.md](CONTRIBUTING.md). Perubahan perilaku dimulai dari
`shared/SPEC.md` dan tes, bukan dari UI. Jangan menyalin kode dari proyek GPL v3;
proyek macOS di `Reference/` hanya acuan konsep.

## Lisensi

[MIT](LICENSE). Proyek ini tidak menyertakan iklan dan tidak memakai kode GPL v3.
