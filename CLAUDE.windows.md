# Tambahan: Versi Windows (Mode Kocak)

Letakkan file ini di samping `CLAUDE.md` (misalnya sebagai `CLAUDE.windows.md`) dan tambahkan baris `@CLAUDE.windows.md` di `CLAUDE.md` supaya ikut terbaca. Semua prinsip, model data, fitur, dan aturan lisensi di `CLAUDE.md` tetap berlaku. File ini berisi detail teknis Windows dan kepribadian lucu. Proyek ini hanya untuk Windows.

## Platform

- **Windows 10 (build 19041+) dan Windows 11**, folder `windows/`.
- Stack: **C# .NET 8 + WPF**. Alasannya: jendela transparan, selalu di atas, dan bisa tembus klik jauh lebih mudah di WPF daripada di WinUI 3.
- Windows tidak punya notch, jadi **selalu kapsul mengambang** di tengah atas layar. Posisi default: 8 px dari tepi atas, di monitor utama. Bisa dipindah ke monitor lain.

## Hal teknis yang wajib ditangani

- Jendela: tanpa bingkai, `AllowsTransparency`, `Topmost`, tidak muncul di taskbar dan Alt+Tab, `WS_EX_NOACTIVATE` supaya tidak merebut fokus dari aplikasi lain.
- Tembus klik di area kosong. Hanya kapsulnya yang menerima klik.
- Sembunyi otomatis saat ada aplikasi layar penuh (game, presentasi), dengan opsi untuk mematikannya.
- Untuk Mode Tidak Mengganggu dan game, ikuti Tahap 4 di `CLAUDE.md` (termasuk larangan menyuntik atau meng-hook proses game).
- DPI per monitor dan multi-monitor. Uji di 100%, 150%, dan 200%.
- Media: `GlobalSystemMediaTransportControlsSessionManager` (SMTC).
- Notifikasi: `UserNotificationListener` (butuh izin dari pengguna, jelaskan dulu alasannya).
- Baterai dan pengisian daya: `SystemParameters.PowerStatus` dan event power.
- Mic dan kamera aktif: baca status dari registry privasi Windows.
- Unduhan: pantau folder Downloads lewat `FileSystemWatcher`.
- File shelf: seret-lepas file ke kapsul (drag and drop).
- Pintasan global: daftarkan satu hotkey untuk buka/tutup, bisa diubah pengguna.
- Mulai otomatis saat login, bisa dimatikan.
- Hormati pengaturan "Reduce motion" Windows dan mode terang atau gelap.

## Kepribadian: Mode Kocak

Aplikasi punya dua mode di pengaturan: **Mode Serius** dan **Mode Kocak** (default saat pertama kali tapi ditanyakan di onboarding). Semua teks lucu disimpan di satu file string (`strings.id.json`, `strings.en.json`) dan punya padanan serius. Kode tidak boleh menulis teks lucu langsung di UI.

### Contoh teks (bahasa Indonesia)

| Kejadian | Mode Serius | Mode Kocak |
|---|---|---|
| Baterai 20% | Baterai 20% | Baterai 20%. Colokin, kasihan dia. |
| Baterai 5% | Baterai 5% | 5%. Ini bukan latihan. Colokin sekarang. |
| Mulai mengisi | Mengisi daya | Nyedot listrik. Mantap. |
| Timer fokus selesai | Sesi selesai | 25 menit kelar. Minum dulu, jangan scroll. |
| Istirahat selesai | Istirahat selesai | Istirahatnya sudah habis, kopinya jangan. |
| Mic aktif di rapat | Mikrofon aktif | Mic nyala. Hati-hati ngomongin bos. |
| Mute di rapat | Anda dibisukan | Kamu dibisukan. Damai. |
| Unduhan lambat | Mengunduh 42% | 42%. Sabar, bukan cuma kamu yang nunggu. |
| Unduhan selesai | Unduhan selesai | Kelar. Jangan lupa dibuka, bukan cuma diunduh. |
| Bluetooth tersambung | Earbud tersambung | Earbud nyambung. Dunia sunyi lagi. |
| Idle / tidak ada apa-apa | (kapsul kecil) | Mata di kapsul mengikuti kursor |
| Kalender: 5 menit lagi | Rapat dalam 5 menit | Rapat 5 menit lagi. Pura-pura siap. |

### Perilaku kocak (opsional, bisa dimatikan satu per satu)

- **Mata kapsul:** saat idle, dua titik putih kecil mengikuti gerak kursor.
- **Klik berkali-kali:** klik kapsul 5 kali cepat, kapsul bergoyang pegas dan muncul teks "Aduh, geli."
- **Kapsul ngambek:** kalau diabaikan 10 menit saat timer berjalan, teksnya berganti "Halo? Masih hidup?"
- **Lagu berulang:** kalau lagu yang sama diputar 3 kali beruntun, tampil "Lagu ini lagi? Aku sampai hafal."
- **Hitung mundur drama:** 10 detik terakhir timer, kapsul berdenyut makin cepat.
- **Telur paskah:** urutan tombol tertentu membuat kapsul berubah jadi bentuk lain selama 3 detik (isi bebas, tapi aman dan tidak mengganggu).

### Aturan humor (wajib)

1. Peringatan penting tidak boleh dibungkus lelucon. Baterai kritis, kegagalan, dan izin sistem tetap jelas dan serius, humor hanya sebagai tambahan di sampingnya.
2. Tidak ada lelucon soal kelompok orang, agama, suku, fisik, atau kondisi kesehatan.
3. Pembaca layar membaca versi serius, bukan versi lucu.
4. Humor tidak boleh menahan pengguna lebih lama. Teks lucu tetap singkat, maksimal satu baris dan 4 detik.
5. Satu tombol "Matikan semua lelucon" di menu kapsul (klik kanan) selalu tersedia.
6. Jangan menyebut merek atau tokoh nyata dalam lelucon.

## Urutan pengerjaan Windows

1. **W1:** kapsul mengambang, antrean, state minimal/compact/expanded, event palsu. Tes untuk antrean dan state.
2. **W2:** media (SMTC), timer, baterai, dan sistem string dengan Mode Serius/Kocak.
3. **W3:** notifikasi, rapat (mic aktif), unduhan, file shelf, hotkey global.
4. **W4:** perilaku kocak, pengaturan, onboarding, installer (MSIX atau Inno Setup).

Berhenti di akhir tiap tahap dan tunggu konfirmasi.

## Kriteria selesai

- Build Release tanpa peringatan baru.
- Tidak merebut fokus dari aplikasi lain saat muncul.
- Berjalan benar di dua monitor dan tiga skala DPI.
- Semua teks UI berasal dari file string, dan tiap teks kocak punya padanan serius.
- Pembaca layar (Narrator) membaca versi serius.

## Prompt awal (tempel ke Claude Code)

```
Baca CLAUDE.md dan CLAUDE.windows.md. Kita mulai W1 untuk Windows.
1. Buat folder windows/ berisi solusi C# .NET 8 WPF.
2. Buat jendela kapsul transparan, selalu di atas, tidak merebut fokus, tembus klik di luar kapsul, tengah atas monitor utama.
3. Implementasi antrean aktivitas berprioritas dan transisi minimal/compact/expanded dengan animasi pegas, lengkap dengan tes unit.
4. Sediakan sistem string (strings.id.json) dengan varian serius dan kocak, dan tombol tukar mode di menu klik kanan.
5. Buat skrip event palsu untuk media, timer, dan baterai agar bisa diuji manual.
Berhenti setelah W1 berjalan dan tunjukkan ringkasan hasil serta cara menjalankannya.
Jangan salin kode dari proyek GPL; tulis sendiri.
```
