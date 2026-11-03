# Changelog

Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/) dan
[Semantic Versioning](https://semver.org/lang/id/). Semua perubahan yang terlihat
pengguna dicatat di sini; detail teknis ada di riwayat git.

## [0.6.0-beta] - 2026-11-03

Tahap 5c: modul opsional dan penutup Tahap 5.

### Added

- Status agen AI coding dari `agent-status.json`: state, token terpakai, dan
  tombol Setujui/Tolak yang menulis `agent-response.json`. Tanpa kunci API.
- Terminal cepat: hanya perintah di `commands.json`, dijalankan dari palette
  setelah konfirmasi, tanpa shell dan tanpa sumber luar.
- Tangkapan layar ke `Pictures\\DynamicIsland` dari palette.
- Ticker RSS/Atom opsional, mati bawaan, hanya judul/tautan, gagal tampil jelas.
- Asisten AI opsional (OpenAI-compatible): kunci hanya di memori, pengiriman
  hanya saat tombol Kirim ditekan.
- SDK plugin lokal dengan manifest dan izin terbatas.
- Visualizer kartu media dan warna aksen dari sampul album, plus pemilih jam
  tenang dan playlist fokus di Pengaturan.
- Perilaku Mode Kocak lengkap sesuai CLAUDE.windows.md: mata kapsul mengikuti gerak
  kursor saat idle, goyang pegas 5 klik cepat ("Aduh, geli."), kapsul ngambek setelah
  10 menit diabaikan saat timer jalan ("Halo? Masih hidup?"), deteksi lagu berulang
  3 kali berturut-turut ("Lagu ini lagi? Aku sampai hafal."), hitung mundur drama
  10 detik terakhir berdenyut, dan telur paskah.
- Saklar per perilaku Mode Kocak di jendela Pengaturan.
- Aksi cepat fleksibel dan dapat dikustomisasi lewat `quick-actions.json`.
- Skrip Inno Setup 6 (`windows/installer.iss`) untuk pembuatan installer desktop mandiri.
- 41 tes baru untuk parser 5c, perilaku kocak, dan persistensi (total 307).

### Changed

- Versi naik ke `0.6.0-beta`; preferensi 5c disimpan di `Stage5C`.
- Tahap 5b tidak lagi menyisakan utang UI.

### Known limitations

- Rekaman layar belum ada; hanya tangkapan layar.
- Plugin berjalan in-process dan belum punya sandbox penuh.

## [0.5.0-beta] - 2026-11-03

Tahap 5b: produktivitas, kenyamanan, dan aksesibilitas.

### Added

- Catatan dan snippet lokal (`notes.json`): tambah lewat palette (`catat …`),
  cari, salin, dan pengingat lewat catatan suara.
- Catatan suara: satu ucapan di perangkat, teks bisa diedit sebelum disimpan;
  "ingatkan 10 menit …" menjadi pengingat. Bila pengenalan suara gagal,
  alasannya tampil dan catatan tetap bisa diketik.
- Tunda notifikasi saat mengetik: hanya memakai waktu input terakhir, tanpa hook
  keyboard dan tanpa menyimpan isi ketikan; notifikasi dilepas setelah berhenti.
- Jam tenang otomatis: kapsul redup (65%) dan hanya peringatan kritis lewat.
- Musik fokus: playlist pilihan pengguna diputar saat sesi fokus dimulai; app
  tidak membawa atau mengunduh musik.
- Aksesibilitas: bacakan notifikasi dengan suara Windows, teks besar (1,25x),
  kontras tinggi, dan navigasi keyboard yang sudah ada tetap berlaku.
- 19 tes baru untuk catatan, pengingat, tunda mengetik, jam tenang, musik fokus,
  catatan suara, dan warna sampul (total 264).

### Changed

- Versi naik ke `0.5.0-beta`; preferensi Tahap 5b disimpan di `Stage5B` pada
  `settings.json` tanpa mengubah modul Tahap 1-5a.

### Known limitations

- Visualizer musik dan warna sampul album yang sudah dihitung di Core belum
  dipasang ke UI; menunggu tahap berikutnya.

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
