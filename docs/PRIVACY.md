# Privasi dan Izin

Semua proses berjalan lokal. Tidak ada analitik, telemetri, iklan, atau unggahan
otomatis. Fitur yang butuh internet (ticker RSS dan asisten AI) mati secara
bawaan, hanya menghubungi URL yang diisi pengguna, dan meminta izin saat
dinyalakan.

## Data yang dibaca

| Data | Kapan | Disimpan? | Bisa dimatikan? |
|---|---|---|---|
| Sesi Now Playing (judul, artis, posisi) | saat ada pemutar aktif | tidak | tidak perlu — bawaan Windows |
| Aplikasi yang memakai mikrofon + waktu mulai | saat mikrofon dipakai aplikasi lain | tidak | ya, dengan menutup app |
| Status mute mikrofon | saat fitur bisukan dipakai | tidak | ya, tombol hanya mengubah saat ditekan |
| Notifikasi toast Windows (judul, isi, app) | hanya bila fitur dinyalakan | tidak, hanya di memori | ya, saklar menu |
| Baterai, charger, daftar drive lepas | berkala | tidak | tidak perlu |
| Berkas `.ics` dari `ISLAND_ICS` / `%TEMP%` | tiap 30 detik | tidak | ganti/tidak sediakan berkas |
| Berkas `.lrc` dari folder lirik | sekali per lagu | tidak | tidak sediakan berkas |
| Window depan (nama proses, kotak, layar penuh) | sekali per detik | tidak | saklar penempatan otomatis |
| Berkas yang ditarik ke shelf | sampai app ditutup | hanya path di memori | jangan tarik berkas |
| `agent-status.json` (status agen) | tiap 2 detik bila modul menyala | tidak | saklar Status agen AI |
| `commands.json` (perintah terminal) | saat app mulai | tidak | saklar Terminal |
| Feed RSS/Atom | tiap 15 menit bila ticker menyala | hanya judul di memori | saklar Ticker |
| Pertanyaan asisten + kunci API | hanya saat tombol Kirim | kunci hanya di memori | tutup jendela / jangan kirim |
| Layar saat tangkapan layar | hanya saat perintah dipilih | PNG di Pictures | saklar Tangkapan layar |

## Data yang ditulis

| Berkas | Isi | Lokasi |
|---|---|---|
| `settings.json` | mode tampilan, bahasa, humor, aksen, posisi, prioritas, saklar notifikasi/autostart | `%APPDATA%\DynamicIsland` |
| `dynamic-island.log` | waktu, nama fitur, aksi, dan pesan galat; **bukan** isi notifikasi | `%TEMP%` |
| `.ics` uji | hanya bila skrip uji dijalankan | `%TEMP%` |
| Nilai `DynamicIsland` | path exe, hanya saat autostart dicentang | `HKCU\...\Run` |
| `agent-response.json` | id permintaan + setuju/tolak | folder status agen |
| `commands.json` | nama, program, argumen perintah pilihan pengguna | `%APPDATA%\DynamicIsland` |
| `plugins\*\plugin.json` | manifest dan izin plugin pilihan pengguna | `%APPDATA%\DynamicIsland` |
| `capture-*.png` | tangkapan layar yang diminta pengguna | `Pictures\DynamicIsland` |
| `island-hook.ps1` / `island-hook.cmd` | skrip relay Claude Code, hanya bila hook dipasang dari Pengaturan | `%APPDATA%\DynamicIsland` |
| `~/.claude/settings.json` | hanya entri hook `island-hook` (cadangan `.bak` bertanggal dibuat dulu) | folder pengguna |

Tidak ada isi notifikasi, teks yang diketik, audio mikrofon, gambar kamera, atau
isi clipboard yang ditulis ke disk oleh versi ini. Kunci API asisten tidak
ditulis ke disk.

## Izin

| Fitur | Izin/API | Diminta kapan | Bila ditolak |
|---|---|---|---|
| Notifikasi Windows | `UserNotificationListener` | saat menu Notifikasi dinyalakan | alasan tampil di kapsul dan dicatat di log |
| Bisukan mikrofon | Core Audio (tanpa izin khusus) | saat tombol Bisukan ditekan | status tidak berubah, alasan di log |
| Mulai otomatis | tulis `HKCU\...\Run` | saat centang di Pengaturan | centang kembali mati + alasan |
| Penempatan adaptif | `GetForegroundWindow`/`GetWindowRect` | otomatis saat app jalan | tidak aktif bila gagal |
| Named pipe event | named pipe lokal | saat app mulai | app tetap jalan tanpa event eksternal |
| Ticker RSS | HTTPS GET ke URL pilihan | saat ticker dinyalakan | kapsul menampilkan kegagalan |
| Asisten AI | HTTPS ke endpoint pilihan + kunci Bearer | hanya saat Kirim | pesan galat di jendela |
| Tangkapan layar | GDI+ layar | saat perintah dipilih | alasan tampil di kapsul |
| Plugin lokal | muat assembly di folder pengguna | saat saklar dinyalakan | plugin dilewati, log mencatat |
| Hook Claude Code | tulis `~/.claude/settings.json` + skrip relay | hanya saat tombol **Pasang Hook** ditekan | pesan galat di Pengaturan, hook lama tidak berubah |

Tidak ada: hak admin, aksesibilitas, rekaman layar, hook keyboard/mouse, atau
instalasi driver. Jaringan hanya untuk ticker dan asisten, keduanya mati bawaan.

## Yang tidak dilakukan

- Tidak merekam atau menyimpan isi ketikan.
- Tidak membaca memori proses lain, menyuntik kode, atau meng-hook DirectX.
- Tidak mengunggah berkas, notifikasi, atau statistik penggunaan.
- Tidak menyimpan gambar kamera; tangkapan layar hanya saat diminta dan tetap lokal.
- Tidak mengirim pertanyaan asisten atau feed ticker tanpa saklar/aksi pengguna.
- Tidak menyalin berkas yang ditarik ke shelf; hanya path-nya di memori.

## Menghapus data

- Pengaturan dan data modul: hapus `%APPDATA%\DynamicIsland` atau jalankan
  `Scripts\uninstall.ps1 -PurgeSettings`.
- Tangkapan layar: hapus folder `Pictures\DynamicIsland`.
- Log: hapus `%TEMP%\dynamic-island.log`.
- Autostart: matikan centang di Pengaturan atau jalankan `uninstall.ps1`.
