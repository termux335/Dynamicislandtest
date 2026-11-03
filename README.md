# Dynamic Island untuk Windows

Kapsul mengambang di tengah atas layar Windows 10/11 yang menampilkan musik,
timer, rapat, notifikasi, dan aktivitas lain — lalu ciut sendiri saat tidak
dibutuhkan. Windows tidak punya notch, jadi kapsulnya melayang 8 px dari tepi
atas.

Status: **0.2.0-beta**. Fitur Tahap 1–3 sudah dipakai harian; Tahap 4 (mode
tidak mengganggu) dan Tahap 5 (modul tambahan) belum dikerjakan. Belum
ditandatangani dan belum ada installer MSIX.

## Isi repo

| Path | Keterangan |
|---|---|
| [`windows/`](windows/) | Solusi C# .NET 8 + WPF (`DynamicIsland.App`, `DynamicIsland.Core`, tes) |
| [`shared/SPEC.md`](shared/SPEC.md) | Sumber kebenaran model data, antrean, state, dan ukuran tampilan |
| [`shared/events.md`](shared/events.md) | Format event JSON untuk uji manual |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Lapisan, alur data, threading, dan peta tes |
| [`docs/PRIVACY.md`](docs/PRIVACY.md) | Data yang dibaca/ditulis dan izin tiap fitur |
| [`windows/README.md`](windows/README.md) | Cara menjalankan, daftar fitur, izin, dan batasan |

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
