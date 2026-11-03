# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/) dan
[Semantic Versioning](https://semver.org/lang/id/). Semua perubahan yang terlihat
pengguna dicatat di sini; detail teknis ada di riwayat git.

## [0.4.0-beta] - 2026-11-03

Tahap 5a: modul harian yang berdiri sendiri.

### Added

- Riwayat clipboard aman: 30 entri, 24 jam, hanya di memori. Kata sandi, token,
  API key, OTP/PIN, JWT, dan nomor kartu (Luhn) disamarkan sebelum disimpan.
  Gambar disalin dibaca dengan OCR bawaan Windows bila tersedia.
- Command palette **Ctrl+Alt+V**: hitungan ("15% dari 240 ribu"), konversi satuan
  offline (panjang, massa, waktu, data, suhu), pencarian clipboard, dan pintasan.
- Cermin kamera sementara: pratinjau di jendela sendiri, indikator aktif, kamera
  mati saat jendela ditutup atau island menciut, tanpa menyimpan gambar.
- Tampilan volume/kecerahan pengganti: volume lewat Core Audio, kecerahan lewat
  DDC/CI; saklar sendiri, mati secara bawaan.
- Pintasan aplikasi/folder dari `shortcuts.json`, tampil sebagai tombol kecil di
  kartu expanded dan bisa dicari dari palette.
- 29 tes baru: penyamaran data, Luhn, riwayat/retensi, kalkulator dan konversi,
  palette, serta format OSD (total 245).

### Changed

- Versi naik ke `0.4.0-beta`; pengaturan menyimpan saklar module Tahap 5a.
- Saklar modul diterapkan tanpa restart lewat `App.ApplyModules`.

## [0.3.0-beta] - 2026-11-03

Tahap 4: mode tidak mengganggu dan modul pembeda.

### Added

- Deteksi konteks tanpa injeksi: game/video layar penuh
  (`SHQueryUserNotificationState` + window depan), rapat (mikrofon), presentasi,
  dan berbagi layar. Konteks game ditahan 3 detik setelah keluar.
- Enam tingkat respons per konteks: normal, redup+ciut, pindah posisi aman,
  tembus klik, hanya peringatan kritis, dan sembunyi. Bisa ditimpa per aplikasi.
- Penempatan aman otomatis (tengah atas → kiri atas → kanan atas → tengah
  bawah), zona terlarang per game, geser menjauhi kursor 80 px, dan profil
  aplikasi tersimpan di `app-profiles.json`.
- Mode berbagi layar aman: isi notifikasi disembunyikan dan diganti “Ada pesan
  baru”.
- Jadwal salat lokal (Subuh -20°, Isya -18°) tanpa internet, dengan hitung mundur.
- Peringatan cuaca BMKG opsional; mati secara bawaan, butuh kode ADM4 dan izin
  internet saat dinyalakan.
- Pengingat kebiasaan (minum, istirahat mata 20 menit, lama di depan layar)
  dengan varian Mode Kocak.
- Skenario otomatis: rapat (mic + durasi), belajar (timer + tahan notifikasi),
  dan charging malam (jam).
- Pintasan global Ctrl+Alt+Spasi / N / M dengan umpan balik singkat di kapsul.
- Pelindung layar OLED: geser mikro 2 px tiap 4 menit, redup setelah idle 60
  detik, sembunyi setelah 10 menit.
- `Scripts/measure-cpu.ps1` untuk mencatat pemakaian CPU saat idle/tersembunyi.
- 51 tes baru: konteks, tingkat respons, penempatan aman/profil, skenario,
  kebiasaan, jadwal salat, parser BMKG, dan pelindung OLED (total 216).

### Changed

- Versi naik ke `0.3.0-beta`.
- `IslandPresentation` membawa konteks, tingkat respons, opacity, dan tembus
  klik; controller memaksa state minimal dan menyaring aktivitas non-kritis.

## [0.2.0-beta] - 2026-11-03

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
