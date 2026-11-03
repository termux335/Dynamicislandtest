# Panduan Kontribusi

Terima kasih sudah tertarik. Proyek ini Windows-only (C# .NET 8 + WPF);
Android, macOS, iOS, dan Linux di luar cakupan.

## Sebelum mengubah kode

1. Baca `shared/SPEC.md`. Model data, aturan antrean, dan ukuran tampilan ada di
   sana; kode mengikuti spec, bukan sebaliknya.
2. Baca `CLAUDE.windows.md` untuk keputusan teknis Windows dan aturan Mode Kocak.
3. Untuk perubahan perilaku, ubah spec dan tes dulu, baru UI.

## Menyiapkan lingkungan

Butuh Windows 10 2004 (build 19041) atau lebih baru dan .NET SDK 8+.

```powershell
cd windows
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project DynamicIsland.App
```

Uji manual memakai event palsu tanpa menyentuh data asli:

```powershell
powershell -File Scripts/fake-events.ps1 all
$env:ISLAND_SETTINGS = "$env:TEMP\island-uji.json"
$env:ISLAND_PIPE = "DynamicIslandEventsTest"
dotnet run --project DynamicIsland.App
```

## Standar kode

- Nullable aktif; jangan menambah `!` tanpa alasan yang jelas.
- Logika yang bisa dites tanpa WPF masuk `DynamicIsland.Core`; UI hanya
  menerjemahkan state ke tampilan.
- Teks yang terlihat pengguna diambil dari `strings-indonesia.json` dan
  `strings-english.json`; kedua bahasa harus punya kunci yang sama (ada tesnya).
  Humor hanya pelengkap: teks penting tetap serius dan pembaca layar selalu
  mendapat versi serius.
- Jangan menambah dependensi, izin sistem, atau akses jaringan tanpa menyebut
  alasannya di deskripsi perubahan.
- Jangan menyalin kode dari proyek berlisensi GPL v3. Pelajari perilakunya, tulis
  implementasi sendiri. Proyek dengan iklan tidak dipakai sebagai acuan.
- Jangan menyuntik kode ke proses lain, meng-hook DirectX, atau membaca memori
  proses lain. Interaksi dengan aplikasi lain hanya lewat API Windows publik.

## Menambah event atau fitur

- Event JSON baru: perbarui `shared/events.md`, `IslandEvent.Parse`, dan
  `Scripts/fake-events.ps1` supaya bisa diuji manual.
- Monitor baru: turunkan dari pola `Dispose` yang ada, jalankan di Dispatcher UI,
  tangkap kegagalan perangkat/izin dan tampilkan sebabnya, dan pastikan CPU saat
  idle tetap rendah (target < 1% saat kapsul tersembunyi).
- Fitur yang butuh internet harus mati secara bawaan, meminta izin saat
  dinyalakan, dan punya saklar sendiri.

## Sebelum mengirim perubahan

Jalankan:

```powershell
cd windows
dotnet build DynamicIsland.slnx -c Release
dotnet test DynamicIsland.slnx -c Release
```

CI menjalankan hal yang sama dengan `CI=true`, yang membuat peringatan analisis
menjadi galat. Pastikan build lokal juga tanpa peringatan baru.

Yang diperiksa saat review:

- [ ] Spec dan tes diperbarui bila perilaku berubah.
- [ ] Tidak ada teks UI yang ditulis langsung di kode tampilan.
- [ ] Tidak ada data sensitif (isi notifikasi, clipboard, suara, gambar kamera)
      yang ditulis ke disk atau dikirim keluar.
- [ ] Perubahan diuji minimal sekali secara manual (sebutkan langkahnya).
- [ ] `CHANGELOG.md` diperbarui untuk perubahan yang terlihat pengguna.

## Lisensi kontribusi

Dengan mengirim perubahan, kamu setuju kontribusi itu dirilis di bawah MIT
License yang sama dengan proyek ini.
