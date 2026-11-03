# Keamanan dan Privasi

Dynamic Island berjalan lokal di Windows dan tidak mengirim data ke mana pun.
Kalau menemukan masalah keamanan atau kebocoran privasi, jangan buka issue
publik. Gunakan **GitHub Security Advisories** di repo ini (tab *Security* →
*Report a vulnerability*) supaya laporan bisa ditangani sebelum diumumkan.

Sertakan:

- versi (terlihat di jendela Pengaturan atau log `%TEMP%\dynamic-island.log`),
- langkah reproduksi,
- apa yang bocor atau bisa dieksekusi,
- dampak yang kamu perkirakan.

## Cakupan

Yang dianggap masalah keamanan:

- eksekusi kode dari luar (event pipe, berkas `.ics`/`.lrc`, tautan rapat),
- penulisan atau pembacaan berkas di luar lokasi yang diumumkan,
- kebocoran isi notifikasi, mikrofon, clipboard, atau kamera,
- eskalasi hak akses di luar HKCU.

Yang bukan masalah keamanan:

- aplikasi yang tidak mendaftar ke Now Playing sehingga tidak terlihat,
- notifikasi yang tidak bisa dibalas dari kapsul,
- keterbatasan API Windows yang sudah dicatat di README.

## Jaminan yang dipegang

- Tanpa telemetri, analitik, atau panggilan jaringan di luar fitur opsional yang
  dinyalakan pengguna sendiri.
- Named pipe hanya lokal dan tidak punya jalur eksekusi perintah: isinya JSON
  event yang divalidasi, bukan perintah shell.
- Log hanya memuat nama aplikasi, aksi, dan pesan galat; isi notifikasi tidak
  ditulis ke log.
- Tidak ada hook keyboard, hook DirectX, atau injeksi ke proses lain.

Detail data yang dibaca dan ditulis ada di `docs/PRIVACY.md`.
