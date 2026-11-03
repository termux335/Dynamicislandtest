# Dynamic Island — Windows (beta, 0.3.0-beta)

Kapsul mengambang di tengah atas layar, 30–480 px tergantung state. Windows tidak
punya notch, jadi kapsul selalu melayang 8 px dari tepi atas.

> **Status: beta publik (0.3.0-beta).** Fitur Tahap 1–4 sudah dipakai harian;
> Tahap 5 belum dikerjakan. Build belum ditandatangani, jadi Windows SmartScreen
> bisa menampilkan peringatan saat pertama dijalankan. Angka versi ada di
> `Directory.Build.props` (satu sumber untuk semua proyek), tampil di kaki jendela
> Pengaturan, dan dicatat di log saat app mulai. Format berkas pengaturan masih
> boleh berubah antar rilis beta.
>
> Proyek ini MIT, punya CI (build + tes + publish) di `.github/workflows/ci.yml`,
> dan rilis bertag menghasilkan zip + checksum lewat `release.yml`.

Tahap 1: overlay, antrean prioritas, transisi state, event palsu, notifikasi
Windows (dengan izin), dan rapat lewat status mikrofon.
Tahap 2: acara kalender berikutnya, progres live (unduhan dan pesanan), baterai,
perangkat terpasang, file shelf, serta mode ringkas/standar.
Tahap 3 (sebagian): dua kapsul + promosi, penempatan adaptif, timer asli,
Now Playing sungguhan, aksi cepat tekan-tahan, gestur gulir, pengaturan
tersimpan, serta kustomisasi (bahasa, Mode Kocak, aksen, posisi, prioritas).

Ditambah: onboarding saat pertama jalan, jendela pengaturan, mulai otomatis
saat login, dan pemasangan tanpa admin lewat `Scripts/install.ps1`.

## Menjalankan

Butuh Windows 10 versi 2004 (build 19041) atau lebih baru dan .NET SDK 8 (atau
lebih baru; proyek menargetkan .NET 8). Build 19041 dipakai untuk API Now
Playing bawaan Windows.

```
cd windows
dotnet build
dotnet test
dotnet run --project DynamicIsland.App
```

### Memasang untuk dipakai sehari-hari

```
powershell -ExecutionPolicy Bypass -File windows\Scripts\install.ps1 -Launch
```

Skrip itu menerbitkan build Release (win-x64) lalu menyalinnya ke
`%LOCALAPPDATA%\Programs\DynamicIsland`, membuat pintasan **Dynamic Island** di
menu Start, dan menutup instance lama yang masih jalan. Tanpa hak admin, tanpa
installer pihak ketiga. Tambahkan `-SelfContained` bila mesin tujuan tidak punya
.NET 8 Desktop Runtime (ukuran jadi sekitar 150 MB).

```
powershell -ExecutionPolicy Bypass -File windows\Scripts\uninstall.ps1            # cabut, simpan pengaturan
powershell -ExecutionPolicy Bypass -File windows\Scripts\uninstall.ps1 -PurgeSettings
```

Tanpa SDK: unduh zip rilis, ekstrak ke folder mana pun, lalu jalankan
`DynamicIsland.App.exe`. Varian `-selfcontained` tidak butuh .NET 8 Desktop
Runtime; varian biasa hanya sekitar 2 MB. Setiap zip punya pasangan `.sha256`
untuk memeriksa unduhan (`Get-FileHash .\DynamicIsland-*.zip -Algorithm SHA256`).

Membuat paket itu sendiri di mesin lokal (tes dijalankan dulu, lalu zip dan
checksum ditulis ke `dist/`):

```
powershell -ExecutionPolicy Bypass -File windows\Scripts\release.ps1
powershell -ExecutionPolicy Bypass -File windows\Scripts\release.ps1 -SelfContained
```

Kapsul langsung muncul dalam bentuk idle (titik oranye kecil). App berjalan tanpa
ikon Dock/taskbar. Klik kanan kapsul untuk menu **Mode tampilan**, **Dua
kapsul**, **Penempatan otomatis**, **Aksi cepat**, **Timer**, **Shelf berkas**,
dan **Keluar**.

Demo tanpa skrip: `dotnet run --project DynamicIsland.App -- --demo`
atau set `ISLAND_DEMO=1`. Dua kapsul bisa langsung dinyalakan dengan
`ISLAND_SECONDARY=1` (sama dengan mencentang **Dua kapsul** di menu).

## Tahap 4: mode tidak mengganggu

Kapsul sadar konteks dan bisa menyingkir sendiri. Deteksi memakai
`SHQueryUserNotificationState`, window depan, dan status mikrofon — **tanpa**
injeksi proses, DLL ke game, atau hook DirectX, jadi aman dari anti-cheat.

| Konteks | Terdeteksi dari | Bawaan |
|---|---|---|
| Game layar penuh | Window depan menutup layar + proses game, atau status D3D Windows | Redup 40%, kapsul minimal, tembus klik, hanya peringatan kritis. Kembali normal 3 detik setelah keluar |
| Video layar penuh | Peramban F11 atau status `QUNS_BUSY` | Redup dan ciut |
| Rapat | Mikrofon dipakai aplikasi lain | Redup dan ciut, tombol mic tetap berfungsi |
| Berbagi layar | Saklar menu **Mode berbagi layar aman** atau `ISLAND_SCREEN_SHARE=1` | Isi notifikasi disembunyikan; hanya “Ada pesan baru” |
| Presentasi | Mode presentasi Windows | Redup, isi notifikasi disembunyikan |

Tingkat respons bisa diganti per konteks dan per aplikasi di `settings.json`
(`Context`), termasuk posisi aman, opacity, dan mode tampilan. Zona terlarang
per game dan posisi terakhir disimpan di
`%APPDATA%\DynamicIsland\app-profiles.json`.

Penempatan aman mencoba **tengah atas → kiri atas → kanan atas → tengah bawah**,
melewati zona terlarang, dan bergeser maksimal 48 px saat kursor mendekat 80 px.

### Uji manual konteks

```powershell
$env:ISLAND_CONTEXT = 'game'      # game | video | meeting | share | presentation | normal
$env:ISLAND_SCREEN_SHARE = '1'    # paksa mode berbagi layar aman
```

Konteks dipoll tiap 2 detik; game ditahan 3 detik setelah keluar. Semua transisi
dicatat di `%TEMP%\dynamic-island.log`.

### Kontrol tanpa keluar dari game

| Pintasan | Aksi |
|---|---|
| `Ctrl+Alt+Spasi` | Putar/jeda musik |
| `Ctrl+Alt+N` | Lagu berikutnya |
| `Ctrl+Alt+M` | Bisukan/nyalakan mikrofon |

Umpan balik singkat muncul di kapsul. Jika Windows menolak mendaftarkan
pintasan (sudah dipakai app lain), alasannya masuk log dan app tetap jalan.

### Modul Tahap 4

| Modul | Saklar | Catatan |
|---|---|---|
| Mode tidak mengganggu | Menu klik kanan / Pengaturan | Bisa dimatikan; kapsul kembali normal seketika |
| Berbagi layar aman | Menu klik kanan / Pengaturan | Deteksi berbagi layar manual karena Windows tidak punya API publik untuk itu |
| Jadwal salat | Pengaturan | Dihitung lokal (Subuh -20°, Isya -18°), tanpa internet; koordinat Jakarta |
| Cuaca BMKG | Pengaturan (izin internet diminta saat dinyalakan) | **Mati bawaan**; butuh kode `WeatherAdm4` di `settings.json` |
| Pengingat kebiasaan | Pengaturan | Minum 45 menit, istirahat mata 20 menit, layar 2 jam; varian Mode Kocak |
| Skenario otomatis | Otomatis | Rapat (mic + durasi), belajar (tahan notifikasi, saklar menu), charging malam (jam) |
| Pelindung OLED | Pengaturan | Geser 2 px tiap 4 menit, redup setelah idle 60 detik, sembunyi setelah 10 menit |

### Pemakaian CPU

Di dalam game atau saat tersembunyi, target di bawah 1% CPU. Ukur dengan build
yang sedang berjalan:

```powershell
powershell -ExecutionPolicy Bypass -File windows\Scripts\measure-cpu.ps1 -Seconds 60
```

### Izin dan data Tahap 4

- Status notifikasi Windows (`SHQueryUserNotificationState`) dan window depan:
  tanpa izin, tanpa membaca isi aplikasi lain.
- `GetLastInputInfo` untuk pelindung OLED: hanya **waktu** input terakhir, bukan
  isi ketikan atau tombol.
- Jadwal salat: koordinat bawaan Jakarta, dihitung di perangkat, tanpa jaringan.
- Cuaca BMKG: satu permintaan HTTPS ke `api.bmkg.go.id` saat modul dinyalakan;
  mati secara bawaan dan bisa dimatikan lagi kapan saja.
- Profil aplikasi: hanya nama proses, posisi, opacity, dan tingkat respons.

## Fitur

| Fitur | Cara menyalakan | Yang tampil |
|---|---|---|
| Mode ringkas | Klik kanan → **Mode tampilan → Ringkas** | Ukuran lebih kecil di semua state: expanded 400 x 128, compact 168 x 30 (timer 220 x 32, rapat 252 x 32), notifikasi 272 x 72, minimal 30 x 30 |
| Kalender berikutnya | Berkas `.ics` lokal (lihat di bawah) | Judul acara, hitung mundur (`mulai dalam 15 menit`), tombol **Gabung** bila acara punya tautan |
| Progres live | Event palsu `download`/`order` | Persen dan garis progres di kapsul, garis penuh di kartu expanded |
| Baterai | Otomatis, tanpa izin | Alert 4 detik saat baterai melewati batas 15% atau saat charger dicolok |
| Perangkat terpasang | Otomatis, tanpa izin | Alert 4 detik saat daftar volume yang bisa dilepas berubah |
| File shelf | Klik kanan → **Shelf berkas** | Area putus-putus 312 x 84 tempat menaruh berkas (maks. 12); klik nama berkas untuk membukanya |
| Dua kapsul (Tahap 3) | Klik kanan → **Dua kapsul** | Aktivitas kedua tampil sebagai kapsul minimal 34 x 34 di kanan kapsul utama (jarak 8 px); klik kapsul kecil itu untuk menjadikannya utama |
| Penempatan otomatis (Tahap 3) | Otomatis; saklar **Penempatan otomatis** di menu klik kanan | Saat game layar penuh atau peramban maximized di depan, kapsul pindah ke kiri tengah dengan jarak 8 px supaya tidak menutupi tab, bilah alamat, atau HUD; kembali ke tengah atas saat window itu tidak lagi menutup layar |
| Timer asli (Tahap 3) | Klik kanan → **Timer** → **Fokus 25 menit** / **Stopwatch** | Hitung mundur berjalan sungguhan di kapsul, tombol **Jeda**/**Lanjut** dan **Ulangi** di kartu expanded, lalu alert **Timer selesai** 4 detik |
| Now Playing sungguhan (Tahap 3) | Otomatis; tanpa izin | Judul, artis, dan progres dari pemutar yang sedang aktif |
| Kontrol media di kartu (baru) | Klik kapsul saat lagu/video tampil | Kartu media 480 x 152: judul + artis, **baris lirik** (bila ada), garis timeline dengan waktu berjalan/total, dan tiga tombol ikon bundar 40 x 32 — sebelumnya, putar/jeda, berikutnya. Tombol memakai glyph Segoe MDL2 Assets dan label bahasa yang aktif |
| Lirik lokal (baru) | Taruh `Artis - Judul.lrc` di `%APPDATA%\DynamicIsland\lyrics` (atau folder `ISLAND_LYRICS`) | Baris lirik yang cocok dengan posisi lagu tampil di bawah judul dan berganti sendiri. Tanpa berkas .lrc, baris itu tidak ditampilkan dan tidak ada unduhan apa pun |
| Aksi cepat (Tahap 3) | Tekan-tahan kapsul 500 ms, atau menu **Aksi cepat** | Panel 312 x 64 berisi **Putar/jeda**, **Berikutnya**, dan **Fokus 25** |
| Gestur gulir (Tahap 3) | Gulir saat kursor di atas kapsul | Gulir ke atas = lagu sebelumnya, ke bawah = lagu berikutnya |
| Pengaturan tersimpan (Tahap 3) | Otomatis | Mode tampilan, dua kapsul, penempatan otomatis, bahasa, Mode Kocak, aksen, posisi, prioritas, dan saklar notifikasi disimpan di `%APPDATA%\DynamicIsland\settings.json` dan dipakai lagi saat app dibuka |
| Bahasa (Tahap 3) | Klik kanan → **Bahasa → Indonesia/English** | Seluruh menu, label tombol, rak berkas, dan teks pembaca layar berganti bahasa; teks alert (baterai, timer, kalender) ikut berganti pada kejadian berikutnya |
| Mode Kocak (Tahap 3) | Klik kanan → **Mode Kocak** | Nama menu dan teks alert memakai varian kocak (`menu.quit` → "Pamit", baterai lemah → "20%. Colokin, kasihan dia."). Pembaca layar tetap membaca versi serius |
| Aksen warna (Tahap 3) | Klik kanan → **Aksen warna** | Oranye (bawaan), Biru, Ungu, atau Hijau; berlaku langsung ke titik idle, glyph, garis progres, dan fokus tombol |
| Posisi (Tahap 3) | Klik kanan → **Posisi** | Tengah atas (bawaan), kiri atas, atau kanan atas, 8 px dari tepi. Penempatan adaptif tetap memindahkan kapsul ke kiri tengah saat game layar penuh atau peramban maximized |
| Prioritas (Tahap 3) | Klik kanan → **Prioritas** | **Bawaan**, **Musik utama**, atau **Timer utama**: jenis yang dipilih naik ke prioritas 95 sehingga jadi aktivitas utama, tanpa mengubah prioritas jenis lain |
| Rapat & mikrofon (Tahap 1) | Otomatis; tanpa izin | Selama aplikasi lain memakai mikrofon, kapsul menampilkan nama aplikasi dan durasi (`Teams · Mikrofon aktif · 05:12`). Tombol **Bisukan/Nyalakan** mengubah mikrofon bawaan lewat Core Audio, **Kembali** membawa jendela aplikasi itu ke depan |
| Notifikasi Windows (Tahap 1) | Klik kanan → **Notifikasi Windows** (izin diminta saat itu) | Notifikasi toast terbaru tampil 4 detik dengan judul, isi, dan tombol **Tandai dibaca**. Bila izin ditolak atau API tidak tersedia, alasannya muncul di kapsul |
| Onboarding (baru) | Otomatis saat app pertama kali jalan (belum ada `settings.json`) | Jendela pengaturan awal: bahasa, posisi, aksen, Mode Kocak, dan mulai otomatis. Semua perubahan langsung terlihat di kapsul; menutup jendela menyimpan berkas pengaturan supaya onboarding tidak muncul lagi |
| Pengaturan (baru) | Klik kanan → **Pengaturan…** | Jendela yang sama untuk diubah kapan saja, tanpa perlu mengingat menu klik kanan |
| Mulai otomatis saat login (baru) | Jendela pengaturan → **Mulai otomatis saat login** | Satu nilai `DynamicIsland` di `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Bila registry menolak, kotak centang kembali mati dan alasannya tampil di bawahnya |

### Kalender `.ics`

Path dibaca dari variabel lingkungan `ISLAND_ICS`; tanpa itu app memakai
`%TEMP%\dynamic-island.ics`. Berkas dibaca ulang setiap 30 detik. Format yang
didukung: `SUMMARY`, `DTSTART`, `DTEND`, `DESCRIPTION`, `LOCATION`, `UID`
(waktu UTC `...Z`, waktu lokal, dan tanggal saja), termasuk baris terlipat.

Tautan pertemuan diambil dari `DESCRIPTION` lalu `LOCATION`; tombol **Gabung**
membuka tautan itu di browser. Berkas dibaca lokal, tanpa jaringan.

### Lirik `.lrc`

Lirik tidak diunduh: app hanya membaca berkas yang kamu taruh sendiri di
`%APPDATA%\DynamicIsland\lyrics` (bisa dialihkan dengan `ISLAND_LYRICS`). Nama
berkas yang dicoba: `Artis - Judul.lrc`, lalu `Judul.lrc`, tanpa membedakan
huruf besar-kecil. Format LRC yang dibaca: `[mm:ss]`, `[mm:ss.xx]`,
`[mm:ss.xxx]`, beberapa tag pada satu baris; tag metadata seperti `[ar:...]`
dilewati. Berkas dibaca sekali per lagu, bukan tiap detik.

## Menguji manual

```
cd windows
powershell -File Scripts/fake-events.ps1 all      # media, timer, notifikasi, rapat, unduhan
powershell -File Scripts/fake-events.ps1 call     # hanya rapat
powershell -File Scripts/fake-events.ps1 notify   # notifikasi hilang setelah 4 detik
powershell -File Scripts/fake-events.ps1 ics      # kalender uji: acara mulai 15 menit lagi
powershell -File Scripts/fake-events.ps1 ics-clear
powershell -File Scripts/fake-events.ps1 raw '{"op":"clearAll"}'
```

Klik kapsul untuk membuka (compact → expanded). Klik lagi, tekan Esc, atau tunggu
4 detik untuk ciut kembali. Enter/Spasi juga membuka dan menutup.

Skenario uji:

1. Jalankan `fake-events.ps1 ics`, tunggu ≤ 30 detik → kapsul menampilkan
   **Rapat Produk**, `mulai dalam 15 menit`, dan tombol **Gabung**. Jalankan
   `ics-clear` → kapsul kalender hilang pada pembacaan berikutnya.
2. Klik kanan → **Mode tampilan → Ringkas** → kapsul mengecil di semua state;
   ganti kembali ke **Standar** untuk ukuran spec.
3. Cabut/colok charger → alert **Mengisi daya** muncul 4 detik.
4. Colok lalu cabut USB flashdisk → alert **N perangkat terhubung** muncul dan
   hilang sendiri.
5. Klik kanan → **Shelf berkas**, tarik 1–2 berkas ke kapsul → nama berkas
   tampil; klik nama berkas untuk membukanya; **Kosongkan shelf** menghapus isi.
6. Kirim `fake-events.ps1 order` → kartu pesanan dengan progres 60%.
7. Klik kanan → **Dua kapsul**, lalu kirim `timer` saat media tampil → muncul
   kapsul minimal kedua di kanan. Klik kapsul kecil itu → media pindah jadi
   utama dan timer turun ke kapsul kedua. Matikan lagi → kapsul kedua hilang.
8. Buka game layar penuh (atau Chrome/Edge/Firefox yang di-maximize) → kapsul
   bergeser ke kiri tengah dalam ≤ 1 detik. Alt+Tab ke desktop → kapsul kembali
   ke tengah atas. Matikan **Penempatan otomatis** untuk mengunci posisi.
9. Klik kanan → **Timer → Fokus 25 menit** → kapsul menampilkan `25:00` dan
   berjalan mundur tiap detik; klik untuk membuka → tombol **Jeda** dan
   **Ulangi**. Biarkan sampai nol → alert **Timer selesai** muncul 4 detik.
   **Stopwatch** menghitung naik tanpa garis progres.
10. Putar lagu di Spotify/browser/player apa pun → kapsul menampilkan judul dan
    artis; tombol **Jeda/Berikutnya** di kartu expanded mengendalikan pemutar
    itu. Tanpa sesi aktif, kapsul media tidak muncul.
11. Tekan-tahan kapsul 500 ms → panel aksi cepat 312 x 64; klik di luar atau
    Esc menutupnya. Menu **Aksi cepat** memberi jalan yang sama untuk keyboard.
12. Gulir di atas kapsul → lagu berikutnya/sebelumnya, sama seperti tombol di
    kartu expanded.
13. Ubah mode tampilan atau dua kapsul, tutup app, buka lagi → pilihan tadi
    masih terpakai (berkas pengaturan di `%APPDATA%\DynamicIsland`).
14. Klik kanan → **Bahasa → English** → semua menu jadi bahasa Inggris; ganti
    balik ke **Indonesia**.
15. Klik kanan → **Mode Kocak** (mati) → menu kembali serius; nyalakan lagi →
    teks kocak muncul lagi. Kirim `fake-events.ps1 notify` pada kedua mode untuk
    melihat varian serius dan kocak pada teks alert.
16. Klik kanan → **Aksen warna → Ungu** → titik idle, glyph, dan garis progres
    ikut ungu tanpa restart; pilih **Oranye** untuk kembali ke spesifikasi.
17. Klik kanan → **Posisi → Kanan atas** → kapsul pindah ke pojok kanan atas;
    lalu **Prioritas → Musik utama** dan kirim `fake-events.ps1 all` → kapsul
    media menjadi utama meski timer punya prioritas lebih tinggi.
18. Buka aplikasi yang memakai mikrofon (Teams, Zoom, Voice Recorder) dan
    bicara sebentar → dalam ≤ 5 detik kapsul menampilkan `Teams · Mikrofon
    aktif · 00:12` berjalan naik. Klik tombol **Bisukan** → subjudul berganti
    `Mikrofon dibisukan`, dan indikator mic di aplikasi rapat ikut mati. Klik
    **Nyalakan** untuk mengembalikannya. **Kembali** membuka jendela aplikasi
    rapat bila nama prosesnya sama dengan nama aplikasi.
19. Klik kanan → **Notifikasi Windows** → Windows bertanya izin. Setelah
    diizinkan, kirim notifikasi apa pun → kapsul menampilkannya 4 detik; klik
    **Tandai dibaca** untuk membuangnya dari pusat notifikasi. Bila izin
    ditolak, kapsul menampilkan alasannya, bukan diam saja.
20. Untuk uji tanpa membuka aplikasi rapat: `ISLAND_NOTIFICATIONS=1` menyalakan
    pemantau notifikasi seperti mencentang menunya (hasil izin dicatat di log).
21. Hapus/pindahkan sementara `%APPDATA%\DynamicIsland\settings.json`, jalankan
    app → jendela **Halo! Ini Dynamic Island** muncul di tengah layar. Tutup →
    berkas pengaturan dibuat, dan app berikutnya tidak onboarding lagi. Untuk uji
    dengan berkas terpisah tanpa menyentuh pengaturan asli, set
    `ISLAND_SETTINGS=%TEMP%\island-uji.json`.
22. Jendela pengaturan → centang **Mulai otomatis saat login** → nilai
    `DynamicIsland` muncul di `HKCU\...\Run` dan menunjuk exe yang sedang jalan;
    hilangkan centang → nilainya hilang.
23. Uji autostart tanpa klik UI: jalankan app dengan `ISLAND_AUTOSTART_CHECK=1`,
    lalu lihat log — status dibaca, ditulis, dibaca ulang, dan dipulihkan.
24. DPI/multi-monitor: setiap kali mulai, log mencatat satu baris per monitor
    (resolusi, area kerja, DPI efektif, primer) dan ukuran kapsul dalam DIP pada
    skala jendela itu (mis. `dpi 120x120 (125%)` dan `kapsul: 34x34 DIP`).
25. Putar lagu, klik kapsul → kartu media 480 x 152 berisi judul, artis, garis
    timeline dengan `0:42` / `3:21`, serta tombol **sebelumnya**, **putar/jeda**,
    dan **berikutnya** yang mengendalikan pemutar. Klik lagi atau Esc menutup.
26. Taruh `Artis - Judul.lrc` di `%APPDATA%\DynamicIsland\lyrics`, putar lagu
    yang namanya cocok → baris lirik muncul di bawah judul dan berganti mengikuti
    posisi lagu. Log mencatat `lirik: <berkas> (<n> baris)` saat berkas terbaca.
27. Klik kanan → **Shelf berkas** saat kartu media terbuka → isi kartu
    disembunyikan dan hanya area shelf yang tampil; tutup shelf untuk kembali.

Semua pesan (termasuk aksi yang ditekan) dicatat di `%TEMP%\dynamic-island.log`.

## Izin yang dibutuhkan

Tidak ada izin admin, jaringan, aksesibilitas, atau rekaman layar. Named pipe
hanya lokal di mesin yang sama. Tiga hal yang menyentuh sistem:

- **Status mikrofon**: app hanya membaca registry privasi Windows
  (`CapabilityAccessManager\ConsentStore\microphone`) untuk tahu aplikasi mana
yang sedang memakai mikrofon, namanya, dan kapan mulai. Isi suara tidak pernah
dibaca, tidak direkam, dan tidak disimpan.
- **Bisukan mikrofon**: tombol Bisukan/Nyalakan mengubah status mute mikrofon
  bawaan lewat Core Audio, hanya selama tombol itu ditekan. Tidak ada perekaman.
- **Notifikasi Windows**: memakai `UserNotificationListener` yang butuh izin
  eksplisit. Izin hanya diminta saat fitur dinyalakan dari menu, dan isi
  notifikasi hanya dibaca di memori (tidak ditulis ke cakram, tidak dikirim ke
  mana pun). Bila izin ditolak, app menampilkan sebabnya.
- **Mulai otomatis**: satu nilai di `HKCU\...\Run` yang menunjuk exe app, hanya
  ditulis saat kamu mencentangnya (tidak oleh installer). Mencabutnya menghapus
  nilai yang sama; `uninstall.ps1` juga membersihkannya.

Window dibuat transparan,
selalu di atas, tanpa fokus saat muncul (`ShowActivated="False"`). Tahap 2 hanya
membaca path `.ics` (dari `ISLAND_ICS` atau `%TEMP%`), status daya lewat
`GetSystemPowerStatus`, daftar drive, dan berkas yang sengaja ditarik ke shelf.
Shelf menyimpan path di memori saja, tidak menyalin atau mengunggah berkas.
Untuk penempatan adaptif, app hanya membaca nama proses dan kotak window yang
sedang fokus lewat `GetForegroundWindow`/`GetWindowRect` (tanpa hook keyboard
atau mouse, tanpa hak admin), sekali per detik. Now Playing dibaca lewat API
Windows `SystemMediaTransportControls` untuk pemutar yang sedang aktif; app
tidak merekam audio. Satu-satunya berkas yang ditulis app adalah pengaturan di
`%APPDATA%\DynamicIsland\settings.json`.

## Isi folder

- `DynamicIsland.Core` — model, antrean prioritas, state machine, parser event,
  metrik, mode tampilan, aturan baterai/perangkat, rencana kalender, pembaca
  `.ics`, mesin timer, aturan penempatan, kustomisasi (aksen/posisi/prioritas),
  parser lirik LRC (`Lyrics.cs`), katalog string (`strings-indonesia.json`,
  `strings-english.json`), dan penyimpanan pengaturan. Bebas UI.
- `DynamicIsland.Core.Tests` — tes antrean, transisi state, parser, controller,
  metrik, mode tampilan, baterai/perangkat, kalender, `.ics`, timer, penempatan,
  pengaturan, katalog string (kedua bahasa), dan kustomisasi.
- `DynamicIsland.App` — window WPF, tampilan kapsul, animasi pegas, named pipe,
  demo, monitor baterai/drive/kalender/media/timer, pemantau window depan,
  shelf berkas, panel aksi cepat, tema aksen (`Theme.cs`), monitor rapat
  (`MicrophoneMonitor`), mute mikrofon (`MicrophoneMute`), pemantau notifikasi
  (`NotificationMonitor`), jendela onboarding/pengaturan (`SettingsWindow`),
  autostart (`StartupRegistration`), log monitor/DPI (`DisplayInfo`), dan pembaca
  lirik lokal (`LyricsLibrary`).
- `Scripts/fake-events.ps1` — kirim event dan tulis kalender uji.
- `Scripts/install.ps1`, `Scripts/uninstall.ps1` — pasang dan cabut untuk pengguna
  saat ini, tanpa admin.

## Teks, bahasa, dan kepribadian

Semua teks UI dan alert berasal dari `DynamicIsland.Core/strings-*.json`; kode
tidak menulis teks kocak langsung di tampilan. Tiap kunci punya varian `serius`
dan `kocak`; bila varian kocak tidak ada, versi serius yang dipakai. Kunci yang
benar-benar tidak ditemukan ditampilkan apa adanya supaya tidak hilang diam-diam.

Aturan yang dipegang: teks penting (baterai lemah, kegagalan, izin) tetap serius
dan humor hanya tambahan; pembaca layar selalu membaca versi serius; satu tombol
**Mode Kocak** di menu klik kanan selalu bisa mematikannya. Mode Kocak menyala
secara bawaan sesuai `CLAUDE.windows.md`.

## Batasan

- Notifikasi hanya bisa ditandai dibaca, belum bisa dibalas langsung dari
  kapsul (balas cepat butuh UI input teks dan dukungan tombol balasan dari app
  pengirim). Izin `UserNotificationListener` bisa ditolak pengguna atau tidak
  didukung di sebagian mesin; app menampilkan alasannya.
- Rapat dikenali dari pemakaian mikrofon, bukan dari aplikasi rapat tertentu:
  perekam suara biasa juga dianggap rapat. **Kembali** hanya bekerja bila nama
  proses aplikasi sama dengan nama yang tercatat di registry; kalau tidak, app
  hanya mencatat kegagalan di log.
- **Bisukan** mengubah mikrofon bawaan seluruh sistem (bukan hanya satu app),
  jadi mikrofon mati untuk semua aplikasi selama tombol itu aktif. Tombol
  aksi lain hanya dicatat ke log, kecuali **Gabung** pada acara kalender dan
  aksi media/timer.
- Media memakai sesi Now Playing Windows: pemutar yang tidak mendaftar ke
  SystemMediaTransportControls (mis. app lama) tidak terlihat.
- Timer asli berjalan di dalam app (bukan timer sistem), jadi ikut berhenti
  bila app ditutup, dan hanya satu timer yang aktif pada satu waktu.
- Aksi cepat masih tetap tiga tombol; daftarnya belum bisa diatur pengguna.
- Progress media dihitung dari posisi yang dilaporkan pemutar, jadi bisa
  meleset sedikit pada pemutar yang jarang memperbarui posisi.
- Lirik hanya dari berkas `.lrc` lokal yang namanya cocok dengan artis/judul;
  Windows tidak menyediakan API lirik, jadi tidak ada lirik otomatis. Video di
  YouTube juga jarang punya judul yang cocok dengan nama berkas.
- Kontrol media mengikuti kemampuan pemutar: Sebelumnya/Berikutnya bisa ditolak
  pemutar tertentu (mis. video tunggal), dan penolakan itu dicatat di log.
- Ukuran kartu expanded sekarang 480 x 152 (ringkas 400 x 128), lebih kecil dari
  wireframe awal 560 x 176 supaya tidak menutupi konten; `shared/SPEC.md` sudah
  disesuaikan. Tim tidak mengecilkan kartu notifikasi (tetap 312 x 84).
- Kalender hanya membaca berkas `.ics` lokal; kalender Windows/Outlook belum
  dibaca, dan tidak ada pengingat atau acara berulang yang dihitung sendiri.
- "Perangkat terhubung" di laptop berarti volume yang bisa dilepas (USB, kartu
  SD). Bluetooth butuh WinRT dan izin app model, jadi belum dipakai.
- Progres live baru dari event palsu: belum ada pemantau unduhan sistem.
- Shelf menyimpan path di memori (maks. 12), tidak bertahan setelah app ditutup.
- Tahap 3 baru sebagian: kapsul kedua sudah bisa diketuk untuk dipromosikan dan
  penempatan adaptif sudah jalan, tetapi belum ada sinkronisasi dengan HP.
- Penempatan adaptif hanya mengenali peramban dari nama proses (Chrome, Edge,
  Firefox, Brave, Opera, Vivaldi, Chromium, Arc); peramban lain diperlakukan
  sebagai aplikasi biasa. Game dikenali dari window layar penuh, bukan dari
  daftar judul, jadi presentasi layar penuh juga ikut dianggap game.
- Posisi kiri tengah diperhitungkan dari work area layar utama; monitor kedua
  belum dipakai.
- Gestur geser (drag) belum ada; laptop memakai gulir untuk pindah lagu.
- Kustomisasi baru mencakup aksen, posisi, prioritas, bahasa, dan Mode Kocak;
  ukuran kapsul per state belum bisa diatur sendiri.
- Widget layar kunci tidak mungkin untuk app Win32 biasa (dibatasi oleh Windows),
  dan sinkronisasi dengan HP belum ada.
- Pemasangan memakai skrip PowerShell, bukan MSIX/installer bertanda tangan:
  mesin ini tidak punya Windows SDK (`makeappx`) maupun sertifikat, jadi paket
  MSIX belum bisa dibuat dan diuji di sini.
- Uji DPI hanya bisa dijalankan pada konfigurasi mesin ini (satu monitor 1920x1080
  pada 125%); jalur dua monitor dan tiga skala belum diukur di mesin lain.

## Lisensi

MIT (lihat `LICENSE` di akar repo). Tidak ada iklan dan tidak ada kode dari
proyek GPL v3. Aturan kontribusi ada di `CONTRIBUTING.md`, kebijakan privasi di
`docs/PRIVACY.md`, dan arsitektur di `docs/ARCHITECTURE.md`.
