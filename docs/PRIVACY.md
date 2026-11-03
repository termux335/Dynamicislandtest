# Privasi dan Izin

Semua proses berjalan lokal. Tidak ada analitik, telemetri, iklan, atau unggahan
data. Fitur yang butuh internet (belum ada di 0.2.0-beta) harus mati secara
bawaan dan meminta izin saat dinyalakan.

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

## Data yang ditulis

| Berkas | Isi | Lokasi |
|---|---|---|
| `settings.json` | mode tampilan, bahasa, humor, aksen, posisi, prioritas, saklar notifikasi/autostart | `%APPDATA%\DynamicIsland` |
| `dynamic-island.log` | waktu, nama fitur, aksi, dan pesan galat; **bukan** isi notifikasi | `%TEMP%` |
| `.ics` uji | hanya bila skrip uji dijalankan | `%TEMP%` |
| Nilai `DynamicIsland` | path exe, hanya saat autostart dicentang | `HKCU\...\Run` |

Tidak ada isi notifikasi, teks yang diketik, audio mikrofon, gambar kamera, atau
isi clipboard yang ditulis ke disk oleh versi ini.

## Izin

| Fitur | Izin/API | Diminta kapan | Bila ditolak |
|---|---|---|---|
| Notifikasi Windows | `UserNotificationListener` | saat menu Notifikasi dinyalakan | alasan tampil di kapsul dan dicatat di log |
| Bisukan mikrofon | Core Audio (tanpa izin khusus) | saat tombol Bisukan ditekan | status tidak berubah, alasan di log |
| Mulai otomatis | tulis `HKCU\...\Run` | saat centang di Pengaturan | centang kembali mati + alasan |
| Penempatan adaptif | `GetForegroundWindow`/`GetWindowRect` | otomatis saat app jalan | tidak aktif bila gagal |
| Named pipe event | named pipe lokal | saat app mulai | app tetap jalan tanpa event eksternal |

Tidak ada: hak admin, akses jaringan, aksesibilitas, rekaman layar, hook
keyboard/mouse, atau instalasi driver.

## Yang tidak dilakukan

- Tidak merekam atau menyimpan isi ketikan.
- Tidak membaca memori proses lain, menyuntik kode, atau meng-hook DirectX.
- Tidak mengunggah berkas, notifikasi, atau statistik penggunaan.
- Tidak menyimpan gambar kamera (fitur kamera belum ada).
- Tidak menyalin berkas yang ditarik ke shelf; hanya path-nya di memori.

## Menghapus data

- Pengaturan: hapus `%APPDATA%\DynamicIsland` atau jalankan
  `Scripts\uninstall.ps1 -PurgeSettings`.
- Log: hapus `%TEMP%\dynamic-island.log`.
- Autostart: matikan centang di Pengaturan atau jalankan `uninstall.ps1`.
