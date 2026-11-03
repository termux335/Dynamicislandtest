# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/) dan
[Semantic Versioning](https://semver.org/lang/id/). Semua perubahan yang terlihat
pengguna dicatat di sini; detail teknis ada di riwayat git.

## [Unreleased]

### Added

- CI GitHub Actions: build Release, 165+ tes, dan artefak publish di setiap push/PR.
- Workflow rilis untuk tag `v*`: dua varian zip (framework-dependent dan
  self-contained), checksum SHA-256, dan catatan rilis otomatis.
- `Scripts/release.ps1` untuk membuat paket rilis yang sama di mesin lokal.
- `LICENSE` (MIT), `CONTRIBUTING.md`, `SECURITY.md`, `docs/ARCHITECTURE.md`,
  `docs/PRIVACY.md`, dan README akar repo.
- `.editorconfig`, `.gitattributes`, `windows/global.json`, dan analisis statis
  .NET (`AnalysisLevel=latest-recommended`, peringatan jadi galat di CI).

### Changed

- Versi naik ke `0.2.0-beta`; metadata assembly memakai nama proyek, bukan
  "alpha pribadi".
- Log notifikasi tidak lagi mencatat judul pesan; hanya nama aplikasi pengirim.

### Fixed

- `Directory.Build.props` kini menjadi satu sumber versi, nama produk, dan
  copyright untuk semua proyek.

## [0.1.0-alpha] - 2026-10-31

Rilis internal pertama.

### Added

- Overlay kapsul transparan, selalu di atas, tanpa merebut fokus, di tengah atas
  monitor utama; tiga state (minimal, compact, expanded) dengan animasi pegas.
- Antrean aktivitas berprioritas, dua kapsul dengan promosi lewat ketuk, dan
  ciut otomatis 4 detik.
- Tahap 1: kontrol media (Now Playing Windows), timer/fokus/stopwatch, rapat
  lewat pemakaian mikrofon + bisukan Core Audio, notifikasi Windows 4 detik.
- Tahap 2: acara `.ics` berikutnya, progres unduhan/pesanan, baterai, perangkat
  dilepas, shelf berkas, mode ringkas/standar.
- Tahap 3 sebagian: aksi cepat tekan-tahan, gestur gulir, lirik `.lrc` lokal,
  penempatan adaptif, pengaturan tersimpan, bahasa Indonesia/English, Mode Kocak,
  aksen warna, posisi, dan preset prioritas.
- Onboarding, jendela pengaturan, autostart HKCU, skrip pasang/cabut tanpa admin,
  dan skrip event palsu untuk uji manual.
- 165 tes unit untuk antrean, state, parser event, kalender, timer, penempatan,
  strings, lirik, dan controller.
