# Spesifikasi Bersama: Dynamic Island

Dokumen ini adalah sumber kebenaran untuk model data, aturan antrean, dan
spesifikasi tampilan. Implementasi ada di `windows/`; bila model berubah, ubah
dokumen ini dulu.

## 1. Model data

```
Activity {
  id: string            // unik, dibuat pemanggil
  kind: Kind            // lihat di bawah
  priority: Int         // 0..100, makin besar makin penting
  state: ActivityState  // minimal | compact | expanded
  title: string
  subtitle: string?
  progress: Double?     // 0.0..1.0, hanya untuk kind yang punya progres
  actions: [Action]
  isLive: Bool          // true = Live Activity, false = Transient Alert
  expiresAt: Instant?   // hanya untuk Transient Alert
}

Kind: media | timer | call | download | order | notification | battery | device | calendar

Action {
  id: string
  label: string         // teks yang dibaca screen reader
  glyph: string         // nama ikon, dipetakan per platform
}
```

### Prioritas bawaan per Kind

Nilai ini dipakai bila pemanggil tidak memberi prioritas eksplisit.

| Kind         | Prioritas | Jenis     |
|--------------|-----------|-----------|
| call         | 100       | Live      |
| timer        | 80        | Live      |
| media        | 70        | Live      |
| order        | 60        | Live      |
| download     | 50        | Live      |
| calendar     | 40        | Live      |
| notification | 30        | Transient |
| device       | 20        | Transient |
| battery      | 10        | Transient |

## 2. Dua jenis event

- **Live Activity**: menetap sampai pemanggil mencabutnya. Contoh: media,
  timer, panggilan, unduhan, pesanan.
- **Transient Alert**: tampil sebentar lalu hilang sendiri. Umur bawaan 4 detik,
  bisa diperpanjang lewat `expiresAt`. Contoh: baterai, Bluetooth, Wi-Fi, VPN,
  notifikasi.

## 3. Aturan antrean

1. Hanya satu aktivitas menjadi **utama** pada satu waktu.
2. Aktivitas utama = aktivitas dengan prioritas tertinggi. Bila prioritas sama,
   yang lebih dulu masuk menang (FIFO).
3. Aktivitas di urutan kedua (bila ada) tampil sebagai **kapsul minimal
   terpisah** di sebelah kanan kapsul utama, dengan jarak 8 px/dp. Hanya tampil
   bila fitur dua kapsul dinyalakan (Tahap 3); sebelum itu ia tetap di antrean
   tanpa tampil. Kapsul kedua menerima ketuk untuk mempromosikan aktivitasnya
   (aturan 8); grup kapsul tetap di tengah layar. Ukurannya mengikuti state
   minimal platform masing-masing.
4. Aktivitas dengan `id` yang sama menggantikan yang lama di tempat (tidak
   mengubah urutan FIFO).
5. Mencabut aktivitas utama mempromosikan aktivitas berikutnya.
6. Transient Alert yang kedaluwarsa dicabut otomatis, baik sedang tampil
   maupun sedang menunggu.
7. Antrean stabil: urutan aktivitas yang tidak berubah tidak boleh berubah
   setelah operasi apa pun.
8. **Promosi pengguna**: mengetuk kapsul kedua menjadikan aktivitas itu utama
   dan menampilkannya sebagai `compact`. Nilai prioritas tidak diubah; urutan
   hanya ditimpa selama promosi berlaku. Promosi berakhir bila aktivitas itu
   dicabut, `ClearAll` dipanggil, atau ada aktivitas masuk dengan prioritas lebih
   tinggi daripada prioritas tertinggi saat promosi dibuat — supaya panggilan
   atau event mendesak tidak tertahan di bawah. Promosi hanya berlaku saat
   fitur dua kapsul aktif.

## 4. Transisi state

```
minimal <-> compact <-> expanded
```

- State awal saat aktivitas masuk: `compact`.
- **Ciut otomatis**: setelah 4 detik tanpa interaksi, `expanded` turun ke
  `compact`. `minimal` tidak pernah naik sendiri.
- **Membuka**: interaksi pengguna (klik/ketuk) menaikkan `compact` ke
  `expanded`, dan `minimal` ke `compact` lebih dulu.
- **Laptop**: gulir di atas kapsul = lagu berikutnya/sebelumnya, dan pukul-tahan
  500 ms membuka aksi cepat 312 x 64. Keduanya setara dengan klik/ketuk di HP.
- **Menutup**: interaksi kedua atau gesture tutup menurunkan `expanded` ke
  `compact`.
- Transisi hanya boleh mengikuti graf di atas; lompatan `minimal -> expanded`
  dipecah menjadi dua langkah.
- Setiap transisi memicu animasi pegas. Dengan "kurangi gerakan" aktif,
  animasi diganti fade singkat atau tanpa animasi.

## 5. Spesifikasi tampilan

### Laptop (Windows)

| Bagian               | Ukuran          | Catatan                                  |
|----------------------|-----------------|------------------------------------------|
| Expanded             | 480 x 152 px    | menempel di atas, sudut bawah radius 28 (dikecilkan dari 560 x 176 agar tidak menutupi konten) |
| Compact idle         | 200 x 34 px     |                                          |
| Compact timer        | 260 x 36 px     |                                          |
| Compact rapat        | 300 x 36 px     |                                          |
| Expanded notifikasi  | 312 x 84 px     | radius 28                                |
| Shelf                | 312 x 84 px     | garis putus-putus                        |
| Aksi cepat           | 312 x 64 px     | tekan-tahan, dapat dikustomisasi di quick-actions.json |

Posisi: di tengah atas layar utama. Windows tidak punya notch, jadi kapsul
selalu mengambang dengan jarak 8 px dari tepi atas layar.

Kapsul kedua (Tahap 3) berukuran 34 x 34 px, 8 px di kanan kapsul utama, dan
menerima ketuk untuk menjadikan aktivitasnya utama. Grup kapsul tetap di tengah
layar.

### Warna

- Latar: hitam murni `#000000`
- Teks utama: putih `#FFFFFF`
- Teks sekunder: abu terang `#B0B0B0`
- Aksen: oranye `#FF9F43`
- Kontras teks minimal 4.5:1 terhadap latar hitam.

## 6. Aksesibilitas

- Setiap kapsul punya label screen reader: judul, subjudul, dan progres bila ada.
- Tombol aksi minimal 44 px area sentuh.
- Menghormati "kurangi gerakan".
- Fokus keyboard dapat menjangkau kapsul dan semua aksi.

## 7. Privasi

- Semua data diproses lokal, tanpa analitik.
- Tidak ada izin internet kecuali fitur yang jelas membutuhkannya.
- Izin sistem diminta saat fitur terkait diaktifkan, disertai penjelasan singkat.
