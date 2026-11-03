# Proyek: Dynamic Island untuk Windows

Letakkan file ini di root repo sebagai `CLAUDE.md`, bersama `CLAUDE.windows.md`. Tambahkan baris `@CLAUDE.windows.md` di bawah judul ini supaya keduanya terbaca otomatis oleh Claude Code.

## Tujuan

Bangun "Dynamic Island" khusus **Windows 10/11**: kapsul mengambang di tengah atas layar (Windows tidak punya notch) yang menampilkan musik, timer, rapat, notifikasi, dan aktivitas lain, lalu ciut sendiri saat tidak dibutuhkan.

Platform target: **Windows 10/11** dengan C# .NET 8 + WPF. Android, macOS, iOS, dan Linux **tidak termasuk**. Detail teknis Windows dan kepribadian Mode Kocak ada di `CLAUDE.windows.md`.

## Prinsip

1. Satu aktivitas utama per waktu. Sisanya antre berdasarkan prioritas.
2. Dua jenis event: **Live Activity** (menetap: media, timer, rapat, unduhan) dan **Transient Alert** (hilang sendiri: baterai, Bluetooth, Wi-Fi, VPN, notifikasi).
3. Tiga ukuran: **minimal** (lingkaran kecil), **compact** (kapsul), **expanded** (kartu penuh).
4. Ciut otomatis setelah 4 detik tanpa interaksi. Tidak boleh menutupi konten penting.
5. Privasi: semua diproses lokal, tanpa analitik, tanpa akses internet kecuali fitur yang jelas butuh.
6. Aksesibilitas: label untuk pembaca layar (Narrator), opsi kurangi gerakan, target klik minimal 32 px (44 px untuk layar sentuh).
7. Izin diminta hanya saat fitur terkait diaktifkan, dengan penjelasan singkat.

## Model data

```
Activity { id, kind, priority, state: minimal|compact|expanded, title, subtitle, progress?, actions[] }
Kind: media | timer | call | download | notification | battery | device | calendar
Aturan: aktivitas prioritas tertinggi jadi utama; yang kedua jadi kapsul minimal terpisah.
```

## Spesifikasi tampilan (dari wireframe)

- Expanded: 560 x 176 px, mengambang 8 px dari tepi atas, sudut melengkung penuh (radius 36), latar hitam.
- Compact: 200 x 34 (idle), 260 x 36 (timer), 300 x 36 (rapat).
- Expanded notifikasi: 312 x 84, radius 28. Shelf: 312 x 84 dengan garis putus-putus. Aksi cepat: 312 x 64.

Warna: latar hitam murni, teks putih, teks sekunder abu terang, satu aksen oranye (#ff9f43). Kontras teks minimal 4.5:1.

## Urutan pengerjaan

Urutan teknis ada di `CLAUDE.windows.md` (W1 sampai W4) dan mencakup **Tahap 1**. Setelah W4 selesai, lanjut Tahap 2, 3, 4, dan 5 di bawah. Berhenti di akhir tiap tahap dan tunggu konfirmasi.

**Tahap 1 (Inti)**
- Kontrol musik (putar, jeda, lanjut, progres)
- Timer dan fokus (pomodoro, stopwatch, hitung mundur)
- Panggilan dan rapat (mute, durasi, kembali ke rapat)
- Notifikasi prioritas (balas cepat)

**Tahap 2 (Pelengkap)**
- Acara kalender berikutnya (hitung mundur, tombol gabung)
- Progres live (unduhan, unggahan, salin file)
- Baterai dan perangkat terhubung
- File shelf
- Mode ringkas dan mode standar

**Tahap 3 (Lanjutan)**
- Aksi cepat lewat tekan-tahan, bisa diatur sendiri
- Multi-aktivitas dua kapsul
- Gestur geser untuk buka, tutup, dan lagu berikutnya
- Widget layar kunci
- Kustomisasi (ukuran, posisi, warna, prioritas) dan lokalisasi

## Tahap 4: Pembeda dan Mode Tidak Mengganggu

Dikerjakan setelah Tahap 1 sampai 3 stabil. Tujuannya membuat island ini berbeda dari proyek sejenis.

### A. Fitur pembeda

1. **Mode berbagi layar aman.** Saat berbagi atau merekam layar, isi notifikasi disembunyikan dan hanya tampil "Ada pesan baru".
2. **Aktivitas live khas Indonesia**, masing-masing sebagai modul terpisah yang bisa dimatikan tanpa merusak yang lain:
   - Jadwal dan hitung mundur salat (dihitung lokal, tanpa internet).
   - Peringatan cuaca BMKG (butuh internet, opsional, izin diminta saat diaktifkan).
3. **Pelatih kebiasaan** dengan Mode Kocak: pengingat minum, istirahat mata, dan lama di depan layar. Selalu bisa dimatikan.
4. **Skenario otomatis.** Island berganti tampilan sesuai konteks: rapat (mic dan durasi), belajar (timer dan blokir notifikasi), charging malam (jam dan alarm).

### B. Mode Tidak Mengganggu (sadar konteks)

**Konteks yang dideteksi:** game atau layar penuh, video layar penuh, rapat atau berbagi layar, dan presentasi.

**Tingkat respons** (bisa diatur per konteks dan per aplikasi):
1. Normal
2. Redup dan ciut (kapsul minimal, opacity sekitar 40%)
3. Pindah ke posisi aman
4. Tembus klik
5. Hanya peringatan kritis (baterai sangat rendah, panggilan)
6. Sembunyi total

**Bawaan untuk game:** tembus klik, kapsul minimal yang redup, dan hanya peringatan kritis. Kembali normal sekitar 3 detik setelah keluar dari game.

**Susun ulang otomatis (auto rearrange):**
- Posisi aman dicoba berurutan: tengah atas, kiri atas, kanan atas, tengah bawah.
- Pengguna bisa menandai "zona terlarang" per aplikasi atau game (misalnya area HUD), dan island tidak boleh masuk ke sana.
- Profil per aplikasi menyimpan posisi, ukuran, opacity, dan tingkat respons. Posisi terakhir diingat per game.
- Hindari kursor di desktop (bukan game): kalau kursor mendekat sekitar 80 px, kapsul bergeser atau mengecil, lalu kembali setelah kursor menjauh.

**Kontrol tanpa keluar dari game:** hotkey global untuk jeda atau lanjut musik, lagu berikutnya, dan mute mic, dengan umpan balik singkat di kapsul minimal.

**Pelindung layar OLED:** geser posisi mikro setiap beberapa menit, redup saat idle, dan sembunyi otomatis saat idle lama.

**Teknis:**
- Deteksi layar penuh dan game dengan `SHQueryUserNotificationState`, ditambah daftar proses game milik pengguna (opsional).
- Kapsul hanya jendela biasa yang selalu di atas. **Dilarang** menyuntik kode ke proses game, meng-hook DirectX, atau menyentuh memori proses lain, supaya aman dari anti-cheat.
- Saat tersembunyi atau di dalam game, penggunaan CPU target di bawah 1% (ukur dan catat).

**Kriteria selesai Tahap 4:** tes deteksi konteks dengan event palsu, tes antrean untuk tingkat respons, ukuran CPU tercatat, dan uji dua monitor dengan tiga skala DPI.

## Tahap 5: Fitur Tambahan

Dikerjakan setelah Tahap 4. Setiap fitur adalah **modul terpisah**: bisa dimatikan di pengaturan, meminta izin hanya saat diaktifkan, punya tes sendiri, dan tidak boleh memperlambat modul lain. Kerjakan berurutan 5a, 5b, 5c, lalu berhenti dan tunggu konfirmasi di tiap sub-tahap.

### 5a. Paling berguna sehari-hari

1. **Riwayat clipboard yang aman.** Simpan teks, gambar, dan tautan terakhir dengan pencarian. Teks yang tampak seperti kata sandi, token, atau nomor kartu otomatis disamarkan, dan riwayat dihapus berkala. Ada OCR untuk gambar yang disalin atau di-screenshot.
2. **Command palette.** Tekan hotkey lalu ketik: buka aplikasi, hitung ("15% dari 240 ribu"), konversi satuan, atau jalankan aksi cepat. Kalkulator dan konversi satuan offline. Konversi kurs butuh internet, jadi opsional.
3. **Cermin kamera.** Pratinjau webcam singkat di island untuk mengecek penampilan sebelum rapat, dengan indikator kamera aktif yang jelas. Kamera mati begitu island menciut. Tidak menyimpan gambar.
4. **Pengganti tampilan volume dan kecerahan (OSD).** Tampilan volume, kecerahan, dan mute bawaan sistem diganti kapsul. Wajib bisa dimatikan karena tidak semua perangkat mengizinkan.
5. **Pintasan aplikasi dan folder** yang bisa diatur, ditampilkan sebagai ikon kecil di panel expanded.

### 5b. Produktivitas dan kenyamanan

6. **Catatan suara cepat.** Tekan hotkey, bicara, hasilnya jadi catatan atau pengingat. Utamakan pengenalan suara di perangkat. Kalau memakai layanan online, tampilkan peringatan dan minta persetujuan.
7. **Tunda notifikasi saat mengetik.** Notifikasi non-kritis ditahan selama pengguna aktif mengetik, lalu muncul saat berhenti. Hanya membaca waktu aktivitas input terakhir (misalnya `GetLastInputInfo`). **Dilarang** merekam atau menyimpan isi ketikan. Jelaskan ini di README dan di layar izin.
8. **Catatan dan snippet.** Papan catatan cepat dan potongan teks yang sering dipakai, tersimpan lokal.
9. **Pomodoro dengan musik fokus.** Opsi memutar daftar putar fokus pilihan pengguna saat sesi dimulai. Tidak menyertakan atau mengunduh musik sendiri.
10. **Visualizer musik** dan sampul album yang warnanya ikut lagu.
11. **Jam tenang otomatis.** Kapsul meredup dan hanya peringatan kritis yang lewat pada jam yang ditentukan pengguna.
12. **Aksesibilitas penuh.** Bacakan notifikasi dengan suara, kontras tinggi, teks besar, dan navigasi penuh dengan keyboard.

### 5c. Lanjutan dan ekosistem

13. **Status agen AI coding.** Tampilkan kuota dan status alat seperti Claude Code, serta tombol setuju atau tolak untuk permintaan izin, tanpa pindah jendela. Baca dari data lokal alat tersebut, tanpa meminta kunci API baru.
14. **Terminal dan otomasi cepat.** Panel perintah singkat (hotkey) untuk menjalankan skrip atau perintah favorit. Skrip yang boleh dijalankan harus didaftarkan sendiri oleh pengguna, tidak ada eksekusi dari sumber luar.
15. **Tangkapan layar dan rekam layar.** Tombol cepat dengan indikator rekam di kapsul.
16. **Ticker opsional** (RSS, kurs, harga). Butuh internet, mati secara bawaan.
17. **Asisten AI cepat (opsional).** Kotak tanya singkat di island. Memakai kunci API milik pengguna sendiri, tidak ada data dikirim tanpa persetujuan, dan mati secara bawaan.
18. **SDK plugin.** Antarmuka modul sederhana agar fitur baru bisa ditambahkan tanpa mengubah inti. Plugin berjalan dengan izin terbatas dan harus menyatakan izin yang diminta. Toko tema dan mode komunitas **ditunda** sampai ada mekanisme tinjauan keamanan.

### Aturan khusus Tahap 5

- Jangan menyimpan data sensitif (clipboard, suara, gambar kamera) di luar perangkat tanpa persetujuan eksplisit.
- Tidak ada fitur yang merekam isi ketikan.
- Fitur bergantung internet harus mati secara bawaan dan ditandai jelas.
- Setiap modul punya saklar sendiri dan tidak boleh gagal diam-diam: tampilkan penyebab kalau izin ditolak.

**Kriteria selesai Tahap 5:** tes unit untuk penyamaran data sensitif, tes bahwa modul bisa dimatikan tanpa memengaruhi modul lain, kamera dipastikan mati saat island menciut, dan README mencantumkan izin tiap modul.

## Di luar cakupan (ditunda)

- Versi Android, macOS, iOS, dan Linux.
- Sinkron dengan HP (notifikasi, panggilan, file shelf lintas perangkat). Bisa dipertimbangkan lagi setelah versi Windows stabil.
- Toko tema dan mode komunitas.

## Referensi (hanya untuk dipelajari)

| Proyek | Dipelajari untuk | Lisensi |
|---|---|---|
| Dynamic Island for Windows (devcode90) | Titik privasi mic dan kamera, game overlay, efek blur/akrilik | Cek di repo |
| D-Island (H7gamer/d-island) | Daftar tugas, catatan, morph antara island dan notch | D-Island License 1.0 (baca syaratnya) |
| BrimDeck (MoFeng2223) | Panel kuota alat AI dan pemantau sistem, WPF | Cek di repo |
| DynamicWin Legacy (59xa) | File tray dan widget, C# .NET | Cek di repo |
| Quick Pill (nabil24024004) | Animasi pegas, tema | Cek di repo |
| Atoll (Ebullioscopic/Atoll) | Fitur lengkap, tab, mode ringkas/standar, gestur (macOS) | GPL v3 |
| DynamicNotch (jackson-storm/DynamicNotch) | Animasi pegas, alert sementara (macOS) | GPL v3 |
| Boring Notch (TheBoredTeam/boring.notch) | Cermin kamera, OSD, shelf, riwayat clipboard (macOS) | Cek di repo |

Proyek macOS hanya dipakai sebagai acuan konsep dan perilaku, bukan platform target.

**Aturan lisensi (penting):** jangan menyalin kode dari proyek GPL v3. Pelajari perilaku dan arsitekturnya, lalu tulis implementasi sendiri. Kalau ingin memakai kode proyek mana pun, periksa lisensinya dulu dan tanya saya sebelum menyalin. Jangan memakai repo yang menyertakan iklan.

## Cara kerja Claude Code di repo ini

- Mulai dengan rencana singkat, lalu kerjakan satu tahap sekali jalan. Berhenti di akhir tiap tahap dan tunggu konfirmasi.
- Struktur folder: `windows/` (kode), `docs/` (spesifikasi dan model data).
- Tulis tes untuk antrean prioritas dan transisi state (minimal, compact, expanded) sebelum UI.
- Jangan menambah izin sistem atau dependensi baru tanpa menyebutkan alasannya.
- Jangan memverifikasi dengan membuat screenshot kecuali saya minta.

## Kriteria selesai per tahap

- Build Release berjalan tanpa peringatan baru.
- Tes antrean dan state lulus.
- Setiap fitur punya cara memicu uji manual (misalnya skrip yang mengirim event palsu).
- README singkat menjelaskan cara menjalankan dan izin yang dibutuhkan.

## Prompt awal (tempel ke Claude Code)

```
Baca CLAUDE.md dan CLAUDE.windows.md. Kita mulai W1 untuk Windows.
1. Buat struktur repo (windows/, docs/) dan tulis docs/SPEC.md dari model data di CLAUDE.md.
2. Di windows/, buat solusi C# .NET 8 WPF dengan jendela kapsul transparan, selalu di atas, tidak merebut fokus, tembus klik di luar kapsul, tengah atas monitor utama.
3. Implementasi antrean aktivitas berprioritas dan transisi minimal/compact/expanded dengan animasi pegas, lengkap dengan tes unit.
4. Sediakan sistem string (strings.id.json) dengan varian serius dan kocak, dan tombol tukar mode di menu klik kanan.
5. Buat skrip event palsu untuk media, timer, dan baterai agar bisa diuji manual.
Berhenti setelah W1 berjalan dan tunjukkan ringkasan hasil serta cara menjalankannya.
Jangan salin kode dari proyek GPL; tulis sendiri.
```
